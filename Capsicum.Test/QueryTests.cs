using System;
using System.Collections.Generic;
using System.Linq;
using Capsicum.Interfaces;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture]
    public class QueryTests {
        private static List<Entity> Run(Query query) {
            var matches = new List<Entity>();
            foreach (var entity in query) matches.Add(entity);
            return matches;
        }

        [Test]
        public void MatchesEntitiesWithEveryRequiredComponent() {
            var pool = new Pool();
            var both = pool.CreateEntity(new IComponent[] {new FakeComponent(), new OtherComponent()});
            pool.CreateEntity(new IComponent[] {new FakeComponent()});
            pool.CreateEntity(new IComponent[] {new OtherComponent()});

            var matches = Run(pool.Query().With<FakeComponent>().With<OtherComponent>());

            Assert.That(matches, Is.EqualTo(new[] {both}));
        }

        [Test]
        public void ExcludesEntitiesWithAnExcludedComponent() {
            var pool = new Pool();
            var fakeOnly = pool.CreateEntity(new IComponent[] {new FakeComponent()});
            pool.CreateEntity(new IComponent[] {new FakeComponent(), new OtherComponent()});

            var matches = Run(pool.Query().With<FakeComponent>().Without<OtherComponent>());

            Assert.That(matches, Is.EqualTo(new[] {fakeOnly}));
        }

        [Test]
        public void QueryWithNoRequiredComponentWalksEveryListedEntity() {
            var pool = new Pool();
            var bare = pool.CreateEntity();
            var other = pool.CreateEntity(new IComponent[] {new OtherComponent()});
            pool.CreateEntity(new IComponent[] {new FakeComponent()});
            pool.CreateEntity(addToPool: false);

            var matches = Run(pool.Query().Without<FakeComponent>());

            Assert.That(matches, Is.EqualTo(new[] {bare, other}));
        }

        [Test]
        public void UnlistedEntitiesAreNotMatched() {
            var pool = new Pool();
            var listed = pool.CreateEntity(new IComponent[] {new FakeComponent()});
            pool.CreateEntity(new IComponent[] {new FakeComponent()}, addToPool: false);

            Assert.That(Run(pool.Query().With<FakeComponent>()), Is.EqualTo(new[] {listed}));
        }

        [Test]
        public void ResultIsIndependentOfWhichStoreIsSmallest() {
            var pool = new Pool();
            var expected = new List<Entity>();
            for (var i = 0; i < 6; i++) {
                var entity = pool.CreateEntity(new IComponent[] {new FakeComponent()});
                if (i % 2 == 0) {
                    entity.AddComponent(new OtherComponent());
                    expected.Add(entity);
                }
            }

            var buffer = new List<Entity>();
            pool.QueryInto(pool.Query().With<FakeComponent>().With<OtherComponent>(), buffer);
            var reversed = new List<Entity>();
            pool.QueryInto(pool.Query().With<OtherComponent>().With<FakeComponent>(), reversed);

            Assert.That(buffer, Is.EqualTo(expected));
            Assert.That(reversed, Is.EqualTo(expected));
        }

        [Test]
        public void QueryIntoOrdersByHandleIndexWhateverTheStoreOrder() {
            var pool = new Pool();
            var entities = Enumerable.Range(0, 4).Select(_ => pool.CreateEntity()).ToArray();
            // Add in reverse and remove the first so the store's dense order differs from index order.
            for (var i = entities.Length - 1; i >= 0; i--) entities[i].AddComponent(new FakeComponent());
            entities[3].RemoveComponent<FakeComponent>();
            entities[3].AddComponent(new FakeComponent());

            var direct = Run(pool.Query().With<FakeComponent>());
            var buffer = new List<Entity> {entities[0]};
            pool.QueryInto(pool.Query().With<FakeComponent>(), buffer);

            Assert.That(direct, Is.Not.EqualTo(entities), "the store order must differ for this test to prove sorting");
            Assert.That(buffer, Is.EqualTo(entities));
        }

        [Test]
        public void AddingAQueriedComponentDuringEnumerationThrows() {
            var pool = new Pool();
            pool.CreateEntity(new IComponent[] {new FakeComponent()});
            var spare = pool.CreateEntity();

            var ex = Assert.Throws<InvalidOperationException>(() => {
                foreach (var entity in pool.Query().With<FakeComponent>()) spare.AddComponent(new FakeComponent());
            });
            Assert.That(ex!.Message, Does.Contain(nameof(FakeComponent)));
        }

        [Test]
        public void RemovingAnExcludedComponentDuringEnumerationThrows() {
            var pool = new Pool();
            pool.CreateEntity(new IComponent[] {new FakeComponent()});
            var holder = pool.CreateEntity(new IComponent[] {new OtherComponent()});

            Assert.Throws<InvalidOperationException>(() => {
                foreach (var entity in pool.Query().With<FakeComponent>().Without<OtherComponent>())
                    holder.RemoveComponent<OtherComponent>();
            });
        }

        [Test]
        public void ChangingAnUnrelatedStoreDuringEnumerationIsAllowed() {
            var pool = new Pool();
            var entities = new[] {
                pool.CreateEntity(new IComponent[] {new FakeComponent()}),
                pool.CreateEntity(new IComponent[] {new FakeComponent()}),
            };

            foreach (var entity in pool.Query().With<FakeComponent>()) entity.AddComponent(new OtherComponent());

            Assert.That(entities.Select(e => e.HasComponent<OtherComponent>()), Has.All.True);
        }

        [Test]
        public void CreatingAnEntityDuringAnUnfilteredQueryThrows() {
            var pool = new Pool();
            pool.CreateEntity();

            Assert.Throws<InvalidOperationException>(() => {
                foreach (var entity in pool.Query()) pool.CreateEntity();
            });
        }

        [Test]
        public void ClauseLimitIsEnforced() {
            var pool = new Pool();
            var query = pool.Query().With<FakeComponent>().With<OtherComponent>().With<WritableComponent>().With<ExtraComponent>();

            Assert.Throws<InvalidOperationException>(() => query.With<FakeComponent>());
        }

        [Test]
        public void QueryIntoRejectsAnotherPoolsQuery() {
            var pool = new Pool();
            var other = new Pool();

            Assert.Throws<ArgumentException>(() => pool.QueryInto(other.Query(), new List<Entity>()));
        }

        private sealed class ExtraComponent : IComponent {}
    }
}
