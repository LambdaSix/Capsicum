using System;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    /// <summary>
    /// Sparse-set storage for one component type: lookups, adds and removes are O(1) and allocation-free once the
    /// arrays have grown to fit.
    /// </summary>
    /// <remarks>
    /// Removal swaps the last dense entry into the hole, so dense order depends on add/remove history.
    /// Use <see cref="Pool.QueryInto"/> where iteration order must be stable.
    /// </remarks>
    public sealed class ComponentStore<T> : ComponentStore where T : class, IComponent {
        private const int InitialDenseCapacity = 4;

        // Slot index -> dense position + 1, so a zeroed array means "absent" without a fill pass on growth.
        private int[] _sparse = Array.Empty<int>();
        private int[] _indices = new int[InitialDenseCapacity];
        private T[] _dense = new T[InitialDenseCapacity];

        /// <summary>Raised after a component is added through <see cref="Entity.AddComponent{T}"/>.</summary>
        public event ComponentEventHandler<T> Added;

        /// <summary>Raised after a component is removed through <see cref="Entity.RemoveComponent{T}"/>, or on release when notification is requested.</summary>
        public event ComponentEventHandler<T> Removed;

        /// <summary>Raised after <see cref="Entity.ReplaceComponent{T}"/> swaps the instance in place.</summary>
        public event ComponentReplacedHandler<T> Replaced;

        /// <inheritdoc />
        public override Type ComponentType => typeof(T);

        internal T Get(int index) {
            var sparse = _sparse;
            if ((uint)index >= (uint)sparse.Length) return null;
            var position = sparse[index];
            return position == 0 ? null : _dense[position - 1];
        }

        internal override bool Has(int index) => (uint)index < (uint)_sparse.Length && _sparse[index] != 0;

        internal override int IndexAt(int position) => _indices[position];

        internal override IComponent GetUntyped(int index) => Get(index);

        internal override void AddUntyped(Entity entity, IComponent component, bool notify) =>
            Add(entity, (T)component, notify);

        internal void Add(Entity entity, T component, bool notify) {
            var index = entity.Handle.Index;
            if (Has(index)) {
                throw new ComponentAlreadyRegisteredException(entity, component,
                    $"Component type '{typeof(T).Name}' is already registered");
            }

            if (index >= _sparse.Length) Array.Resize(ref _sparse, Math.Max(index + 1, _sparse.Length * 2));
            if (Count == _dense.Length) {
                Array.Resize(ref _dense, _dense.Length * 2);
                Array.Resize(ref _indices, _indices.Length * 2);
            }

            _dense[Count] = component;
            _indices[Count] = index;
            Count++;
            _sparse[index] = Count;
            Version++;

            if (notify) Added?.Invoke(entity, component);
        }

        internal override void Remove(Entity entity, bool notify) {
            var index = entity.Handle.Index;
            if (!Has(index)) {
                throw new ComponentNotRegisteredException(entity,
                    $"Component type '{typeof(T).Name}' is not registered");
            }

            var position = _sparse[index] - 1;
            var removed = _dense[position];
            var last = Count - 1;
            if (position != last) {
                _dense[position] = _dense[last];
                _indices[position] = _indices[last];
                _sparse[_indices[position]] = position + 1;
            }

            _dense[last] = null;
            _sparse[index] = 0;
            Count = last;
            Version++;

            if (notify) Removed?.Invoke(entity, removed);
        }

        internal T Replace(Entity entity, T component) {
            var index = entity.Handle.Index;
            if (!Has(index)) {
                throw new ComponentNotRegisteredException(entity,
                    $"Component type '{typeof(T).Name}' is not registered");
            }

            var position = _sparse[index] - 1;
            var previous = _dense[position];
            _dense[position] = component;
            Replaced?.Invoke(entity, previous, component);
            return previous;
        }
    }
}
