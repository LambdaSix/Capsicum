#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Capsicum.Systems;

/// <summary>
/// A system's name and the component, event and resource types it reads and writes.
/// </summary>
/// <remarks>
/// Writing a type also allows reading it. <see cref="SystemSchedule{TContext}"/> validates the declarations against the
/// order it is given, and a <see cref="SystemAccessChecker"/> holds a running system to them.
/// </remarks>
public sealed class SystemDeclaration {
    private readonly bool[] _mayRead;
    private readonly bool[] _mayWrite;

    private SystemDeclaration(string name, ImmutableArray<Type> reads, ImmutableArray<Type> writes) {
        Name = name;
        Reads = reads;
        Writes = writes;
        _mayRead = Table(reads, writes);
        _mayWrite = Table(writes, ImmutableArray<Type>.Empty);
    }

    /// <summary>Stable name, unique within a schedule; observers and metrics key on it.</summary>
    public string Name { get; }

    /// <summary>Types the system reads and does not write.</summary>
    public ImmutableArray<Type> Reads { get; }

    /// <summary>Types the system writes, and may also read.</summary>
    public ImmutableArray<Type> Writes { get; }

    /// <summary>Declares a system.</summary>
    /// <exception cref="ArgumentException">The name is blank, or a type is listed twice or in both lists.</exception>
    public static SystemDeclaration Create(string name, IEnumerable<Type> reads, IEnumerable<Type> writes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(reads);
        ArgumentNullException.ThrowIfNull(writes);

        var readList = reads.ToImmutableArray();
        var writeList = writes.ToImmutableArray();
        var seen = new HashSet<Type>();
        foreach (var type in readList) AddOnce(seen, type, name);
        foreach (var type in writeList) AddOnce(seen, type, name);

        return new SystemDeclaration(name, readList, writeList);
    }

    internal bool MayRead(int typeId) => typeId < _mayRead.Length && _mayRead[typeId];

    internal bool MayWrite(int typeId) => typeId < _mayWrite.Length && _mayWrite[typeId];

    /// <inheritdoc />
    public override string ToString() => Name;

    private static void AddOnce(HashSet<Type> seen, Type type, string name) {
        ArgumentNullException.ThrowIfNull(type);
        if (!seen.Add(type))
            throw new ArgumentException($"System '{name}' declares '{type.Name}' more than once; list it in reads or writes, not both.");
    }

    private static bool[] Table(ImmutableArray<Type> first, ImmutableArray<Type> second) {
        var ids = new List<int>(first.Length + second.Length);
        foreach (var type in first) ids.Add(ComponentType.IdOf(type));
        foreach (var type in second) ids.Add(ComponentType.IdOf(type));

        var size = 0;
        foreach (var id in ids) size = Math.Max(size, id + 1);
        var table = new bool[size];
        foreach (var id in ids) table[id] = true;
        return table;
    }
}
