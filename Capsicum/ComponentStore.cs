using System;
using Capsicum.Interfaces;

namespace Capsicum {
    /// <summary>
    /// A pool's storage for one component type, addressed by entity slot index.
    /// </summary>
    public abstract class ComponentStore {
        /// <summary>The exact component type held by this store.</summary>
        public abstract Type ComponentType { get; }

        /// <summary>Number of entities holding this component.</summary>
        public int Count { get; protected set; }

        /// <summary>Incremented by every add and remove; replacing a component in place does not change it.</summary>
        public int Version { get; protected set; }

        internal abstract bool Has(int index);

        internal abstract int IndexAt(int position);

        internal abstract IComponent GetUntyped(int index);

        internal abstract void AddUntyped(Entity entity, IComponent component, bool notify);

        internal abstract void Remove(Entity entity, bool notify);
    }
}
