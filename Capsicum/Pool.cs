using System;
using System.Collections.Generic;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum {
    public partial class Pool {

        public event EventHandler<PoolChanged> OnEntityCreated = delegate { };
        public event EventHandler<PoolChanged> OnEntityRemoved = delegate { };
        public event EventHandler<PoolChanged> OnEntityRemoving = delegate { };

        public int Count => _entities.Count;

        // TODO: Entity Groups
        private IDictionary<string, Group> GroupQueries { get; set; }

        // TODO: Check if this can just be an ObservableCollection, a lot of group operations search the entire list anyway
        private readonly ObservableHashSet<Entity> _entities = new ObservableHashSet<Entity>();
        private int _creationIndex;

        public Pool(int creationIndex = 0) {
            _creationIndex = creationIndex;
            GroupQueries = new Dictionary<string, Group>();
        }

        public virtual Entity CreateEntity() {
            // It *might* be worth keeping a list of entitys we can reuse in a graveyard.
            // but performance testing to see if that's worth it is needed.

            var entity = new Entity() {
                CreationIndex = _creationIndex,
                IsEnabled = true
            };

            _entities.Add(entity);
            OnEntityCreated.Invoke(this, new PoolChanged(this, entity));

            _creationIndex++;
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

            // Remove the item from memory
            entity.Dispose();
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
                throw new GroupAlreadyExistsException(String.Format("The group '{0}' is already registered", name));
            }

            GroupQueries.Add(name, new Group(_entities, query));
        }

        public virtual Group GetGroup(string name) {
            Group value;
            if (GroupQueries.TryGetValue(name, out value))
                return value;

            throw new GroupNotRegisteredException(String.Format("The group '{0}' is not registered", name));
        }

        public virtual bool TryGetGroup(string name, out Group groupOut) {
            return GroupQueries.TryGetValue(name, out groupOut);
        }

        public virtual IEnumerable<Entity> InvokeGroup(string name) {
            Group value;
            if (GroupQueries.TryGetValue(name, out value))
                return value.Invoke();

            throw new GroupNotRegisteredException(String.Format("The group '{0}' is not registered", name));
        }

        public virtual IEnumerable<Entity> GetAllEntities() {
            return _entities;
        }
    }

    public class FakeComponent : IComponent {
        public string Name { get; set; }
    }
}