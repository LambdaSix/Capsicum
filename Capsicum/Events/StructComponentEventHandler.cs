#nullable enable
using Capsicum.Interfaces;

namespace Capsicum.Events;

/// <summary>Handles a struct component of type <typeparamref name="T"/> being added to or removed from an entity.</summary>
public delegate void StructComponentEventHandler<T>(Entity entity, in T component) where T : struct, IComponent;
