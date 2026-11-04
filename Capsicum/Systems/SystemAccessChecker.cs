#nullable enable
namespace Capsicum.Systems;

/// <summary>
/// Debug check that holds the running system to its declaration. Attach one instance to the schedule and to every
/// <see cref="Pool"/> and <see cref="Events.EventBuffer{T}"/> its systems use.
/// </summary>
/// <remarks>
/// Access outside a running system is not checked. A schedule run from inside a system is checked against the inner
/// system's declaration, and the outer one applies again when it returns. With class components a read hands out a mutable instance, so a
/// write through it is only seen as a read; adding, removing and replacing components, releasing entities and emitting
/// events are seen as writes.
/// </remarks>
public sealed class SystemAccessChecker {
    private SystemDeclaration? _running;

    internal SystemDeclaration? Enter(SystemDeclaration system) {
        var outer = _running;
        _running = system;
        return outer;
    }

    internal void Exit(SystemDeclaration? outer) => _running = outer;

    internal void Read(int typeId) {
        if (_running is { } system && !system.MayRead(typeId))
            throw new UndeclaredAccessException(system, ComponentType.TypeOf(typeId), write: false);
    }

    internal void Write(int typeId) {
        if (_running is { } system && !system.MayWrite(typeId))
            throw new UndeclaredAccessException(system, ComponentType.TypeOf(typeId), write: true);
    }
}
