using System;
using System.Runtime.CompilerServices;
using Capsicum.Interfaces;

namespace Capsicum {
    /// <summary>
    /// Selects a pool's entities by the component types they have and lack. Building and enumerating a query
    /// allocates nothing.
    /// </summary>
    /// <remarks>
    /// Enumeration walks the smallest <c>With</c> store, or every slot when there is none, so order follows storage
    /// history; use <see cref="Pool.QueryInto"/> for a stable order. Adding or removing a component type the query
    /// names, or creating or releasing entities in a query with no <c>With</c> clause, invalidates the enumerator.
    /// </remarks>
    public struct Query {
        /// <summary>Maximum number of <c>With</c> clauses, and separately of <c>Without</c> clauses.</summary>
        public const int MaxClauses = 4;

        private readonly Pool _pool;
        private StoreSet _with;
        private StoreSet _without;
        private int _withCount;
        private int _withoutCount;

        internal Query(Pool pool) {
            _pool = pool;
            _with = default;
            _without = default;
            _withCount = 0;
            _withoutCount = 0;
        }

        /// <summary>Requires matched entities to have a <typeparamref name="T"/>.</summary>
        /// <exception cref="InvalidOperationException">The query already has <see cref="MaxClauses"/> <c>With</c> clauses.</exception>
        public Query With<T>() where T : class, IComponent {
            if (_withCount == MaxClauses)
                throw new InvalidOperationException($"A query takes at most {MaxClauses} With clauses.");

            var next = this;
            next._with[next._withCount++] = _pool.Components<T>();
            return next;
        }

        /// <summary>Requires matched entities not to have a <typeparamref name="T"/>.</summary>
        /// <exception cref="InvalidOperationException">The query already has <see cref="MaxClauses"/> <c>Without</c> clauses.</exception>
        public Query Without<T>() where T : class, IComponent {
            if (_withoutCount == MaxClauses)
                throw new InvalidOperationException($"A query takes at most {MaxClauses} Without clauses.");

            var next = this;
            next._without[next._withoutCount++] = _pool.Components<T>();
            return next;
        }

        internal Pool Pool => _pool;

        /// <summary>Returns an allocation-free enumerator over the matching entities.</summary>
        public Enumerator GetEnumerator() => new Enumerator(this);

        private bool Matches(int index) {
            if (!_pool.IsListed(index)) return false;
            for (var i = 0; i < _withCount; i++) {
                if (!_with[i].Has(index)) return false;
            }

            for (var i = 0; i < _withoutCount; i++) {
                if (_without[i].Has(index)) return false;
            }

            return true;
        }

        [InlineArray(MaxClauses)]
        private struct StoreSet {
            private ComponentStore _element;
        }

        [InlineArray(MaxClauses * 2)]
        private struct VersionSet {
            private int _element;
        }

        /// <summary>Enumerates the entities matched by a <see cref="Query"/>.</summary>
        public struct Enumerator {
            private Query _query;
            private VersionSet _versions;
            private readonly ComponentStore _driver;
            private readonly int _slotVersion;
            private int _position;

            internal Enumerator(Query query) {
                _query = query;
                _versions = default;
                _driver = null;
                _slotVersion = query._pool.SlotVersion;
                _position = 0;
                Current = null;

                for (var i = 0; i < query._withCount; i++) {
                    var store = query._with[i];
                    _versions[i] = store.Version;
                    if (_driver == null || store.Count < _driver.Count) _driver = store;
                }

                for (var i = 0; i < query._withoutCount; i++) {
                    _versions[MaxClauses + i] = query._without[i].Version;
                }

                if (_driver != null) _position = -1;
            }

            /// <summary>The entity at the current position.</summary>
            public Entity Current { get; private set; }

            /// <summary>Advances to the next matching entity.</summary>
            /// <exception cref="InvalidOperationException">A store or the pool changed shape since enumeration began.</exception>
            public bool MoveNext() {
                ThrowIfChanged();
                var pool = _query._pool;

                if (_driver != null) {
                    while (++_position < _driver.Count) {
                        var index = _driver.IndexAt(_position);
                        if (_query.Matches(index)) {
                            Current = pool.SlotAt(index);
                            return true;
                        }
                    }
                }
                else {
                    while (++_position < pool.SlotLimit) {
                        if (_query.Matches(_position)) {
                            Current = pool.SlotAt(_position);
                            return true;
                        }
                    }
                }

                Current = null;
                return false;
            }

            private void ThrowIfChanged() {
                for (var i = 0; i < _query._withCount; i++) {
                    if (_query._with[i].Version != _versions[i]) ThrowChanged(_query._with[i].ComponentType.Name);
                }

                for (var i = 0; i < _query._withoutCount; i++) {
                    if (_query._without[i].Version != _versions[MaxClauses + i]) ThrowChanged(_query._without[i].ComponentType.Name);
                }

                if (_driver == null && _query._pool.SlotVersion != _slotVersion) ThrowChanged("entity");
            }

            private static void ThrowChanged(string what) =>
                throw new InvalidOperationException(
                    $"The {what} store changed during query enumeration; collect matches with Pool.QueryInto before changing them.");
        }
    }
}
