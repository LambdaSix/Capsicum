#nullable enable
using System;

namespace Capsicum.Systems;

/// <summary>A running system touched a type its <see cref="SystemDeclaration"/> does not allow.</summary>
public sealed class UndeclaredAccessException : InvalidOperationException {
    /// <summary>Creates the exception for one undeclared access.</summary>
    public UndeclaredAccessException(SystemDeclaration system, Type type, bool write)
        : base($"System '{system.Name}' {(write ? "wrote" : "read")} {type.Name} without declaring it in its {(write ? "writes" : "reads or writes")}.") {
        System = system;
        Type = type;
        IsWrite = write;
    }

    /// <summary>The system that was running.</summary>
    public SystemDeclaration System { get; }

    /// <summary>The type it accessed.</summary>
    public Type Type { get; }

    /// <summary>True when the access was a write.</summary>
    public bool IsWrite { get; }
}
