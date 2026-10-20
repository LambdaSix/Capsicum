using System.Linq;
using Capsicum.Exceptions;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture]
    public class EntityTests {
        /* Initial State Tests */

        [TestFixture]
        public class InitialState {
            private Entity e;

            [SetUp]
            public void Setup() => e = new Entity();

            [Test]
            public void ThrowsWhenAttemptingToRetrieveUnregisteredComponent() {
                Assert.Throws<ComponentNotRegisteredException>(() => e.GetComponent<FakeComponent>());
            }

            [Test]
            public void NoComponentsWhenNoComponentsRegistered()
            {
                Assert.That(!e.GetComponents().Any());
            }
        }

        [TestFixture]
        public class Events {
            private Pool pool;
            private Entity e;

            [SetUp]
            public void Setup() {
                pool = new Pool();
                e = pool.CreateEntity();
            }

            [Test]
            public void DispatchesAdded() {
                var dispatched = 0;
                var store = pool.Components<FakeComponent>();
                store.Added += (entity, component) => {
                    dispatched++;
                    Assert.That(component, Is.InstanceOf<FakeComponent>());
                    Assert.That(entity, Is.SameAs(e));
                };

                store.Removed += (_, _) => Assert.Fail();
                store.Replaced += (_, _, _) => Assert.Fail();

                e.AddComponent(new FakeComponent());
                Assert.AreEqual(1, dispatched);
            }

            [Test]
            public void DispatchesRemoved() {
                var dispatched = 0;
                var added = new FakeComponent();
                e.AddComponent(added);

                var store = pool.Components<FakeComponent>();
                store.Removed += (entity, component) => {
                    dispatched++;
                    Assert.That(component, Is.SameAs(added));
                    Assert.That(entity, Is.SameAs(e));
                };

                store.Added += (_, _) => Assert.Fail();
                store.Replaced += (_, _, _) => Assert.Fail();

                e.RemoveComponent<FakeComponent>();
                Assert.AreEqual(1, dispatched);
            }

            [Test]
            public void DispatchesReplacedWithPreviousAndNewValues() {
                var dispatched = 0;
                var component = new FakeComponent();
                var componentNew = new FakeComponent();
                e.AddComponent(component);

                var store = pool.Components<FakeComponent>();
                store.Replaced += (entity, previous, current) => {
                    dispatched++;
                    Assert.That(entity, Is.SameAs(e));
                    Assert.That(previous, Is.SameAs(component));
                    Assert.That(current, Is.SameAs(componentNew));
                };

                store.Added += (_, _) => Assert.Fail();
                store.Removed += (_, _) => Assert.Fail();

                e.ReplaceComponent(componentNew);
                Assert.AreEqual(1, dispatched);
                Assert.That(e.GetComponent<FakeComponent>(), Is.SameAs(componentNew));
            }

            [Test]
            public void EventsAreScopedToTheirComponentType() {
                var otherDispatched = 0;
                pool.Components<OtherComponent>().Added += (_, _) => otherDispatched++;

                e.AddComponent(new OtherComponent());
                Assert.That(otherDispatched, Is.EqualTo(1));

                e.AddComponent(new FakeComponent());
                Assert.That(otherDispatched, Is.EqualTo(1));
            }

            [Test]
            public void ThrowsExceptionWhenReplacingUnaddedComponent() {
                Assert.Throws<ComponentNotRegisteredException>(() => e.ReplaceComponent(new FakeComponent()));
            }
        }
    }
}