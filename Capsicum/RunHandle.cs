#nullable enable
namespace Capsicum;

/// <summary>
/// Names one run in a <see cref="RunStore{T}"/>: its slot, the generation it was allocated on and its length. Keep it in the component that owns the run; it is
/// pool-local, like an <see cref="EntityHandle"/>, so never save it.
/// </summary>
/// <remarks>
/// The default handle is <see cref="None"/>, the empty run: it reads as an empty span and freeing it does nothing. A handle does not name its store, so a handle used
/// with another store of the same element type is caught only when that store has no live run in the slot at that generation.
/// </remarks>
public readonly record struct RunHandle {
    internal RunHandle(int slot, int generation, int length) => (Slot, Generation, Length) = (slot, generation, length);

    /// <summary>The empty run, which every store resolves without holding a slot for it.</summary>
    public static RunHandle None => default;

    /// <summary>Elements in the run.</summary>
    public int Length { get; }

    /// <summary>True for <see cref="None"/>.</summary>
    public bool IsNone => Generation == 0;

    internal int Slot { get; }

    internal int Generation { get; }
}
