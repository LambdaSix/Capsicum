using System;
using System.Linq;
using Capsicum.Serialization;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture]
    public class SerializationTests {
        [Test]
        public void CanWriteSingleEntity() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity(new[] {new WritableComponent("NewComponent")});

            var writer = new EntityWriter();
            var buffer = writer.Serialize(new[] {entity0});

            Assert.That(buffer, Is.Not.Null);

            var expected =
                @"10001000FA1000F165436170736963756D2E546573742E5772697461626C65436F6D" +
                @"706F6E656E742C20436170736963756D2E546573742C2056657273696F6E3D312E30" +
                @"2E302E302C2043756C747572653D6E65757472616C2C205075626C69634B6579546F" +
                @"6B656E3D6E756C6CD000C4E6577436F6D706F6E656E74F2FB";

            var actual = String.Join("", buffer.Select(s => s.ToString("X")));

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void CanReadWriteSingleEntity() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity(new[] { new WritableComponent("NewComponent") });

            var writer = new EntityWriter();
            var buffer = writer.Serialize(new[] { entity0 });

            Assert.That(buffer, Is.Not.Null);

            var reader = new EntityReader();
            var entities = reader.ReadFrom(buffer, components => pool.CreateEntity(components));

            var entity0_r = entities.First();
            Assert.That(entity0_r, Is.TypeOf<Entity>());
            Assert.That(entity0_r.GetComponents(), Is.Not.Empty);

            var comp = entity0_r.GetComponents().ToList();

            Assert.AreEqual(1, comp.Count);
            Assert.That(comp[0], Is.TypeOf<WritableComponent>());

            var writableComponent = comp[0] as WritableComponent;
            Assert.That(writableComponent.Name, Is.EqualTo("NewComponent"));
        }

        [Test]
        public void CanReadWriteMultipleEntities() {
            var pool = new Pool();
            var entity0 = pool.CreateEntity(new[] { new WritableComponent("NewComponent") });
            var entity1 = pool.CreateEntity(new[] { new WritableComponent("NewComponent") });
            var entity2 = pool.CreateEntity(new[] { new WritableComponent("NewComponent") });

            var writer = new EntityWriter();
            var buffer = writer.Serialize(pool);

            var newPool = new Pool();

            Assert.That(buffer, Is.Not.Null);

            var reader = new EntityReader();
            var entities = reader.ReadFrom(buffer, components => newPool.CreateEntity(components, false)).ToList();

            Assert.That(entities.Count, Is.EqualTo(3));
            Assert.That(pool.Count, Is.EqualTo(3));
            Assert.That(newPool.Count, Is.EqualTo(0));

            foreach (var entity in entities) {
                Assert.That(entity, Is.TypeOf<Entity>());
                Assert.That(entity.GetComponents(), Is.Not.Empty);

                var comp = entity.GetComponents().ToList();

                Assert.AreEqual(1, comp.Count);
                Assert.That(comp[0], Is.TypeOf<WritableComponent>());

                var writableComponent = comp[0] as WritableComponent;
                Assert.That(writableComponent.Name, Is.EqualTo("NewComponent"));

                Console.WriteLine(entity);
            }
        }
    }
}