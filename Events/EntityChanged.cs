using System;
using Capsicum.Interfaces;

namespace Capsicum.Events {
    public class EntityChanged : EventArgs {
        public Entity Entity { get; set; }
        public IComponent Component { get; set; }

        public EntityChanged(Entity entity, IComponent component) {
            Entity = entity;
            Component = component;
        }
    }
}