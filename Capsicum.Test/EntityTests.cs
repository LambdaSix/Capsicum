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
    }
}