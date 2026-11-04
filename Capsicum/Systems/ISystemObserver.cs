#nullable enable
namespace Capsicum.Systems;

/// <summary>
/// Called around every system a <see cref="SystemSchedule{TContext}"/> runs, so a host can measure time, allocation and
/// entities visited per system. Capsicum never reads a clock itself.
/// </summary>
/// <remarks>An observer that allocates is counted against the system it observes.</remarks>
public interface ISystemObserver {
    /// <summary>Called immediately before the system at <paramref name="position"/> runs.</summary>
    void Starting(int position, SystemDeclaration system);

    /// <summary>Called immediately after the system at <paramref name="position"/> returns; not called if it throws.</summary>
    void Finished(int position, SystemDeclaration system, int visited);
}
