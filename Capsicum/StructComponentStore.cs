#nullable enable
using System;
using System.Collections.Generic;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;

namespace Capsicum;

/// <summary>
/// Sparse-set storage for one struct component type. Components live inline in a dense array, so <see cref="Entity.Ref{T}"/> mutates one in place; lookups, adds and removes
/// are O(1) and allocation-free once the arrays have grown to fit.
/// </summary>
/// <remarks>
/// Removal swaps the last dense entry into the hole, so dense order depends on add/remove history; use an <see cref="EntityOrder"/> where iteration order must be stable.
/// A <c>ref</c> into the store is valid until the next add or remove of this type, which may move or grow the dense array.
/// </remarks>
public sealed class StructComponentStore<T> : ComponentStore where T : struct, IComponent {
    private const int InitialDenseCapacity = 4;

    // Slot index -> dense position + 1, so a zeroed array means "absent" without a fill pass on growth.
    private int[] _sparse = [];
    private int[] _indices = new int[InitialDenseCapacity];
    private T[] _dense = new T[InitialDenseCapacity];
    private readonly List<IStructComponentObserver<T>> _observers = [];

    /// <summary>Raised after a component is added through <see cref="Entity.Add{T}"/>.</summary>
    public event StructComponentEventHandler<T>? Added;

    /// <summary>Raised after a component is removed through <see cref="Entity.Remove{T}"/>, or on release when notification is requested.</summary>
    public event StructComponentEventHandler<T>? Removed;

    /// <summary>Raised after <see cref="Entity.Replace{T}"/> swaps the value in place.</summary>
    public event StructComponentReplacedHandler<T>? Replaced;

    /// <inheritdoc />
    public override Type ComponentType => typeof(T);

    internal override int TypeId => Capsicum.ComponentType<T>.Id;

    internal override bool Has(int index) => (uint)index < (uint)_sparse.Length && _sparse[index] != 0;

    internal override int IndexAt(int position) => _indices[position];

    internal override IComponent GetUntyped(int index) => Ref(index);

    internal override void AddUntyped(Entity entity, IComponent component, bool notify) => Add(entity, (T)component, notify);

    internal ref T Ref(int index) {
        var sparse = _sparse;
        if ((uint)index >= (uint)sparse.Length || sparse[index] == 0) throw new KeyNotFoundException($"No component of type '{typeof(T).Name}' at slot {index}.");
        return ref _dense[sparse[index] - 1];
    }

    internal void Observe(IStructComponentObserver<T> observer) => _observers.Add(observer);

    internal void Add(Entity entity, in T component, bool notify) {
        var index = entity.Handle.Index;
        if (Has(index))
            throw new ComponentAlreadyRegisteredException(entity, component, $"Component type '{typeof(T).Name}' is already registered");
        foreach (var observer in _observers) observer.Validate(default, component, adding: true);

        if (index >= _sparse.Length) Array.Resize(ref _sparse, Math.Max(index + 1, _sparse.Length * 2));
        if (Count == _dense.Length) {
            Array.Resize(ref _dense, _dense.Length * 2);
            Array.Resize(ref _indices, _indices.Length * 2);
        }

        _dense[Count] = component;
        _indices[Count] = index;
        Count++;
        _sparse[index] = Count;
        Version++;

        foreach (var observer in _observers) observer.Added(entity, component);
        if (notify) Added?.Invoke(entity, component);
    }

    internal override void Remove(Entity entity, bool notify) {
        var index = entity.Handle.Index;
        if (!Has(index)) throw new ComponentNotRegisteredException(entity, $"Component type '{typeof(T).Name}' is not registered");

        var position = _sparse[index] - 1;
        var removed = _dense[position];
        var last = Count - 1;
        if (position != last) {
            _dense[position] = _dense[last];
            _indices[position] = _indices[last];
            _sparse[_indices[position]] = position + 1;
        }

        _dense[last] = default;
        _sparse[index] = 0;
        Count = last;
        Version++;

        foreach (var observer in _observers) observer.Removed(entity, removed);
        if (notify) Removed?.Invoke(entity, removed);
    }

    internal void Replace(Entity entity, in T component) {
        ref var slot = ref Ref(entity.Handle.Index);
        var previous = slot;
        foreach (var observer in _observers) observer.Validate(previous, component, adding: false);
        slot = component;
        foreach (var observer in _observers) observer.Replaced(entity, previous, component);
        Replaced?.Invoke(entity, previous, component);
    }
}

/// <summary>Kept in step with a struct store on every add, remove and replace, whether or not the store's events are raised.</summary>
internal interface IStructComponentObserver<T> where T : struct, IComponent {
    /// <summary>Throws, before the store changes, when the component cannot be added or replace <paramref name="previous"/>.</summary>
    void Validate(in T previous, in T component, bool adding);

    void Added(Entity entity, in T component);

    void Removed(Entity entity, in T component);

    void Replaced(Entity entity, in T previous, in T current);
}
