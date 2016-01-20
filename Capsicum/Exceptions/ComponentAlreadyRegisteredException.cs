using System;
using Capsicum.Interfaces;

namespace Capsicum.Exceptions {
    public class ComponentAlreadyRegisteredException : Exception {
        public Entity Entity { get; set; }
        public IComponent Component { get; set; }

        public ComponentAlreadyRegisteredException(Entity entity, IComponent component, string message) : base(message) {
            Entity = entity;
            Component = component;
        }
    }
}