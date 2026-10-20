using System;
using System.Collections.Generic;
using Capsicum.Interfaces;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture]
    public class AllocationTests {
        private const int EntityCount = 64;
        private const int Iterations = 200;

        private Pool _pool;
        private Entity[] _entities;
        private List<Entity> _buffer;

        [SetUp]
        public void Setup() {
            _pool = new Pool();
            _entities = new Entity[EntityCount];
            for (var i = 0; i < EntityCount; i++) {
                _entities[i] = i % 2 == 0
                    ? _pool.CreateEntity(new IComponent[] {new FakeComponent(), new OtherComponent()})
                    : _pool.CreateEntity(new IComponent[] {new FakeComponent()});
            }

            _buffer = new List<Entity>(EntityCount);
        }

        private static long AllocatedBy(Action action) {
            action();
            var before = GC.GetAllocatedBytesForCurrentThread();
            action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void ComponentLookupsAllocateNothing() {
            var found = 0;
            var bytes = AllocatedBy(() => {
                for (var n = 0; n < Iterations; n++) {
                    foreach (var entity in _entities) {
                        if (entity.HasComponent<OtherComponent>()) found++;
                        if (entity.TryGetComponent<FakeComponent>(out _)) found++;
                        if (entity.GetComponentOf<OtherComponent>() != null) found++;
                        found += entity.GetComponent<FakeComponent>() != null ? 1 : 0;
                    }
                }
            });

            Assert.That(found, Is.GreaterThan(0));
            Assert.That(bytes, Is.EqualTo(0));
        }

        [Test]
        public void QueryEnumerationAllocatesNothing() {
            var matched = 0;
            var bytes = AllocatedBy(() => {
                for (var n = 0; n < Iterations; n++) {
                    foreach (var entity in _pool.Query().With<FakeComponent>().Without<OtherComponent>()) matched++;
                }
            });

            Assert.That(matched, Is.GreaterThan(0));
            Assert.That(bytes, Is.EqualTo(0));
        }

        [Test]
        public void QueryIntoAReusedBufferAllocatesNothing() {
            var bytes = AllocatedBy(() => {
                for (var n = 0; n < Iterations; n++) _pool.QueryInto(_pool.Query().With<OtherComponent>(), _buffer);
            });

            Assert.That(_buffer.Count, Is.EqualTo(EntityCount / 2));
            Assert.That(bytes, Is.EqualTo(0));
        }

        [Test]
        public void PoolEnumerationAllocatesNothing() {
            var seen = 0;
            var bytes = AllocatedBy(() => {
                for (var n = 0; n < Iterations; n++) {
                    foreach (var entity in _pool) seen++;
                }
            });

            Assert.That(seen, Is.GreaterThan(0));
            Assert.That(bytes, Is.EqualTo(0));
        }

        [Test]
        public void ComponentChurnAllocatesNothingOnceStoresHaveGrown() {
            var bytes = AllocatedBy(() => {
                for (var n = 0; n < Iterations; n++) {
                    foreach (var entity in _entities) {
                        var component = entity.GetComponent<FakeComponent>();
                        entity.RemoveComponent<FakeComponent>();
                        entity.AddComponent(component);
                    }
                }
            });

            Assert.That(_pool.Components<FakeComponent>().Count, Is.EqualTo(EntityCount));
            Assert.That(bytes, Is.EqualTo(0));
        }
    }
}
