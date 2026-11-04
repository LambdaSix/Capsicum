#nullable enable
using System;

namespace Capsicum.Events;

/// <summary>One reader's position in an <see cref="EventBuffer{T}"/>.</summary>
public sealed class EventReader<T> where T : struct {
    private readonly EventBuffer<T> _buffer;

    internal EventReader(EventBuffer<T> buffer) => _buffer = buffer;

    internal int Position { get; set; }

    /// <summary>Returns the events emitted since this reader last read, and moves past them.</summary>
    /// <remarks>The span is valid until the buffer is next cleared.</remarks>
    public ReadOnlySpan<T> Read() {
        var events = _buffer.Since(Position);
        Position += events.Length;
        return events;
    }
}
