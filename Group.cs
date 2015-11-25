using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Capsicum.Events;

namespace Capsicum {
    /// <summary>
    /// Provides a way to manage a group of entitys.
    /// </summary>
    public class Group {
        public event EventHandler<GroupChanged> OnEntityAdded = delegate { };
        public event EventHandler<GroupChanged> OnEntityRemoved = delegate { };
        public event EventHandler<GroupChanged> OnEntityReplaced = delegate { }; 

        internal Func<IEnumerable<Entity>, IEnumerable<Entity>> QueryExpression{ get; set; }

        private List<Entity> _groupCache;
        /// <summary>
        /// The entities in the pool that created this instance.
        /// </summary>
        private readonly ObservableHashSet<Entity> _entities;

        public Group() {}

        public Group(ObservableHashSet<Entity> collection, Func<IEnumerable<Entity>, IEnumerable<Entity>> query) {
            _entities = collection;
            QueryExpression = query;

            // This is intended to invalidate the internal cache only if changes occur that we care about, that is we would
            // have included the item in our group if Invoke() was called for the first time.
            _entities.CollectionChanged += (sender, args) => {
                switch (args.Action) {
                    // We want to raise the appropriate event for the right collection change, but also always invalidate
                    // the cache for any change.
                    case NotifyCollectionChangedAction.Add:
                        if (QueryExpression.Invoke(args.NewItems.Cast<Entity>()).Any()) {
                            OnEntityAdded.Invoke(this, new GroupChanged(this, args.NewItems.Cast<Entity>()));
                            InvalidateCache();
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (QueryExpression.Invoke(args.OldItems.Cast<Entity>()).Any()) {
                            OnEntityRemoved.Invoke(this, new GroupChanged(this, args.OldItems.Cast<Entity>()));
                            InvalidateCache();
                        }
                        break;
                    // Move is special, we literally don't care if an item moved it's index around, it's still there.
                    case NotifyCollectionChangedAction.Move:
                        break;

                    // Replace may have changed one of our items
                    case NotifyCollectionChangedAction.Replace:
                        if (QueryExpression.Invoke(args.NewItems.Cast<Entity>()).Any()) {
                            OnEntityReplaced.Invoke(this, new GroupChanged(this, args.NewItems.Cast<Entity>()));
                            InvalidateCache();
                        }
                        break;
                    // Nothing is valid for a Reset, just invalidate and try again later.
                    case NotifyCollectionChangedAction.Reset:
                        InvalidateCache();
                        break;
                }
            };
        }

        /// <summary>
        /// Retrieve the objects this group contains
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Entity> Invoke() => _groupCache ?? (_groupCache = QueryExpression.Invoke(_entities).ToList());

        /// <summary>
        /// Invalidate the internal cache for this group
        /// </summary>
        internal void InvalidateCache() => _groupCache = null;
    }
}