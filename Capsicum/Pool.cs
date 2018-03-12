using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    public partial class Pool {

        public event EventHandler<PoolChanged> OnEntityCreated = delegate { };
        public event EventHandler<PoolChanged> OnEntityChanged = delegate { };
        public event EventHandler<PoolChanged> OnEntityRemoved = delegate { };
        public event EventHandler<PoolChanged> OnEntityRemoving = delegate { };

        public int Count => _entities.Count;

        // TODO: Entity Groups
        private IDictionary<string, Group> GroupQueries { get; set; }

        private readonly ObservableCollection<Entity> _entities;
        private readonly List<Entity> _graveyardEntities;
        private int _creationIndex;

        public Pool(int capacity = 2048, int creationIndex = 0) {
            _creationIndex = creationIndex;
            GroupQueries = new Dictionary<string, Group>();

            _entities = new ObservableCollection<Entity>();
            _graveyardEntities = new List<Entity>(capacity / 2);
        }

        /// <summary>
        /// Create a new entity in this pool
        /// </summary>
        /// <returns></returns>
        public virtual Entity CreateEntity() {
            Entity entity = null;

            for (int i = 0; i < _graveyardEntities.Count; i++)
            {
                if (!_graveyardEntities[i].IsEnabled)
                {
                    // Found a free entity, resurrect it for reuse.
                    entity = _entities[i];
                    entity.IsEnabled = true;
                    entity.Pool = this;

                    _graveyardEntities.Remove(entity);
                }
            }

            // Didn't find an entity to recycle
            if (entity == null)
            {
                entity = new Entity()
                {
                    CreationIndex = _creationIndex,
                    IsEnabled = true,
                    Pool = this
                };
                _creationIndex++;
            }

            // Add the entity back to the active entity list
            _entities.Add(entity);
            if (addToPool)
                _entities.Add(entity);

            entity.OnComponentAdded += (sender, changed) => OnEntityChanged.Invoke(this, new PoolChanged(this, entity));
            entity.OnComponentRemoved += (sender, changed) => OnEntityChanged.Invoke(this, new PoolChanged(this, entity));
            entity.OnComponentReplaced += (sender, changed) => OnEntityChanged.Invoke(this, new PoolChanged(this, entity));

            OnEntityCreated.Invoke(this, new PoolChanged(this, entity));
            return entity;
        }

            return entity;
        }

        public virtual void RemoveEntity(Entity entity) {
            var removed = _entities.Remove(entity);

            if (!removed)
                throw new EntityNotInPoolException(entity, "Could not remove entity");

            // Inform subscribers the entity is going to be removed
            OnEntityRemoving.Invoke(this, new PoolChanged(this, entity));

            // Prepare it for removal
            entity.Destroy();

            // Inform subscribers the entity has been removed
            OnEntityRemoved.Invoke(this, new PoolChanged(this, entity));
        }

        public virtual void RemoveAllEntities() {
            foreach (var entity in _entities) {
                RemoveEntity(entity);
            }
        }

        public virtual bool HasEntity(Entity entity) {
            return _entities.Contains(entity);
        }

        public virtual void RegisterGroup(string name, Func<IEnumerable<Entity>, IEnumerable<Entity>> query) {
            if (GroupQueries.ContainsKey(name)) {
                throw new GroupAlreadyExistsException($"The group '{name}' is already registered");
            }

            GroupQueries.Add(name, new Group(this, _entities, query));
        }

        public virtual Group GetGroup(string name) {
            Group value;
            if (GroupQueries.TryGetValue(name, out value))
                return value;

            throw new GroupNotRegisteredException($"The group '{name}' is not registered");
        }

        public virtual bool TryGetGroup(string name, out Group groupOut) {
            return GroupQueries.TryGetValue(name, out groupOut);
        }

        public virtual IEnumerable<Entity> InvokeGroup(string name) {
            Group value;
            if (GroupQueries.TryGetValue(name, out value))
                return value.Invoke();

            throw new GroupNotRegisteredException($"The group '{name}' is not registered");
        }

        public virtual void MoveToGraveyard(Entity entity)
        {
            _entities.Remove(entity);
            _graveyardEntities.Add(entity);
        }

        public virtual IEnumerable<Entity> GetAllEntities() {
            return _entities;
        }
    }
}
