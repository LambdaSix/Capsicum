using System;
using System.Collections.Generic;

namespace Capsicum {
    /// <summary>
    /// Process-wide dense ids for component types, used to index each pool's store array.
    /// </summary>
    /// <remarks>
    /// Ids are handed out in first-use order, so they are stable within a process but not across runs. Nothing
    /// that must be deterministic between runs may be ordered by them.
    /// </remarks>
    internal static class ComponentType {
        private static readonly object Gate = new object();
        private static readonly Dictionary<Type, int> Ids = new Dictionary<Type, int>();

        internal static int IdOf(Type type) {
            lock (Gate) {
                if (!Ids.TryGetValue(type, out var id)) {
                    id = Ids.Count;
                    Ids.Add(type, id);
                }

                return id;
            }
        }
    }

    /// <summary>
    /// Caches <see cref="ComponentType.IdOf"/> per closed generic type, so typed lookups never take the lock.
    /// </summary>
    internal static class ComponentType<T> {
        internal static readonly int Id = ComponentType.IdOf(typeof(T));
    }
}
