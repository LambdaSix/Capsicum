using System;

namespace Capsicum.Events {
    public class EntityReleased : EventArgs {
        public Entity Entity { get; set; }

        public EntityReleased(Entity entity) {
            Entity = entity;
        }
    }
}