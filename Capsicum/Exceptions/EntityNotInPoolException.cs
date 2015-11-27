using System;

namespace Capsicum.Exceptions {
    public class EntityNotInPoolException : Exception {
        public Entity Entity { get; set; }

        public EntityNotInPoolException(Entity entity, string message) : base(message) {
            Entity = entity;
        }
    }
}