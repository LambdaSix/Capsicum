#nullable enable
using System;

namespace Capsicum;

/// <summary>The limits of a <see cref="RunStore{T}"/>: the longest run it hands out and how many runs it holds at once.</summary>
public sealed record RunStoreOptions {
    /// <summary>The largest <see cref="RunLength"/> a store accepts.</summary>
    public const int MaximumRunLength = 4096;

    /// <summary>The largest <see cref="Capacity"/> a store accepts.</summary>
    public const int MaximumCapacity = 1 << 20;

    /// <summary>Elements one run may hold; every run reserves this many.</summary>
    public int RunLength { get; }

    /// <summary>Runs the store holds at once; an allocation past it is refused.</summary>
    public int Capacity { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// A limit is not positive, is over its maximum, or the two together reserve more elements than an array can hold.
    /// </exception>
    public RunStoreOptions(int runLength, int capacity) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(runLength, MaximumRunLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(capacity, MaximumCapacity);
        if ((long)runLength * capacity > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity,
                $"{capacity} runs of {runLength} elements is more than an array can hold ({Array.MaxLength}).");
        RunLength = runLength;
        Capacity = capacity;
    }
}
