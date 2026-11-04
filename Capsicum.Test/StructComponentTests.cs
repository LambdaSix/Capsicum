#nullable enable
using System;
using System.Collections.Generic;
using Capsicum.Events;
using Capsicum.Exceptions;
using Capsicum.Interfaces;
using Capsicum.Systems;
using NUnit.Framework;

namespace Capsicum.Test;

public struct Health : IComponent {
    public int Points;

    public Health(int points) => Points = points;
}

public struct Mood : IComponent {
    public int Anger;
}

[TestFixture]
public class StructComponentTests {
    [Test]
    public void A_struct_component_is_added_read_and_removed() {
        var pool = new Pool();
        var entity = pool.CreateEntity().Add(new Health(30));

        Assert.That(entity.HasComponent<Health>(), Is.True);
        Assert.That(entity.Read<Health>().Points, Is.EqualTo(30));

        entity.Remove<Health>();

        Assert.That(entity.HasComponent<Health>(), Is.False);
        Assert.Throws<ComponentNotRegisteredException>(() => entity.Read<Health>());
        Assert.Throws<ComponentNotRegisteredException>(() => entity.Remove<Health>());
    }

    [Test]
    public void A_change_through_ref_is_seen_by_a_later_read() {
        var pool = new Pool();
        var entity = pool.CreateEntity().Add(new Health(30));

        entity.Ref<Health>().Points -= 12;

        Assert.That(entity.Read<Health>().Points, Is.EqualTo(18));
        Assert.That(pool.StructComponents<Health>().Count, Is.EqualTo(1), "the value lives in the store, not in a copy");
    }

    [Test]
    public void Removing_one_entity_leaves_the_others_values_where_they_were() {
        var pool = new Pool();
        var entities = new List<Entity>();
        for (var index = 0; index < 5; index++) entities.Add(pool.CreateEntity().Add(new Health(index * 10)));

        entities[1].Remove<Health>();
        entities[3].Destroy();

        Assert.That(entities[0].Read<Health>().Points, Is.EqualTo(0));
        Assert.That(entities[2].Read<Health>().Points, Is.EqualTo(20));
        Assert.That(entities[4].Read<Health>().Points, Is.EqualTo(40), "the last entry moved into a hole and kept its value");
        Assert.That(pool.StructComponents<Health>().Count, Is.EqualTo(3));
    }

    [Test]
    public void Adding_twice_is_refused_and_a_released_entity_refuses_new_components() {
        var pool = new Pool();
        var entity = pool.CreateEntity().Add(new Health(1));

        Assert.Throws<ComponentAlreadyRegisteredException>(() => entity.Add(new Health(2)));
        Assert.That(entity.Read<Health>().Points, Is.EqualTo(1));
        entity.Destroy();
        Assert.Throws<InvalidOperationException>(() => entity.Add(new Health(3)));
    }

    [Test]
    public void Typed_events_carry_the_values() {
        var pool = new Pool();
        var store = pool.StructComponents<Health>();
        var seen = new List<string>();
        store.Added += (Entity _, in Health component) => seen.Add($"added {component.Points}");
        store.Replaced += (Entity _, in Health previous, in Health current) => seen.Add($"replaced {previous.Points}>{current.Points}");
        store.Removed += (Entity _, in Health component) => seen.Add($"removed {component.Points}");
        var entity = pool.CreateEntity().Add(new Health(5));

        entity.Replace(new Health(7));
        entity.Remove<Health>();

        Assert.That(seen, Is.EqualTo(new[] { "added 5", "replaced 5>7", "removed 7" }));
    }

    [Test]
    public void Struct_components_given_at_creation_are_stored_without_events() {
        var pool = new Pool();
        var added = 0;
        pool.StructComponents<Health>().Added += (Entity _, in Health _) => added++;

        var entity = pool.CreateEntity(new IComponent[] { new Health(9), new FakeComponent() });

        Assert.That(entity.Read<Health>().Points, Is.EqualTo(9));
        Assert.That(entity.HasComponent<FakeComponent>(), Is.True);
        Assert.That(added, Is.Zero);
    }

    [Test]
    public void A_query_mixes_class_and_struct_components() {
        var pool = new Pool();
        var both = pool.CreateEntity().Add(new Health(1)).AddComponent(new FakeComponent());
        pool.CreateEntity().Add(new Health(2));
        pool.CreateEntity().AddComponent(new FakeComponent());
        var calm = pool.CreateEntity().Add(new Health(3)).AddComponent(new FakeComponent()).Add(new Mood());

        var matched = new List<Entity>();
        foreach (var entity in pool.Query().With<Health>().With<FakeComponent>().Without<Mood>()) matched.Add(entity);

        Assert.That(matched, Is.EqualTo(new[] { both }));
        Assert.That(calm.HasComponent<Mood>(), Is.True);
    }

    [Test]
    public void Adding_a_struct_component_a_running_query_names_throws() {
        var pool = new Pool();
        pool.CreateEntity().Add(new Health(1));
        pool.CreateEntity().Add(new Health(2));

        Assert.Throws<InvalidOperationException>(() => {
            foreach (var _ in pool.Query().With<Health>()) pool.CreateEntity().Add(new Health(3));
        });
    }

    [Test]
    public void Ref_needs_a_read_and_a_write_and_read_needs_a_read() {
        var checker = new SystemAccessChecker();
        var pool = new Pool { AccessChecker = checker };
        var entity = pool.CreateEntity().Add(new Health(1));
        void Run(Type[] reads, Type[] writes, Action body) =>
            new SystemSchedule<int>([new Body(reads, writes, body)], checker: checker).Run(0);

        Run([typeof(Health)], [typeof(Health)], () => entity.Ref<Health>().Points++);
        Run([typeof(Health)], [], () => _ = entity.Read<Health>().Points);

        Assert.Throws<UndeclaredAccessException>(() => Run([typeof(Health)], [], () => entity.Ref<Health>().Points++));
        Assert.Throws<UndeclaredAccessException>(() => Run([], [typeof(Health)], () => entity.Ref<Health>().Points++));
        Assert.Throws<UndeclaredAccessException>(() => Run([], [], () => _ = entity.Read<Health>().Points));
        Assert.Throws<UndeclaredAccessException>(() => Run([typeof(Health)], [], () => entity.Remove<Health>()));
        Assert.That(entity.Read<Health>().Points, Is.EqualTo(2), "only the declared update ran");
    }

    private sealed class Body(Type[] reads, Type[] writes, Action body) : ISystem<int> {
        public SystemDeclaration Declaration { get; } = SystemDeclaration.Create("body", reads, writes);

        public int Run(in int context) {
            body();
            return 0;
        }
    }
}
