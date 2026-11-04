#nullable enable
using Capsicum.Interfaces;

namespace Capsicum;

/// <summary>Whether <typeparamref name="T"/> keys an <see cref="EntityOrder"/>, worked out once per type.</summary>
internal static class OrderKeyType<T> {
    internal static readonly bool IsKey = typeof(IOrderKey).IsAssignableFrom(typeof(T));
}
