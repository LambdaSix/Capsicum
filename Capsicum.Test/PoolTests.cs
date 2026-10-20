using System;
using System.Collections.Generic;
using System.Linq;
using Capsicum.Exceptions;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture()]
    public class PoolTests {
        // Test entitys can be created in the pool
        [Test]
        public void CreateEntitiesInPool() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity();
            var entity1 = pool.CreateEntity();

            Assert.That(pool.Count, Is.EqualTo(2));
            Assert.That(entity0, Is.Not.Null);
            Assert.That(entity1, Is.Not.Null);
        }
        
        // Test that entities can be removed from the pool
        [Test]
        public void RemoveEntitiesFromPool() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity(new []{new FakeComponent()});
            var entity1 = pool.CreateEntity(new[] { new FakeComponent() });

            Assert.That(pool.Count, Is.EqualTo(2));
            Assert.That(entity0, Is.Not.Null);
            Assert.That(entity1, Is.Not.Null);

            pool.RemoveEntity(entity0);

            Assert.That(pool.Count, Is.EqualTo(1));
            Assert.That(entity0, Is.Not.Null); // The entity should be released from the pool and reset, not free()'ed
            Assert.That(entity1, Is.Not.Null);

            entity0.TryGetComponent<FakeComponent>(out var component);
            Assert.That(component, Is.Null); // But it's components should be released
        }

        [Test]
        public void CheckReleaseChain() {
            var pool = new Pool();
            var component = new FakeComponent();
            var entity0 = pool.CreateEntity(new[] {component});

            var order = new List<string>();
            pool.OnEntityRemoving += (sender, changed) => order.Add("removing");
            pool.Components<FakeComponent>().Removed += (entity, removed) => {
                Assert.That(entity, Is.SameAs(entity0));
                Assert.That(removed, Is.SameAs(component));
                order.Add("component");
            };
            pool.OnEntityRemoved += (sender, changed) => order.Add("removed");

            // Opt in to component removal events, which release normally skips.
            pool.RemoveEntity(entity0, true);

            Assert.That(order, Is.EqualTo(new[] {"removing", "component", "removed"}));
        }

        [Test]
        public void ReleaseSkipsComponentEventsUnlessRequested() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity(new[] {new FakeComponent()});
            var dispatched = 0;
            pool.Components<FakeComponent>().Removed += (_, _) => dispatched++;

            pool.RemoveEntity(entity0);

            Assert.That(entity0.IsEnabled, Is.False);
            Assert.That(dispatched, Is.EqualTo(0));
        }

        [Test]
        public void RemoveAllEntitiesReleasesEveryListedEntity() {
            var pool = new Pool();
            var entities = new[] {pool.CreateEntity(), pool.CreateEntity(), pool.CreateEntity()};
            Assert.That(pool.Count, Is.EqualTo(3));

            pool.RemoveAllEntities();

            Assert.That(pool.Count, Is.EqualTo(0));
            Assert.That(entities.Select(e => e.IsEnabled), Has.All.False);
        }

        [Test]
        public void UnlistedEntitiesAreNotEnumerated() {
            var pool = new Pool();
            var listed = pool.CreateEntity(new[] {new FakeComponent()});
            var unlisted = pool.CreateEntity(new[] {new FakeComponent()}, addToPool: false);

            Assert.That(pool.ToList(), Is.EqualTo(new[] {listed}));
            Assert.That(unlisted.HasComponent<FakeComponent>(), Is.True);
            Assert.That(pool.HasEntity(unlisted), Is.False);
            Assert.Throws<EntityNotInPoolException>(() => pool.RemoveEntity(unlisted));
        }

        [Test]
        public void EnumerationThrowsWhenThePoolChanges() {
            var pool = new Pool();
            pool.CreateEntity();
            pool.CreateEntity();

            Assert.Throws<InvalidOperationException>(() => {
                foreach (var entity in pool) pool.CreateEntity();
            });
        }

        [Test]
        public void EnumeratesInHandleIndexOrder() {
            var pool = new Pool();
            var a = pool.CreateEntity();
            var b = pool.CreateEntity();
            var c = pool.CreateEntity();
            pool.RemoveEntity(a);
            var d = pool.CreateEntity();

            Assert.That(d.Handle.Index, Is.EqualTo(a.Handle.Index));
            Assert.That(pool.ToList(), Is.EqualTo(new[] {d, b, c}));
        }
    }
}