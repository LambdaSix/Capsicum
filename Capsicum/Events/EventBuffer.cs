#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Capsicum.Systems;

namespace Capsicum.Events;

/// <summary>
/// A fixed-capacity buffer of one event type for one run. A reader sees every event emitted before it reads, so a
/// reader placed after the emitters gets the run's events in the same run.
/// </summary>
/// <remarks>
/// The buffer never grows: an event past capacity is refused and counted. The host calls <see cref="Clear"/> at the end
/// of each run, which also reports the events some reader never saw. Emitting and reading allocate nothing.
/// </remarks>
public sealed class EventBuffer<T> where T : struct {
    private readonly T[] _events;
    private readonly List<EventReader<T>> _readers = [];
    private int _count;
    private int _refused;

    /// <summary>Creates a buffer that holds at most <paramref name="capacity"/> events per run.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public EventBuffer(int capacity) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _events = new T[capacity];
    }

    /// <summary>Most events the buffer holds per run.</summary>
    public int Capacity => _events.Length;

    /// <summary>Events emitted since the last <see cref="Clear"/>.</summary>
    public int Count => _count;

    /// <summary>Checks emits and reads against the running system's declaration; null, the default, checks nothing.</summary>
    public SystemAccessChecker? AccessChecker { get; set; }

    /// <summary>Appends an event, or refuses it when the buffer is full.</summary>
    /// <returns>False when the event was refused; the refusal is counted in the next <see cref="Clear"/>.</returns>
    public bool TryEmit(in T item) {
        AccessChecker?.Write(ComponentType<T>.Id);
        if (_count == _events.Length) {
            _refused++;
            return false;
        }

        _events[_count++] = item;
        return true;
    }

    /// <summary>Creates a reader with its own position in this buffer. Create readers when composing, not per run.</summary>
    public EventReader<T> CreateReader() {
        var reader = new EventReader<T>(this);
        _readers.Add(reader);
        return reader;
    }

    /// <summary>Empties the buffer and rewinds every reader.</summary>
    /// <returns>What the run emitted, how many of those events some reader never read, and how many were refused.</returns>
    /// <remarks>Clearing inside a running system is a write, so a reader cannot discard events other readers have not seen.</remarks>
    public EventBufferTally Clear() {
        AccessChecker?.Write(ComponentType<T>.Id);
        var oldest = _count;
        foreach (var reader in _readers) oldest = Math.Min(oldest, reader.Position);
        var tally = new EventBufferTally(_count, _readers.Count == 0 ? _count : _count - oldest, _refused);

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) Array.Clear(_events, 0, _count);
        foreach (var reader in _readers) reader.Position = 0;
        _count = 0;
        _refused = 0;
        return tally;
    }

    internal ReadOnlySpan<T> Since(int position) {
        AccessChecker?.Read(ComponentType<T>.Id);
        return _events.AsSpan(position, _count - position);
    }
}
