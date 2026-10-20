using System;

namespace Capsicum {
    /// <summary>
    /// Pool-local identity of a live entity: a recyclable slot index plus the generation that slot was on when the
    /// entity was created. A handle kept past its entity's release no longer matches the slot.
    /// </summary>
    public readonly struct EntityHandle : IEquatable<EntityHandle>, IComparable<EntityHandle> {
        /// <summary>Slot index in the owning pool; 0 is never allocated.</summary>
        public int Index { get; }

        /// <summary>Number of times the slot had been released before this entity took it.</summary>
        public int Generation { get; }

        /// <summary>Creates a handle for a slot index and generation.</summary>
        public EntityHandle(int index, int generation) {
            Index = index;
            Generation = generation;
        }

        /// <summary>True for any handle a pool has issued.</summary>
        public bool IsValid => Index > 0;

        /// <inheritdoc />
        public bool Equals(EntityHandle other) => Index == other.Index && Generation == other.Generation;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is EntityHandle other && Equals(other);

        // Not HashCode.Combine: its per-process seed would make hash-ordered collections differ between runs.
        /// <inheritdoc />
        public override int GetHashCode() => unchecked(Index * 397 ^ Generation);

        /// <inheritdoc />
        public int CompareTo(EntityHandle other) {
            var byIndex = Index.CompareTo(other.Index);
            return byIndex != 0 ? byIndex : Generation.CompareTo(other.Generation);
        }

        /// <summary>Compares two handles for equality.</summary>
        public static bool operator ==(EntityHandle left, EntityHandle right) => left.Equals(right);

        /// <summary>Compares two handles for inequality.</summary>
        public static bool operator !=(EntityHandle left, EntityHandle right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => $"{Index}.{Generation}";
    }
}
