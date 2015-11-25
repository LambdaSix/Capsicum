using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Capsicum.Events;
using Capsicum.Exceptions;

namespace Capsicum {
    public partial class Pool {

        public event EventHandler<PoolChanged> OnEntityCreated = delegate { };
        public event EventHandler<PoolChanged> OnEntityRemoved = delegate { };
        public event EventHandler<PoolChanged> OnEntityRemoving = delegate { };

        public int Count { get { return _entities.Count; } }

        // TODO: Entity Groups

        private readonly HashSet<Entity> _entities = new HashSet<Entity>();
        private int _creationIndex;

        public Pool(int creationIndex = 0) {
            _creationIndex = creationIndex;
        }

        public virtual Entity CreateEntity() {
            var entity = new Entity()
            {
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

            OnEntityRemoving.Invoke(this, new PoolChanged(this, entity));
            
            entity.Destroy();

            OnEntityRemoved.Invoke(this, new PoolChanged(this, entity));

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

        public virtual IEnumerable<Entity> GetAllEntities() {
            return _entities;
        }
    }
}