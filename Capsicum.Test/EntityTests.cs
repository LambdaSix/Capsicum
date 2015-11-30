using System.Linq;
using System.Runtime.Remoting.Channels;
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
            private Entity e;

            [SetUp]
            public void Setup() => e = new Entity();

            [Test]
            public void DispatchesOnComponentAdded() {
                var dispatched = 0;
                e.OnComponentAdded += (sender, value) => {
                    dispatched++;
                    Assert.That(value.Component is FakeComponent);
                    Assert.That(value.Entity == e);
                };

                e.OnComponentRemoved += (o, _) => Assert.Fail();
                e.OnComponentReplaced += (o, _) => Assert.Fail();

                e.AddComponent(new FakeComponent());
                Assert.AreEqual(1, dispatched);
            }

            [Test]
            public void DispatchesOnComponentRemoved() {
                var dispatched = 0;
                e.AddComponent(new FakeComponent());

                e.OnComponentRemoved += (sender, value) => {
                    dispatched++;
                    Assert.That(value.Component is FakeComponent);
                    Assert.That(value.Entity == e);
                };

                e.OnComponentAdded += (o, _) => Assert.Fail();
                e.OnComponentReplaced += (o, _) => Assert.Fail();

                e.RemoveComponent<FakeComponent>();
                Assert.AreEqual(1, dispatched);
            }

            [Test]
            public void DispatchesOnComponentReplaced() {
                var dispatched = 0;
                var component = new FakeComponent();
                e.AddComponent(component);

                e.OnComponentReplaced += (sender, value) => {
                    dispatched++;
                    Assert.That(value.NewComponent == component);
                    Assert.That(value.PreviousComponent == component);
                };

                e.OnComponentAdded += (o, _) => Assert.Fail();
                e.OnComponentRemoved += (o, _) => Assert.Fail();

                e.ReplaceComponent(component);
                Assert.AreEqual(1, dispatched);
            }

            [Test]
            public void DispatchesOnComponentReplaced_PrevAndNewValues()
            {
                var dispatched = 0;
                var component = new FakeComponent();
                var componentNew = new FakeComponent();

                e.AddComponent(component);

                e.OnComponentReplaced += (sender, value) => {
                    dispatched++;
                    Assert.That(value.NewComponent == componentNew);
                    Assert.That(value.PreviousComponent == component);
                };

                e.OnComponentAdded += (o, _) => Assert.Fail();
                e.OnComponentRemoved += (o, _) => Assert.Fail();

                e.ReplaceComponent(componentNew);
                Assert.AreEqual(1, dispatched);
            }

            [Test]
            public void ThrowsExceptionWhenReplacingUnaddedComponent() {
                Assert.Throws<ComponentNotRegisteredException>(() => e.ReplaceComponent(new FakeComponent()));
            }
        }
    }
}