using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.Remoting.Channels;
using NUnit.Framework;

namespace Capsicum.Test {
    [TestFixture]
    public class GroupTests {
        [Test]
        public void CreateNewGroupFromPoolAddEntities() {
            var pool = new Pool();
            var ent1 = pool.CreateEntity(new[] {new FakeComponent()});
            var ent2 = pool.CreateEntity(new[] {new FakeComponent()});

            pool.RegisterGroup("TestGroup", entities => entities.Where(s => s.HasComponent<FakeComponent>()));
            pool.TryGetGroup("TestGroup", out var group);

            Assert.That(group, Is.Not.Empty);
            Assert.That(group, Is.Not.Null);
        }

        [Test]
        public void GroupUpdatesOnPoolChanges() {
            var pool = new Pool();
            var ent0 = pool.CreateEntity(new[] { new FakeComponent() });
            var ent1 = pool.CreateEntity(new[] { new FakeComponent() });

            pool.RegisterGroup("TestGroup", entities => entities.Where(s => s.HasComponent<FakeComponent>()));
            var group = pool.GetGroup("TestGroup");

            bool addSuccess = false;
            bool removeSuccess = false;
            bool replacedSuccess = false;

            group.OnEntityAdded += (sender, changed) => {
                addSuccess = true;
                Console.WriteLine($"({sender}) Detected change in {changed.Group}: ADDED - {String.Join(",", changed.Entities.Select(s => s.ToString()))}\n");
            };

            group.OnEntityRemoved += (sender, changed) => {
                removeSuccess = true;
                Console.WriteLine($"({sender}) Detected change in {changed.Group}: REMOVED - {String.Join(",", changed.Entities.Select(s => s.ToString()))}\n");
            };

            group.OnEntityChanged += (sender, changed) => {
                replacedSuccess = true;
                Console.WriteLine($"({sender}) Detected change in {changed.Group}: CHANGED - {String.Join(",", changed.Entities.Select(s => s.ToString()))}\n");
            };

            Console.WriteLine("Creating new entity2");
            var ent2 = pool.CreateEntity(new[] { new FakeComponent() });

            Console.WriteLine("Removing entity0");
            pool.RemoveEntity(ent0);
            Console.WriteLine("Adding component to entity1");
            ent1.AddComponent(new OtherComponent());

            foreach (var entity in group) {
                Console.WriteLine($"Entity: {entity}");
                Console.WriteLine($"\tComponents: {String.Join(",", entity.GetComponents().Select(s => s.GetType().Name))}");
            }

            Assert.That(pool.Count, Is.EqualTo(2));
            Assert.That(group.Count, Is.EqualTo(2));
            Assert.That(addSuccess, Is.True);
            Assert.That(replacedSuccess, Is.True);
            Assert.That(removeSuccess, Is.True);
        }
    }
}