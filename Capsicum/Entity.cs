using System;
using System.Collections.Generic;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    /// <summary>
    /// A live slot in a <see cref="Pool"/>. Its components are held by the pool's per-type stores, so every
    /// lookup is an O(1) array read that allocates nothing.
    /// </summary>
    /// <remarks>
    /// Lookups match the exact component type a component was added as at runtime, not base types or interfaces.
    /// Once released, the entity reports no components and refuses new ones; its slot goes to a new entity object.
    /// </remarks>
    public sealed class Entity {
        /// <summary>Creates an entity in a private pool of its own.</summary>
        public Entity() : this(new Pool(capacity: 1)) {
            Pool.Attach(this, addToPool: true);
        }

        internal Entity(Pool pool) {
            Pool = pool;
        }

        /// <summary>True until the entity is released.</summary>
        public bool IsEnabled { get; internal set; }

        /// <summary>The pool that owns this entity's storage.</summary>
        public Pool Pool { get; }

        /// <summary>This entity's slot and generation in <see cref="Pool"/>.</summary>
        public EntityHandle Handle { get; internal set; }

        internal int CreationIndex => Handle.Index;

        /// <summary>
        /// Returns the <seealso cref="IComponent"/> of the requested type.
        /// </summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        public T GetComponent<T>() where T : class, IComponent =>
            GetComponentOf<T>() ?? throw new ComponentNotRegisteredException(this,
                $"Component of type '{typeof(T).Name}' was not registered");

        /// <summary>
        /// Try and get the specified component from this Entity.
        /// </summary>
        /// <returns>The instance of the component if it was found, otherwise null</returns>
        public T GetComponentOf<T>() where T : class, IComponent =>
            IsEnabled ? Pool.Get<T>(Handle.Index) : null;

        /// <summary>Gets the component of type <typeparamref name="T"/> if the entity has one.</summary>
        public bool TryGetComponent<T>(out T componentOut) where T : class, IComponent {
            componentOut = GetComponentOf<T>();
            return componentOut != null;
        }

        /// <summary>
        /// Determine if this entity contains any components of the specified type.
        /// </summary>
        public bool HasComponent<T>() where T : IComponent => IsEnabled && Pool.Has<T>(Handle.Index);

        /// <summary>Returns a snapshot of this entity's components.</summary>
        public IEnumerable<IComponent> GetComponents() {
            var components = new List<IComponent>();
            if (!IsEnabled) return components;

            foreach (var store in Pool.Stores) {
                if (store == null || !store.Has(Handle.Index)) continue;
                Pool.AccessChecker?.Read(store.TypeId);
                components.Add(store.GetUntyped(Handle.Index));
            }

            return components;
        }

        /// <summary>
        /// Add a <seealso cref="IComponent"/> instance to this entity, stored under its runtime type.
        /// </summary>
        /// <exception cref="ComponentAlreadyRegisteredException">The entity already contains this Component</exception>
        /// <exception cref="InvalidOperationException">The entity has been released.</exception>
        /// <returns>This entity for further chaining</returns>
        public Entity AddComponent<T>(T component) where T : class, IComponent {
            ArgumentNullException.ThrowIfNull(component);
            ThrowIfReleased();

            if (component.GetType() == typeof(T)) {
                Pool.CheckWrite<T>();
                Pool.Store<T>().Add(this, component, notify: true);
            }
            else Pool.AddUntyped(this, component, notify: true);

            return this;
        }

        /// <summary>
        /// Remove a <seealso cref="IComponent"/> from this entity.
        /// </summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        /// <returns>This entity for further chaining</returns>
        public Entity RemoveComponent<T>() where T : class, IComponent {
            Pool.CheckWrite<T>();
            if (!IsEnabled || !Pool.Contains<T>(Handle.Index)) {
                throw new ComponentNotRegisteredException(this,
                    $"Component type '{typeof(T).Name}' is not registered");
            }

            Pool.Store<T>().Remove(this, notify: true);
            return this;
        }

        /// <summary>Removes every component from this entity.</summary>
        /// <param name="notify">Raise each store's <c>Removed</c> event.</param>
        public Entity RemoveAllComponents(bool notify = false) {
            if (!IsEnabled) return this;

            Pool.CheckWriteOfEach(Pool.Stores, Handle.Index);
            foreach (var store in Pool.Stores) {
                if (store != null && store.Has(Handle.Index)) store.Remove(this, notify);
            }

            return this;
        }

        /// <summary>Swaps the entity's <typeparamref name="T"/> for <paramref name="newComponentInstance"/>.</summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        public Entity ReplaceComponent<T>(T newComponentInstance) where T : class, IComponent {
            ArgumentNullException.ThrowIfNull(newComponentInstance);
            Pool.CheckWrite<T>();
            if (!IsEnabled || !Pool.Contains<T>(Handle.Index)) {
                throw new ComponentNotRegisteredException(this,
                    $"Component '{typeof(T).Name}' is not registered");
            }

            Pool.Store<T>().Replace(this, newComponentInstance);
            return this;
        }

        /// <summary>Adds the struct component <paramref name="component"/>, stored inline in its store.</summary>
        /// <exception cref="ComponentAlreadyRegisteredException">The entity already has a <typeparamref name="T"/>.</exception>
        /// <exception cref="InvalidOperationException">The entity has been released.</exception>
        /// <exception cref="ArgumentException">An order keyed by <typeparamref name="T"/> already holds its key.</exception>
        /// <returns>This entity for further chaining</returns>
        public Entity Add<T>(in T component) where T : struct, IComponent {
            ThrowIfReleased();
            Pool.CheckWrite<T>();
            Pool.StructStore<T>().Add(this, component, notify: true);
            return this;
        }

        /// <summary>
        /// The entity's <typeparamref name="T"/>, by reference, to read and change in place. It is valid until the next add or remove of a <typeparamref name="T"/> on any entity.
        /// </summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        /// <exception cref="InvalidOperationException"><typeparamref name="T"/> is an <see cref="IOrderKey"/>; change it with <see cref="Replace{T}"/>, which keeps its order in step.</exception>
        public ref T Ref<T>() where T : struct, IComponent {
            if (OrderKeyType<T>.IsKey)
                throw new InvalidOperationException($"'{typeof(T).Name}' orders entities, so it changes only through Replace, which moves the entity in its order.");
            Pool.AccessChecker?.Read(ComponentType<T>.Id);
            Pool.CheckWrite<T>();
            return ref StructSlot<T>();
        }

        /// <summary>The entity's <typeparamref name="T"/>, by read-only reference; valid until the next add or remove of a <typeparamref name="T"/> on any entity.</summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        public ref readonly T Read<T>() where T : struct, IComponent {
            Pool.AccessChecker?.Read(ComponentType<T>.Id);
            return ref StructSlot<T>();
        }

        /// <summary>Swaps the entity's <typeparamref name="T"/> for <paramref name="component"/>, raising <c>Replaced</c> and moving the entity in an order its key changed.</summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        /// <exception cref="ArgumentException">An order keyed by <typeparamref name="T"/> already holds the new key.</exception>
        public Entity Replace<T>(in T component) where T : struct, IComponent {
            Pool.CheckWrite<T>();
            if (!IsEnabled || !Pool.Contains<T>(Handle.Index))
                throw new ComponentNotRegisteredException(this, $"Component '{typeof(T).Name}' is not registered");
            Pool.StructStore<T>().Replace(this, component);
            return this;
        }

        /// <summary>Removes the entity's struct component <typeparamref name="T"/>.</summary>
        /// <exception cref="ComponentNotRegisteredException">The entity has no <typeparamref name="T"/>.</exception>
        /// <returns>This entity for further chaining</returns>
        public Entity Remove<T>() where T : struct, IComponent {
            Pool.CheckWrite<T>();
            if (!IsEnabled || !Pool.Contains<T>(Handle.Index))
                throw new ComponentNotRegisteredException(this, $"Component type '{typeof(T).Name}' is not registered");
            Pool.StructStore<T>().Remove(this, notify: true);
            return this;
        }

        private ref T StructSlot<T>() where T : struct, IComponent {
            if (!IsEnabled || !Pool.Contains<T>(Handle.Index))
                throw new ComponentNotRegisteredException(this, $"Component of type '{typeof(T).Name}' was not registered");
            return ref Pool.StructStore<T>().Ref(Handle.Index);
        }

        /// <summary>
        /// Release this entity and its components; its slot is reused by a later entity.
        /// </summary>
        /// <param name="notify">Raise each store's <c>Removed</c> event for the released components.</param>
        public void Destroy(bool notify = false) => Pool.Release(this, notify);

        /// <inheritdoc />
        public override string ToString() => $"Entity_{Handle}";

        private void ThrowIfReleased() {
            if (!IsEnabled) throw new InvalidOperationException($"Entity_{Handle} has been released.");
        }
    }
}
