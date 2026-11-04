#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Capsicum.Systems;

/// <summary>
/// Runs systems in the order the host lists them, and reports where that order contradicts their declared access.
/// </summary>
/// <remarks>
/// The schedule never reorders: the host's list is the order. It runs on the caller's thread, and a run allocates
/// nothing beyond what its systems and observer allocate.
/// </remarks>
public sealed class SystemSchedule<TContext> {
    private readonly ISystem<TContext>[] _systems;
    private readonly SystemDeclaration[] _declarations;
    private readonly ISystemObserver? _observer;
    private readonly SystemAccessChecker? _checker;

    /// <summary>Builds a schedule over <paramref name="systems"/>, in that order.</summary>
    /// <param name="systems">The systems, in run order.</param>
    /// <param name="observer">Called around each system; null observes nothing.</param>
    /// <param name="checker">Holds each running system to its declaration; null checks nothing.</param>
    /// <exception cref="ArgumentException">A system is null or listed twice, or two systems share a name.</exception>
    public SystemSchedule(IEnumerable<ISystem<TContext>> systems, ISystemObserver? observer = null, SystemAccessChecker? checker = null) {
        ArgumentNullException.ThrowIfNull(systems);
        _systems = [.. systems];
        _declarations = new SystemDeclaration[_systems.Length];
        _observer = observer;
        _checker = checker;

        var instances = new HashSet<ISystem<TContext>>(ReferenceEqualityComparer.Instance);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var position = 0; position < _systems.Length; position++) {
            var system = _systems[position] ?? throw new ArgumentException($"The system at position {position} is null.", nameof(systems));
            if (!instances.Add(system)) throw new ArgumentException($"The system at position {position} is already listed.", nameof(systems));

            var declaration = system.Declaration ?? throw new ArgumentException($"The system at position {position} has no declaration.", nameof(systems));
            if (!names.Add(declaration.Name)) throw new ArgumentException($"Two systems are named '{declaration.Name}'.", nameof(systems));
            _declarations[position] = declaration;
        }

        Findings = Validate(_declarations);
    }

    /// <summary>Number of systems in the schedule.</summary>
    public int Count => _systems.Length;

    /// <summary>The declarations of the systems, in run order.</summary>
    public ReadOnlySpan<SystemDeclaration> Systems => _declarations;

    /// <summary>
    /// Every place a system reads a type that a later system writes, in reader then writer order. The host decides which
    /// of these are intended, for example a pass that deliberately reads last run's value.
    /// </summary>
    public ImmutableArray<ScheduleFinding> Findings { get; }

    /// <summary>Runs every system once, in order.</summary>
    /// <remarks>An exception from a system or the observer stops the run and propagates; later systems do not run.</remarks>
    public void Run(in TContext context) {
        var systems = _systems;
        var declarations = _declarations;
        for (var position = 0; position < systems.Length; position++) {
            var declaration = declarations[position];
            _observer?.Starting(position, declaration);

            int visited;
            _checker?.Enter(declaration);
            try {
                visited = systems[position].Run(in context);
            }
            finally {
                _checker?.Exit();
            }

            _observer?.Finished(position, declaration, visited);
        }
    }

    private static ImmutableArray<ScheduleFinding> Validate(SystemDeclaration[] declarations) {
        var findings = ImmutableArray.CreateBuilder<ScheduleFinding>();
        for (var reader = 0; reader < declarations.Length; reader++) {
            foreach (var type in declarations[reader].Reads) {
                for (var writer = reader + 1; writer < declarations.Length; writer++) {
                    if (declarations[writer].Writes.Contains(type))
                        findings.Add(new ScheduleFinding(declarations[reader], reader, declarations[writer], writer, type));
                }
            }
        }

        return findings.ToImmutable();
    }
}
