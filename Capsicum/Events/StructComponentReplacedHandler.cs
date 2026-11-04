#nullable enable
using Capsicum.Interfaces;

namespace Capsicum.Events;

/// <summary>Handles an entity's struct component of type <typeparamref name="T"/> being replaced, with the value before and after.</summary>
public delegate void StructComponentReplacedHandler<T>(Entity entity, in T previous, in T current) where T : struct, IComponent;
