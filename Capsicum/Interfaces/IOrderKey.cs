#nullable enable
namespace Capsicum.Interfaces;

/// <summary>A struct component that orders the entities holding it, such as a spawn serial. An <see cref="EntityOrder"/> walks them by ascending key.</summary>
public interface IOrderKey {
    /// <summary>The entity's place in the order; equal keys keep the order they were added in.</summary>
    long OrderKey { get; }
}
