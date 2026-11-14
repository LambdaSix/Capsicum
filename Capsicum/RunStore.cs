#nullable enable
using System;

namespace Capsicum;

/// <summary>
/// A pooled side store for component data whose size varies by entity, such as a table sized by the entity's type. Each run is a slot of up to
/// <see cref="RunStoreOptions.RunLength"/> elements, reached by the <see cref="RunHandle"/> kept in the owning component and read and written as a span.
/// </summary>
/// <remarks>
/// <para>
/// A struct component cannot hold an array, because copies of the struct would share it. It holds a <see cref="RunHandle"/> instead, and the elements live here. The
/// store is not tied to a pool: the code that adds the owning component allocates the run, and the code that despawns the entity frees it.
/// </para>
/// <para>
/// A new run reads as zeroes, and freeing a run clears it, so a reused slot never shows an earlier owner's data. Freed slots are reused before the store grows, so once
/// the live count has peaked, allocating, freeing, reading and writing allocate nothing. The store grows by doubling up to <see cref="RunStoreOptions.Capacity"/> and
/// refuses an allocation past it without changing anything. Slots are handed out in a fixed order given the same calls, but nothing here enumerates runs, so outcomes
/// never depend on which slot a run got; a save writes each owner's elements and a restore allocates them again.
/// </para>
/// <para>
/// A span is valid until the next allocation, which may move the elements. Access is not checked by a <see cref="Systems.SystemAccessChecker"/>: a system reaches a run
/// through its owning component, whose access it declares. Like a <see cref="Pool"/>, the store is not thread-safe.
/// </para>
/// </remarks>
public sealed class RunStore<T> where T : struct {
    private const int InitialSlots = 16;

    private readonly int _runLength;
    private T[] _elements;
    private int[] _generations;
    private int[] _lengths;
    private int[] _free;
    private int _freeCount;
    private int _issued;

    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public RunStore(RunStoreOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _runLength = options.RunLength;
        var slots = Math.Min(InitialSlots, options.Capacity);
        _elements = new T[slots * _runLength];
        _generations = new int[slots];
        _lengths = new int[slots];
        _free = new int[slots];
    }

    /// <summary>The store's limits.</summary>
    public RunStoreOptions Options { get; }

    /// <summary>Live runs, not counting <see cref="RunHandle.None"/>.</summary>
    public int Count { get; private set; }

    /// <summary>Runs that can still be allocated before the store refuses; check it before a change that needs several runs.</summary>
    public int Available => Options.Capacity - Count;

    /// <summary>
    /// Allocates a run of <paramref name="length"/> zeroed elements, or returns false with <see cref="RunHandle.None"/> and no change when the store is full. A length of
    /// zero returns <see cref="RunHandle.None"/> and takes no slot.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or longer than <see cref="RunStoreOptions.RunLength"/>.</exception>
    public bool TryAllocate(int length, out RunHandle handle) {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, _runLength);
        handle = RunHandle.None;
        if (length == 0) return true;

        int slot;
        if (_freeCount > 0) slot = _free[--_freeCount];
        else if (_issued < Options.Capacity) {
            if (_issued == _lengths.Length) Grow();
            slot = _issued++;
        }
        else return false;

        var generation = _generations[slot] + 1;
        if (generation <= 0) generation = 1;
        _generations[slot] = generation;
        _lengths[slot] = length;
        Count++;
        handle = new RunHandle(slot, generation, length);
        return true;
    }

    /// <summary>The run's elements, to read and write in place; empty for <see cref="RunHandle.None"/>.</summary>
    /// <exception cref="ArgumentException">The handle names a run this store has freed, or never allocated.</exception>
    public Span<T> Get(RunHandle handle) {
        if (handle.IsNone) return Span<T>.Empty;
        EnsureLive(handle);
        return _elements.AsSpan(handle.Slot * _runLength, handle.Length);
    }

    /// <summary>True when the handle names a live run of this store, or is <see cref="RunHandle.None"/>.</summary>
    public bool IsLive(RunHandle handle) =>
        handle.IsNone || (uint)handle.Slot < (uint)_issued && _generations[handle.Slot] == handle.Generation && _lengths[handle.Slot] == handle.Length;

    /// <summary>Clears the run and returns its slot for reuse; does nothing for <see cref="RunHandle.None"/>.</summary>
    /// <exception cref="ArgumentException">The handle names a run this store has freed, or never allocated.</exception>
    public void Free(RunHandle handle) {
        if (handle.IsNone) return;
        EnsureLive(handle);
        _elements.AsSpan(handle.Slot * _runLength, handle.Length).Clear();
        _lengths[handle.Slot] = 0;
        _free[_freeCount++] = handle.Slot;
        Count--;
    }

    private void EnsureLive(RunHandle handle) {
        if (!IsLive(handle)) throw new ArgumentException($"The {typeof(T).Name} run handle is stale or belongs to another store.", nameof(handle));
    }

    private void Grow() {
        var slots = (int)Math.Min(Options.Capacity, (long)_lengths.Length * 2);
        Array.Resize(ref _elements, slots * _runLength);
        Array.Resize(ref _generations, slots);
        Array.Resize(ref _lengths, slots);
        Array.Resize(ref _free, slots);
    }
}
