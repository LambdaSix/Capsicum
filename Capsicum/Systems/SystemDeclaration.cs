#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Capsicum.Systems;

/// <summary>
/// A system's name and the component, event and resource types it reads and writes.
/// </summary>
/// <remarks>
/// Reads and writes are checked separately, so a system that updates a type in place lists it in both.
/// <see cref="SystemSchedule{TContext}"/> validates every read against the order it is given, and a
/// <see cref="SystemAccessChecker"/> holds a running system to its declaration.
/// </remarks>
public sealed class SystemDeclaration {
    private readonly bool[] _mayRead;
    private readonly bool[] _mayWrite;

    private SystemDeclaration(string name, ImmutableArray<Type> reads, ImmutableArray<Type> writes) {
        Name = name;
        Reads = reads;
        Writes = writes;
        _mayRead = Table(reads);
        _mayWrite = Table(writes);
    }

    /// <summary>Stable name, unique within a schedule; observers and metrics key on it.</summary>
    public string Name { get; }

    /// <summary>Types the system reads.</summary>
    public ImmutableArray<Type> Reads { get; }

    /// <summary>Types the system writes.</summary>
    public ImmutableArray<Type> Writes { get; }

    /// <summary>Declares a system.</summary>
    /// <exception cref="ArgumentException">The name is blank, or a list names a type twice.</exception>
    public static SystemDeclaration Create(string name, IEnumerable<Type> reads, IEnumerable<Type> writes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(reads);
        ArgumentNullException.ThrowIfNull(writes);

        var readList = reads.ToImmutableArray();
        var writeList = writes.ToImmutableArray();
        RefuseRepeats(readList, name, "reads");
        RefuseRepeats(writeList, name, "writes");

        return new SystemDeclaration(name, readList, writeList);
    }

    internal bool MayRead(int typeId) => typeId < _mayRead.Length && _mayRead[typeId];

    internal bool MayWrite(int typeId) => typeId < _mayWrite.Length && _mayWrite[typeId];

    /// <inheritdoc />
    public override string ToString() => Name;

    private static void RefuseRepeats(ImmutableArray<Type> types, string name, string list) {
        var seen = new HashSet<Type>();
        foreach (var type in types) {
            ArgumentNullException.ThrowIfNull(type);
            if (!seen.Add(type)) throw new ArgumentException($"System '{name}' lists '{type.Name}' in its {list} more than once.");
        }
    }

    private static bool[] Table(ImmutableArray<Type> types) {
        var ids = new List<int>(types.Length);
        foreach (var type in types) ids.Add(ComponentType.IdOf(type));

        var size = 0;
        foreach (var id in ids) size = Math.Max(size, id + 1);
        var table = new bool[size];
        foreach (var id in ids) table[id] = true;
        return table;
    }
}
