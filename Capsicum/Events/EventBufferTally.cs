#nullable enable
namespace Capsicum.Events;

/// <summary>What an <see cref="EventBuffer{T}"/> held when it was cleared.</summary>
/// <param name="Emitted">Events accepted during the run.</param>
/// <param name="Unread">Accepted events at least one reader never read; all of them when the buffer has no reader.</param>
/// <param name="Refused">Events refused because the buffer was full.</param>
public readonly record struct EventBufferTally(int Emitted, int Unread, int Refused);
