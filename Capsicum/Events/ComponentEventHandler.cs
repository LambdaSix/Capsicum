using Capsicum.Interfaces;

namespace Capsicum.Events {
    /// <summary>Handles a component of type <typeparamref name="T"/> being added to or removed from an entity.</summary>
    public delegate void ComponentEventHandler<in T>(Entity entity, T component) where T : class, IComponent;
}
