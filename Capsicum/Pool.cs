using System;
using System.Collections;
using System.Collections.Generic;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    /// <summary>
    /// Owns a set of entities and one sparse-set store per component type they use.
    /// </summary>
    /// <remarks>
    /// Released slot indices are reused first-in first-out with their generation incremented, so a stale
    /// <see cref="EntityHandle"/> never resolves to the new occupant. <see cref="Entity"/> objects are never reused.
    /// A pool is not thread-safe.
    /// </remarks>
    public sealed class Pool : IEnumerable<Entity> {
        private const int MinimumSlotCapacity = 4;

        private static readonly Comparison<Entity> ByHandleIndex = (left, right) => left.Handle.Index.CompareTo(right.Handle.Index);

        private readonly Queue<int> _freeIndices = new Queue<int>();
        private Entity[] _slots;
        private int[] _generations;
        private bool[] _listed;
        private int _nextIndex;
        private ComponentStore[] _stores = Array.Empty<ComponentStore>();

        /// <summary>Raised after an entity is created, before any initial components are added.</summary>
        public event EventHandler<PoolChanged> OnEntityCreated;

        /// <summary>Raised before <see cref="RemoveEntity"/> releases an entity.</summary>
        public event EventHandler<PoolChanged> OnEntityRemoving;

        /// <summary>Raised after <see cref="RemoveEntity"/> has released an entity.</summary>
        public event EventHandler<PoolChanged> OnEntityRemoved;

        /// <summary>Creates a pool.</summary>
        /// <param name="capacity">Initial slot capacity; the pool grows past it as needed.</param>
        /// <param name="creationIndex">First slot index to hand out; values below 1 start at 1.</param>
        public Pool(int capacity = 16, int creationIndex = 0) {
            _nextIndex = Math.Max(1, creationIndex);
            var slots = Math.Max(MinimumSlotCapacity, Math.Max(capacity, _nextIndex + 1));
            _slots = new Entity[slots];
            _generations = new int[slots];
            _listed = new bool[slots];
        }

        /// <summary>Number of live entities enumerated by this pool.</summary>
        public int Count { get; private set; }

        internal int SlotLimit => _nextIndex;

        internal int SlotVersion { get; private set; }

        internal Entity SlotAt(int index) => _slots[index];

        internal bool IsListed(int index) => _listed[index];

        /// <summary>
        /// Create a new entity in this pool
        /// </summary>
        /// <param name="addToPool">False keeps the entity out of enumeration, queries and <see cref="Count"/>.</param>
        public Entity CreateEntity(bool addToPool = true) {
            var entity = new Entity(this);
            Attach(entity, addToPool);
            return entity;
        }

        /// <summary>
        /// Create a new entity in this pool with a prefilled list of components
        /// </summary>
        public Entity CreateEntity(IEnumerable<IComponent> components, bool addToPool = true) {
            ArgumentNullException.ThrowIfNull(components);
            var entity = CreateEntity(false);
            foreach (var component in components) {
                AddUntyped(entity, component, notify: false);
            }

            if (addToPool) List(entity.Handle.Index);
            return entity;
        }

        /// <summary>Create a new entity in this pool and let <paramref name="aspect"/> compose it.</summary>
        public Entity CreateEntity(IEntityAspect aspect, bool addToPool = true) {
            ArgumentNullException.ThrowIfNull(aspect);
            var entity = CreateEntity(false);
            aspect.Setup(entity);

            if (addToPool) List(entity.Handle.Index);
            return entity;
        }

        /// <summary>Release an entity this pool enumerates.</summary>
        /// <param name="notifyComponents">Raise each store's <c>Removed</c> event for the components released with it.</param>
        /// <exception cref="EntityNotInPoolException">The entity is not live and enumerated in this pool.</exception>
        public void RemoveEntity(Entity entity, bool notifyComponents = false) {
            ArgumentNullException.ThrowIfNull(entity);
            if (!HasEntity(entity))
                throw new EntityNotInPoolException(entity, "Could not remove entity");

            OnEntityRemoving?.Invoke(this, new PoolChanged(this, entity));
            Release(entity, notifyComponents);
            OnEntityRemoved?.Invoke(this, new PoolChanged(this, entity));
        }

        /// <summary>Release every entity this pool enumerates.</summary>
        public void RemoveAllEntities() {
            for (var index = 1; index < _nextIndex; index++) {
                if (_listed[index]) RemoveEntity(_slots[index]);
            }
        }

        /// <summary>True when <paramref name="entity"/> is live and enumerated in this pool.</summary>
        public bool HasEntity(Entity entity) =>
            entity != null && entity.Pool == this && IsAlive(entity.Handle) && _listed[entity.Handle.Index];

        /// <summary>True when <paramref name="handle"/> still names a live entity of this pool.</summary>
        public bool IsAlive(EntityHandle handle) =>
            handle.Index > 0 && handle.Index < _nextIndex && _slots[handle.Index] != null &&
            _generations[handle.Index] == handle.Generation;

        /// <summary>Resolve a handle to its live entity.</summary>
        public bool TryGetEntity(EntityHandle handle, out Entity entity) {
            entity = IsAlive(handle) ? _slots[handle.Index] : null;
            return entity != null;
        }

        /// <summary>Returns this pool's store for <typeparamref name="T"/>, creating it on first use.</summary>
        public ComponentStore<T> Components<T>() where T : class, IComponent {
            var id = ComponentType<T>.Id;
            EnsureStoreCapacity(id);
            return (ComponentStore<T>)(_stores[id] ??= new ComponentStore<T>());
        }

        /// <summary>Starts a query over this pool's enumerated entities.</summary>
        public Query Query() => new Query(this);

        /// <summary>
        /// Clears <paramref name="buffer"/> and fills it with the query's matches in ascending handle index, which
        /// does not depend on store history. Reuse the buffer to keep this allocation-free.
        /// </summary>
        public void QueryInto(Query query, List<Entity> buffer) {
            ArgumentNullException.ThrowIfNull(buffer);
            if (query.Pool != this) throw new ArgumentException("The query belongs to another pool.", nameof(query));

            buffer.Clear();
            foreach (var entity in query) {
                buffer.Add(entity);
            }

            buffer.Sort(ByHandleIndex);
        }

        /// <summary>Returns an allocation-free enumerator over the enumerated entities in handle index order.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        IEnumerator<Entity> IEnumerable<Entity>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>The enumerated entities of this pool.</summary>
        public IEnumerable<Entity> GetAllEntities() => this;

        internal void Attach(Entity entity, bool addToPool) {
            var index = _freeIndices.Count > 0 ? _freeIndices.Dequeue() : _nextIndex++;
            EnsureSlotCapacity(index);

            entity.Handle = new EntityHandle(index, _generations[index]);
            entity.IsEnabled = true;
            _slots[index] = entity;
            SlotVersion++;
            if (addToPool) List(index);

            OnEntityCreated?.Invoke(this, new PoolChanged(this, entity));
        }

        internal void Release(Entity entity, bool notifyComponents) {
            if (!entity.IsEnabled || entity.Pool != this) return;

            var index = entity.Handle.Index;
            var stores = _stores;
            for (var id = 0; id < stores.Length; id++) {
                var store = stores[id];
                if (store != null && store.Has(index)) store.Remove(entity, notifyComponents);
            }

            if (_listed[index]) {
                _listed[index] = false;
                Count--;
            }

            _slots[index] = null;
            _generations[index]++;
            _freeIndices.Enqueue(index);
            SlotVersion++;
            entity.IsEnabled = false;
        }

        internal T Get<T>(int index) where T : class, IComponent {
            var id = ComponentType<T>.Id;
            var stores = _stores;
            return id < stores.Length && stores[id] is ComponentStore<T> store ? store.Get(index) : null;
        }

        internal bool Has<T>(int index) {
            var id = ComponentType<T>.Id;
            var stores = _stores;
            return id < stores.Length && stores[id] is { } store && store.Has(index);
        }

        internal ComponentStore[] Stores => _stores;

        internal void AddUntyped(Entity entity, IComponent component, bool notify) {
            ArgumentNullException.ThrowIfNull(component);
            var type = component.GetType();
            var id = ComponentType.IdOf(type);
            EnsureStoreCapacity(id);
            var store = _stores[id] ??= (ComponentStore)Activator.CreateInstance(typeof(ComponentStore<>).MakeGenericType(type));
            store.AddUntyped(entity, component, notify);
        }

        private void List(int index) {
            if (_listed[index]) return;
            _listed[index] = true;
            Count++;
            SlotVersion++;
        }

        private void EnsureSlotCapacity(int index) {
            if (index < _slots.Length) return;
            var size = Math.Max(index + 1, _slots.Length * 2);
            Array.Resize(ref _slots, size);
            Array.Resize(ref _generations, size);
            Array.Resize(ref _listed, size);
        }

        private void EnsureStoreCapacity(int id) {
            if (id >= _stores.Length) Array.Resize(ref _stores, Math.Max(id + 1, _stores.Length * 2));
        }

        /// <summary>Enumerates a pool's enumerated entities in handle index order.</summary>
        public struct Enumerator : IEnumerator<Entity> {
            private readonly Pool _pool;
            private readonly int _slotVersion;
            private int _index;

            internal Enumerator(Pool pool) {
                _pool = pool;
                _slotVersion = pool.SlotVersion;
                _index = 0;
                Current = null;
            }

            /// <inheritdoc />
            public Entity Current { get; private set; }

            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext() {
                if (_pool.SlotVersion != _slotVersion)
                    throw new InvalidOperationException("The pool changed during enumeration; copy its entities before creating or removing any.");

                while (++_index < _pool._nextIndex) {
                    if (_pool._listed[_index]) {
                        Current = _pool._slots[_index];
                        return true;
                    }
                }

                Current = null;
                return false;
            }

            /// <inheritdoc />
            public void Reset() {
                _index = 0;
                Current = null;
            }

            /// <inheritdoc />
            public void Dispose() { }
        }
    }
}
