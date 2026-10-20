using Capsicum.Interfaces;

namespace Capsicum.Events {
    /// <summary>Handles an entity's component of type <typeparamref name="T"/> being replaced in place.</summary>
    public delegate void ComponentReplacedHandler<in T>(Entity entity, T previous, T current) where T : class, IComponent;
}
