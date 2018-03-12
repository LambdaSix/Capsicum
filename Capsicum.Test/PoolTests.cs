using NUnit.Framework;
using NUnit.Framework.Internal;

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

        // Test entity closedown procedure (Event chain)
        [Test]
        public void CheckReleaseChain() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity(new[] {new FakeComponent()});

            bool poolRemovingPassed = false;
            bool entityReleasePassed = false;
            bool entityComponentRemovePassed = false;
            bool poolRemovedPassed = false;

            // Called once per entity removal
            pool.OnEntityRemoving += (sender, changed) => poolRemovingPassed = true;
            // Called once per entity removal
            entity0.OnEntityReleased += (sender, changed) => entityReleasePassed = true;
            // Called for all components rmeoved from the entity during closedown, normally this event is not raised
            entity0.OnComponentRemoved += (sender, changed) => entityComponentRemovePassed = true;
            // Called once per entity removal
            pool.OnEntityRemoved += (sender, changed) => poolRemovedPassed = true;

            // Remove the entity, opting in for ComponentRemoval events being broadcasted.
            // Normally this event isn't broadcast for performance reasons.
            pool.RemoveEntity(entity0, true);

            Assert.That(poolRemovingPassed, Is.True, "Failed to invoke Pool::OnEntityRemoving");
            Assert.That(entityReleasePassed, Is.True, "Failed to invoke Entity::OnEntityReleased");
            Assert.That(entityComponentRemovePassed, Is.True, "Failed to invoke Entity::OnComponentRemoved");
            Assert.That(poolRemovedPassed, Is.True, "Failed to invoke Entity::OnEntityRemoved");

        }
    }
}