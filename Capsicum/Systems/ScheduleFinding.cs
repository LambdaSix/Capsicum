#nullable enable
using System;

namespace Capsicum.Systems;

/// <summary>
/// A system that reads <see cref="Type"/> placed before a later system that writes it, so in one run the reader sees the
/// value or events as they were before the writer ran.
/// </summary>
/// <param name="Reader">The system that reads <paramref name="Type"/>.</param>
/// <param name="ReaderPosition">The reader's position in the schedule.</param>
/// <param name="Writer">The later system that writes <paramref name="Type"/>.</param>
/// <param name="WriterPosition">The writer's position in the schedule.</param>
/// <param name="Type">The type read before it is written.</param>
public sealed record ScheduleFinding(SystemDeclaration Reader, int ReaderPosition, SystemDeclaration Writer, int WriterPosition, Type Type) {
    /// <inheritdoc />
    public override string ToString() =>
        $"'{Reader.Name}' (position {ReaderPosition}) reads {Type.Name} before '{Writer.Name}' (position {WriterPosition}) writes it";
}
