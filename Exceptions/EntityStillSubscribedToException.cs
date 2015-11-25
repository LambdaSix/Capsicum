using System;

namespace Capsicum.Exceptions {
    internal class EntityStillSubscribedToException : Exception {
        public Entity Entity { get; set; }

        public EntityStillSubscribedToException(Entity entity, string message) : base(message) {
            Entity = entity;
        }
    }
}