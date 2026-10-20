using System;
using System.Linq;
using Capsicum.Exceptions;
using Capsicum.Interfaces;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture]
    public class StorageTests {
        [Test]
        public void ReleasedSlotIsReusedWithTheNextGeneration() {
            var pool = new Pool();
            var first = pool.CreateEntity();
            var firstHandle = first.Handle;

            pool.RemoveEntity(first);
            var second = pool.CreateEntity();

            Assert.That(second.Handle.Index, Is.EqualTo(firstHandle.Index));
            Assert.That(second.Handle.Generation, Is.EqualTo(firstHandle.Generation + 1));
            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void StaleHandleDoesNotResolveToTheSlotsNewOccupant() {
            var pool = new Pool();
            var first = pool.CreateEntity();
            var firstHandle = first.Handle;

            Assert.That(pool.TryGetEntity(firstHandle, out var live), Is.True);
            Assert.That(live, Is.SameAs(first));

            pool.RemoveEntity(first);
            var second = pool.CreateEntity();

            Assert.That(pool.IsAlive(second.Handle), Is.True);
            Assert.That(pool.IsAlive(firstHandle), Is.False);
            Assert.That(pool.TryGetEntity(firstHandle, out _), Is.False);
        }

        [Test]
        public void FreedIndicesAreReusedFirstInFirstOut() {
            var pool = new Pool();
            var a = pool.CreateEntity();
            var b = pool.CreateEntity();
            var c = pool.CreateEntity();

            pool.RemoveEntity(b);
            pool.RemoveEntity(a);

            Assert.That(pool.CreateEntity().Handle.Index, Is.EqualTo(b.Handle.Index));
            Assert.That(pool.CreateEntity().Handle.Index, Is.EqualTo(a.Handle.Index));
            Assert.That(pool.CreateEntity().Handle.Index, Is.GreaterThan(c.Handle.Index));
        }

        [Test]
        public void ReleasedEntityKeepsNoComponentsAndRefusesNewOnes() {
            var pool = new Pool();
            var entity = pool.CreateEntity(new[] {new FakeComponent()});
            Assert.That(entity.HasComponent<FakeComponent>(), Is.True);

            pool.RemoveEntity(entity);
            var successor = pool.CreateEntity(new[] {new FakeComponent {Name = "successor"}});

            Assert.That(successor.Handle.Index, Is.EqualTo(entity.Handle.Index));
            Assert.That(entity.HasComponent<FakeComponent>(), Is.False);
            Assert.That(entity.GetComponentOf<FakeComponent>(), Is.Null);
            Assert.That(entity.GetComponents(), Is.Empty);
            Assert.Throws<InvalidOperationException>(() => entity.AddComponent(new OtherComponent()));
            Assert.That(successor.GetComponent<FakeComponent>().Name, Is.EqualTo("successor"));
        }

        [Test]
        public void RemovingFromTheMiddleKeepsTheOtherEntitiesComponents() {
            var pool = new Pool();
            var entities = Enumerable.Range(0, 5)
                .Select(i => pool.CreateEntity(new[] {new FakeComponent {Name = i.ToString()}}))
                .ToArray();

            entities[1].RemoveComponent<FakeComponent>();

            Assert.That(entities[1].HasComponent<FakeComponent>(), Is.False);
            Assert.That(new[] {0, 2, 3, 4}.Select(i => entities[i].GetComponent<FakeComponent>().Name),
                Is.EqualTo(new[] {"0", "2", "3", "4"}));
            Assert.That(pool.Components<FakeComponent>().Count, Is.EqualTo(4));
        }

        [Test]
        public void ComponentsAreStoredUnderTheirRuntimeType() {
            var entity = new Entity();
            IComponent component = new FakeComponent();

            entity.AddComponent(component);

            Assert.That(entity.GetComponent<FakeComponent>(), Is.SameAs(component));
            Assert.Throws<ComponentAlreadyRegisteredException>(() => entity.AddComponent(new FakeComponent()));
        }

        [Test]
        public void LookupIsExactTypeNotAssignable() {
            var entity = new Entity().AddComponent(new DerivedComponent());

            Assert.That(entity.HasComponent<DerivedComponent>(), Is.True);
            Assert.That(entity.HasComponent<FakeComponent>(), Is.False);
            Assert.That(entity.GetComponentOf<FakeComponent>(), Is.Null);
        }

        [Test]
        public void EntitiesFromSeparatePoolsDoNotShareAHashCode() {
            var hashes = Enumerable.Range(0, 100).Select(_ => new Pool().CreateEntity().GetHashCode()).ToArray();

            Assert.That(hashes.Distinct().Count(), Is.GreaterThan(90));
        }

        [Test]
        public void HandleHashIsAStableFunctionOfIndexAndGeneration() {
            Assert.That(new EntityHandle(3, 1).GetHashCode(), Is.EqualTo(new EntityHandle(3, 1).GetHashCode()));
            Assert.That(new EntityHandle(3, 1).GetHashCode(), Is.Not.EqualTo(new EntityHandle(3, 2).GetHashCode()));
            Assert.That(new EntityHandle(3, 1), Is.Not.EqualTo(new EntityHandle(3, 2)));
        }

        private sealed class DerivedComponent : FakeComponent {}
    }
}
