using System;

namespace Capsicum.Exceptions
{
    public class ComponentNotRegisteredException : Exception
    {
        public Entity Entity { get; set; }

        public ComponentNotRegisteredException(Entity entity, string message) : base(message)
        {
            Entity = entity;
        }
    }
}