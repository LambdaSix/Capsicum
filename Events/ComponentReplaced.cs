using System;
using Capsicum.Interfaces;

namespace Capsicum.Events
{
    public class ComponentReplaced : EventArgs
    {
        public Entity Entity { get; set; }
        public IComponent PreviousComponent { get; set; }
        public IComponent NewComponent { get; set; }

        public ComponentReplaced(Entity entity, IComponent previousComponent, IComponent newComponent)
        {
            Entity = entity;
            PreviousComponent = previousComponent;
            NewComponent = newComponent;
        }
    }
}