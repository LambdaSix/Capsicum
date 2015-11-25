﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    public class Entity : IDisposable {
        public event EventHandler<EntityChanged> OnComponentAdded;
        public event EventHandler<EntityChanged> OnComponentRemoved;
        public event EventHandler<ComponentReplaced> OnComponentReplaced;

        private readonly HashSet<IComponent> _components = new HashSet<IComponent>();
        private string _stringCache;

        public bool IsEnabled { get; internal set; }
        public int CreationIndex { get; internal set; }

        /// <summary>
        /// Returns the <seealso cref="IComponent"/> of the requested type.
        /// </summary>
        /// <typeparam name="T">Type of Component to look for</typeparam>
        /// <returns>The instance of Component stored if it was found</returns>
        public IComponent GetComponent<T>() where T : class, IComponent {
            var component = _components.OfType<T>().SingleOrDefault();
            if (component == null) {
                throw new ComponentNotRegisteredException(this, "Component of type '{0}' was not registered");
            }

            return component;
        }

        public bool TryGetComponent<T>(out IComponent componentOut) where T : class, IComponent {
            componentOut = _components.OfType<T>().SingleOrDefault();
            return componentOut != null;
        }

        /// <summary>
        /// Add a <seealso cref="IComponent"/> instance to this entity.
        /// </summary>
        /// <exception cref="ComponentAlreadyRegisteredException">The entity already contains this Component</exception>
        /// <typeparam name="T">Type of the component</typeparam>
        /// <param name="component">Component instance</param>
        /// <returns>This entity for further chaining</returns>
        public Entity AddComponent<T>(T component) where T : class, IComponent {
            if (HasComponent<T>()) {
                throw new ComponentAlreadyRegisteredException(this, component,
                    String.Format("Component type '{0}' is already registered", typeof (T).Name));
            }

            _components.Add(component);
            OnComponentAdded.Invoke(this, new EntityChanged(this, component));

            return this;
        }

        /// <summary>
        /// Remove a <seealso cref="IComponent"/> from this entity.
        /// </summary>
        /// <typeparam name="T">Component type to remove</typeparam>
        /// <returns>This entity for further chaining</returns>
        public Entity RemoveComponent<T>() where T : class, IComponent {
            if (HasComponent<T>()) {
                Debug.Assert(_components.SingleOrDefault(s => s.GetType() == typeof (T)) != null,
                    String.Format("More than one component of type '{0}' registered to Entity", typeof (T).Name));

                // This is sorta weird but should work?
                _components.RemoveWhere(s => {
                    if (s.GetType() == typeof (T)) {
                        OnComponentRemoved.Invoke(this, new EntityChanged(this, s));
                        return true;
                    }
                    return false;
                });
            }
            else {
                throw new ComponentNotRegisteredException(this,
                    String.Format("Component type '{0}' ios not registered", typeof (T).Name));
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
                RemoveComponent<T>().AddComponent(newComponentInstance);
                OnComponentReplaced.Invoke(this, new ComponentReplaced(this, oldComponent, newComponentInstance));
            }
            else {
                throw new ComponentNotRegisteredException(this,
                    String.Format("Component '{0}' is not registered", typeof (T).Name));
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
            return CreationIndex;
        }

        private bool _disposed;

        ~Entity() {
            Dispose(false);
        }

        public void Dispose() {
            Dispose(true);
        }

        private void Dispose(bool dispose) {
            if (dispose) {
                if (_disposed) {
                    RemoveAllComponents();
                    _disposed = true;
                }
            }
            else {
                // No-unmanaged resources to flush.
            }
        }
    }
}