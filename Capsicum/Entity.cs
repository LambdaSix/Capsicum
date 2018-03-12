using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    public partial class Entity {
        public event EventHandler<EntityChanged> OnComponentAdded = delegate { };
        public event EventHandler<EntityChanged> OnComponentRemoved = delegate { };
        public event EventHandler<ComponentReplaced> OnComponentReplaced = delegate { };

        public event EventHandler<EntityReleased> OnEntityReleased = delegate { };

        private readonly HashSet<IComponent> _components = new HashSet<IComponent>();
        private string _stringCache;

        public bool IsEnabled { get; internal set; }
        public Pool Pool { get; internal set; }
        private int? _creationIndex;

        internal int CreationIndex {
            get => _creationIndex.GetValueOrDefault(0);
            set { if (_creationIndex == null) _creationIndex = value; }
        }

        /// <summary>
        /// Returns the <seealso cref="IComponent"/> of the requested type.
        /// </summary>
        /// <typeparam name="T">Type of Component to look for</typeparam>
        /// <returns>The instance of Component stored if it was found</returns>
        public T GetComponent<T>() where T : class, IComponent {
            var component = _components.OfType<T>().SingleOrDefault();
            if (component == null) {
                throw new ComponentNotRegisteredException(this, "Component of type '{0}' was not registered");
            }

            return component;
        }

        /// <summary>
        /// Try and get the specified component from this Entity.
        /// </summary>
        /// <typeparam name="T">Type of component to look for</typeparam>
        /// <returns>The instance of the component if it was found, otherwise null</returns>
        public T GetComponentOf<T>() where T : class, IComponent
        {
            var component = _components.OfType<T>().SingleOrDefault();
            return component;
        }

        public bool TryGetComponent<T>(out T componentOut) where T : class, IComponent {
            componentOut = _components.OfType<T>().SingleOrDefault();
            return componentOut != null;
        }

        public IEnumerable<IComponent> GetComponents() => _components;

        /// <summary>
        /// Add a <seealso cref="IComponent"/> instance to this entity.
        /// </summary>
        /// <exception cref="ComponentAlreadyRegisteredException">The entity already contains this Component</exception>
        /// <typeparam name="T">Type of the component</typeparam>
        /// <param name="component">Component instance</param>
        /// <returns>This entity for further chaining</returns>
        public Entity AddComponent<T>(T component) where T : class, IComponent {
            return AddComponent(component, notify: true);
        }

        internal Entity AddComponent<T>(T component, bool notify) where T : class, IComponent {
            if (HasComponent<T>()) {
                throw new ComponentAlreadyRegisteredException(this, component,
                    $"Component type '{typeof (T).Name}' is already registered");
            }

            _components.Add(component);
            if (notify) OnComponentAdded.Invoke(this, new EntityChanged(this, component));

            return this;
        }

        /// <summary>
        /// Remove a <seealso cref="IComponent"/> from this entity.
        /// </summary>
        /// <typeparam name="T">Component type to remove</typeparam>
        /// <returns>This entity for further chaining</returns>
        public Entity RemoveComponent<T>() where T : class, IComponent {
            return RemoveComponent<T>(notify: true);
        }

        internal Entity RemoveComponent<T>(bool notify) where T : class, IComponent {
            if (HasComponent<T>()) {
                Debug.Assert(_components.SingleOrDefault(s => s.GetType() == typeof (T)) != null,
                    $"More than one component of type '{typeof (T).Name}' registered to Entity");

                // This is sorta weird but should work?
                _components.RemoveWhere(s => {
                    if (s.GetType() == typeof (T)) {
                        if (notify) OnComponentRemoved.Invoke(this, new EntityChanged(this, s));
                        return true;
                    }
                    return false;
                });
            }
            else {
                throw new ComponentNotRegisteredException(this,
                    $"Component type '{typeof (T).Name}' ios not registered");
            }

            return this;
        }

        public Entity RemoveAllComponents() {
            // For logical consistancy call the event. But maybe it should be optional?
            foreach (var component in _components) {
                OnComponentRemoved(this, new EntityChanged(this, component));
            }

            _components.Clear();
            return this;
        }

        public Entity ReplaceComponent<T>(T newComponentInstance) where T : class, IComponent {
            if (HasComponent<T>()) {
                var oldComponent = GetComponent<T>();
                RemoveComponent<T>(notify: false).AddComponent(newComponentInstance, notify: false);
                OnComponentReplaced.Invoke(this, new ComponentReplaced(this, oldComponent, newComponentInstance));
            }
            else {
                throw new ComponentNotRegisteredException(this,
                    $"Component '{typeof (T).Name}' is not registered");
            }

            return this;
        }

        /// <summary>
        /// Determine if this entity contains any components of the specified type.
        /// </summary>
        /// <returns></returns>
        public bool HasComponent<T>() where T : IComponent {
            return _components.Any(s => s.GetType() == typeof (T));
        }

        /// <summary>
        /// Free this entity for reuse.
        /// </summary>
        public void Destroy() {
            OnEntityReleased.Invoke(this, new EntityReleased(this));
            Pool?.MoveToGraveyard(this);

            RemoveAllComponents();

            // Unhook all the subscribers by attaching a blank delegate.
            OnComponentAdded = delegate { };
            OnComponentRemoved = delegate { };
            OnComponentReplaced = delegate { };

            IsEnabled = false;
        }

        public override string ToString() {
            if (_stringCache == null) {
                var sb = new StringBuilder();
                sb.Append("Entity_");
                sb.Append(CreationIndex);

                sb.Append("(");

                var componentStr = String.Join(", ", _components.Select(s => s.GetType()));
                sb.Append(componentStr);

                sb.Append(")");

                _stringCache = sb.ToString();
            }

            return _stringCache;
        }

        public override int GetHashCode() {
            // The creationIndex should stay the same for the lifetime of the object.
            return CreationIndex;
        }
    }
}