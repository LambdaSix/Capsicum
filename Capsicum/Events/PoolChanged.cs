using System;

namespace Capsicum.Events {
    public class PoolChanged : EventArgs {
        public Pool Pool { get; set; }
        public Entity Entity { get; set; }

        public PoolChanged(Pool pool, Entity entity) {
            Pool = pool;
            Entity = entity;
        }
    }
}