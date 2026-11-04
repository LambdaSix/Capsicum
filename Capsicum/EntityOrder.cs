#nullable enable
using System;
using Capsicum.Interfaces;

namespace Capsicum;

/// <summary>
/// The entities holding one <see cref="IOrderKey"/> struct component, kept in ascending key order as components are added, removed and replaced, so walking them never
/// sorts. Keys are unique: the order is a pure function of the keys, whatever the history of adds and removes.
/// </summary>
/// <remarks>
/// An add with the highest key so far appends; any other add or a changed key is placed by binary search. A remove keeps the order of the rest. A bulk load (a restore)
/// appends without placing and sorts once when it ends; a duplicate key is refused before anything changes, or by the end of a bulk load, which leaves the order sorted but
/// holding the duplicate, so a load that fails it must be abandoned. Walking skips entities the pool does not list. Changing the order or the pool's entities while walking
/// it throws.
/// </remarks>
public sealed class EntityOrder {
    private const int InitialCapacity = 16;

    private readonly Pool _pool;
    private int[] _indices = new int[InitialCapacity];
    private long[] _keys = new long[InitialCapacity];
    private int _count;
    private bool _bulk;

    internal EntityOrder(Pool pool) => _pool = pool;

    /// <summary>Entities in the order, listed or not.</summary>
    public int Count => _count;

    /// <summary>Incremented by every change to the order.</summary>
    public int Version { get; private set; }

    /// <summary>True between <see cref="BeginBulkLoad"/> and the end of the load.</summary>
    public bool IsBulkLoading => _bulk;

    internal Pool Pool => _pool;

    /// <summary>
    /// Starts a bulk load: until the returned scope is disposed, adds append unsorted, and the order is sorted once at the end. Use it around a restore that creates
    /// entities in any key order.
    /// </summary>
    /// <exception cref="InvalidOperationException">A bulk load is already in progress.</exception>
    public BulkLoad BeginBulkLoad() {
        if (_bulk) throw new InvalidOperationException("A bulk load is already in progress.");
        _bulk = true;
        return new BulkLoad(this);
    }

    /// <summary>Walks the listed entities in ascending key order.</summary>
    /// <exception cref="InvalidOperationException">A bulk load is in progress.</exception>
    public Enumerator GetEnumerator() => new(this, default, filtered: false);

    /// <summary>Walks the listed entities that match <paramref name="query"/>, in ascending key order.</summary>
    /// <exception cref="ArgumentException">The query belongs to another pool.</exception>
    public Ordered Where(Query query) {
        if (query.Pool != _pool) throw new ArgumentException("The query belongs to another pool.", nameof(query));
        return new Ordered(this, query);
    }

    internal int IndexAt(int position) => _indices[position];

    /// <summary>Throws when an entity already holds <paramref name="key"/>; during a bulk load the end of the load checks instead.</summary>
    internal void RequireFree(long key) {
        if (!_bulk && Array.BinarySearch(_keys, 0, _count, key) >= 0) throw DuplicateKey(key);
    }

    internal void Insert(int index, long key) {
        if (_count == _indices.Length) {
            Array.Resize(ref _indices, _indices.Length * 2);
            Array.Resize(ref _keys, _keys.Length * 2);
        }
        if (_bulk || _count == 0 || key > _keys[_count - 1]) {
            _indices[_count] = index;
            _keys[_count] = key;
        } else {
            var position = Array.BinarySearch(_keys, 0, _count, key);
            if (position >= 0) throw DuplicateKey(key);
            position = ~position;
            Array.Copy(_indices, position, _indices, position + 1, _count - position);
            Array.Copy(_keys, position, _keys, position + 1, _count - position);
            _indices[position] = index;
            _keys[position] = key;
        }
        _count++;
        Version++;
    }

    internal void Remove(int index, long key) {
        var position = Find(index, key);
        Array.Copy(_indices, position + 1, _indices, position, _count - position - 1);
        Array.Copy(_keys, position + 1, _keys, position, _count - position - 1);
        _count--;
        Version++;
    }

    internal void Move(int index, long previous, long current) {
        if (previous == current) return;
        Remove(index, previous);
        Insert(index, current);
    }

    private int Find(int index, long key) {
        if (!_bulk) {
            var found = Array.BinarySearch(_keys, 0, _count, key);
            if (found >= 0 && _indices[found] == index) return found;
        } else {
            for (var position = 0; position < _count; position++) {
                if (_keys[position] == key && _indices[position] == index) return position;
            }
        }
        throw new InvalidOperationException($"Slot {index} with key {key} is not in the order.");
    }

    private void EndBulkLoad() {
        _bulk = false;
        Array.Sort(_keys, _indices, 0, _count);
        for (var position = 1; position < _count; position++) {
            if (_keys[position] == _keys[position - 1]) throw DuplicateKey(_keys[position]);
        }
        Version++;
    }

    private static ArgumentException DuplicateKey(long key) =>
        new($"Another entity already holds order key {key}; keys in an order are unique.");

    /// <summary>The scope of a bulk load; disposing it sorts the order once.</summary>
    public readonly struct BulkLoad : IDisposable {
        private readonly EntityOrder _order;

        internal BulkLoad(EntityOrder order) => _order = order;

        /// <summary>Ends the load and sorts the order.</summary>
        /// <exception cref="ArgumentException">Two entities hold the same key.</exception>
        public void Dispose() {
            if (_order is { IsBulkLoading: true }) _order.EndBulkLoad();
        }
    }

    /// <summary>The listed entities of an order that match a query.</summary>
    public readonly struct Ordered {
        private readonly EntityOrder _order;
        private readonly Query _query;

        internal Ordered(EntityOrder order, Query query) => (_order, _query) = (order, query);

        /// <summary>Walks the matches in ascending key order.</summary>
        public Enumerator GetEnumerator() => new(_order, _query, filtered: true);
    }

    /// <summary>Walks an order without allocating.</summary>
    public struct Enumerator {
        private readonly EntityOrder _order;
        private readonly Query _query;
        private readonly bool _filtered;
        private readonly int _version;
        private readonly int _slotVersion;
        private Query.Guard _guard;
        private int _position;

        internal Enumerator(EntityOrder order, Query query, bool filtered) {
            if (order._bulk) throw new InvalidOperationException("The order is being bulk loaded; walk it once the load has ended.");
            _order = order;
            _query = query;
            _filtered = filtered;
            _version = order.Version;
            _slotVersion = order._pool.SlotVersion;
            _guard = filtered ? query.CreateGuard() : default;
            _position = -1;
            Current = null!;
        }

        /// <summary>The entity at the current position.</summary>
        public Entity Current { get; private set; }

        /// <summary>Advances to the next listed, matching entity.</summary>
        /// <exception cref="InvalidOperationException">The order, the pool's entities or a store the query names changed since the walk began.</exception>
        public bool MoveNext() {
            if (_order.Version != _version || _order._pool.SlotVersion != _slotVersion)
                throw new InvalidOperationException("The order or the pool changed while it was walked; collect the entities first.");
            if (_filtered) _guard.ThrowIfChanged();
            var pool = _order._pool;
            while (++_position < _order._count) {
                var index = _order._indices[_position];
                if (_filtered ? !_query.Matches(index) : !pool.IsListed(index)) continue;
                Current = pool.SlotAt(index);
                return true;
            }
            Current = null!;
            return false;
        }
    }
}

/// <summary>Keeps an <see cref="EntityOrder"/> in step with the store of the struct component that keys it.</summary>
internal sealed class EntityOrderObserver<T>(EntityOrder order) : IStructComponentObserver<T> where T : struct, IComponent, IOrderKey {
    public void Validate(in T previous, in T component, bool adding) {
        if (adding || previous.OrderKey != component.OrderKey) order.RequireFree(component.OrderKey);
    }

    public void Added(Entity entity, in T component) => order.Insert(entity.Handle.Index, component.OrderKey);

    public void Removed(Entity entity, in T component) => order.Remove(entity.Handle.Index, component.OrderKey);

    public void Replaced(Entity entity, in T previous, in T current) => order.Move(entity.Handle.Index, previous.OrderKey, current.OrderKey);
}
