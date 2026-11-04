#nullable enable
namespace Capsicum.Systems;

/// <summary>A unit of tick logic that a <see cref="SystemSchedule{TContext}"/> runs in a fixed place in its order.</summary>
/// <typeparam name="TContext">What the host hands every system for one run, such as the tick being simulated.</typeparam>
public interface ISystem<TContext> {
    /// <summary>The system's name and declared access; read once, when the schedule is built.</summary>
    SystemDeclaration Declaration { get; }

    /// <summary>Runs the system once.</summary>
    /// <returns>The number of entities the run visited, reported to the schedule's observer.</returns>
    int Run(in TContext context);
}
