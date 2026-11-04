#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Capsicum.Interfaces;
using Capsicum.Systems;
using NUnit.Framework;

namespace Capsicum.Test;

public readonly struct Serial(long value) : IComponent, IOrderKey {
    public long Value { get; } = value;

    public long OrderKey => Value;
}

[TestFixture]
public class EntityOrderTests {
    private static long[] Walk(EntityOrder order) {
        var keys = new List<long>();
        foreach (var entity in order) keys.Add(entity.Read<Serial>().Value);
        return [.. keys];
    }

    [Test]
    public void Entities_are_walked_by_ascending_key_whatever_order_they_were_added_in() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        foreach (var serial in new long[] { 5, 1, 9, 3, 7 }) pool.CreateEntity().Add(new Serial(serial));

        Assert.That(Walk(order), Is.EqualTo(new long[] { 1, 3, 5, 7, 9 }));
    }

    [Test]
    public void A_removal_in_the_middle_keeps_the_order_of_the_rest() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        var entities = new long[] { 1, 2, 3, 4, 5 }.Select(serial => pool.CreateEntity().Add(new Serial(serial))).ToArray();

        entities[2].Destroy();
        entities[0].Remove<Serial>();
        pool.CreateEntity().Add(new Serial(6));

        Assert.That(Walk(order), Is.EqualTo(new long[] { 2, 4, 5, 6 }));
    }

    [Test]
    public void Replacing_the_key_moves_the_entity() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        var first = pool.CreateEntity().Add(new Serial(1));
        pool.CreateEntity().Add(new Serial(2));

        first.Replace(new Serial(3));

        Assert.That(Walk(order), Is.EqualTo(new long[] { 2, 3 }));
    }

    [Test]
    public void A_duplicate_key_is_refused_before_anything_changes() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        pool.CreateEntity().Add(new Serial(1));
        var other = pool.CreateEntity().Add(new Serial(2));
        var late = pool.CreateEntity();

        Assert.Throws<ArgumentException>(() => late.Add(new Serial(1)));
        Assert.Throws<ArgumentException>(() => other.Replace(new Serial(1)));

        Assert.That(late.HasComponent<Serial>(), Is.False, "the refused component was not stored");
        Assert.That(other.Read<Serial>().Value, Is.EqualTo(2));
        Assert.That(Walk(order), Is.EqualTo(new long[] { 1, 2 }));
    }

    [Test]
    public void A_reload_in_any_order_walks_the_same_as_before_the_save() {
        var original = new Pool();
        var order = original.OrderBy<Serial>();
        foreach (var serial in Enumerable.Range(1, 50)) original.CreateEntity().Add(new Serial(serial * 3 % 101));
        var matches = new List<Entity>();
        original.QueryInto(original.Query().With<Serial>(), matches);
        foreach (var entity in matches.Where(entity => entity.Read<Serial>().Value % 7 == 0)) entity.Destroy();
        var saved = Walk(order);

        var reloaded = new Pool();
        var reloadedOrder = reloaded.OrderBy<Serial>();
        using (reloadedOrder.BeginBulkLoad()) {
            foreach (var serial in saved.Reverse()) reloaded.CreateEntity().Add(new Serial(serial));
            Assert.Throws<InvalidOperationException>(() => Walk(reloadedOrder), "the order is not walked half loaded");
        }

        Assert.That(saved, Is.Not.Empty);
        Assert.That(Walk(reloadedOrder), Is.EqualTo(saved));
    }

    [Test]
    public void A_duplicate_in_a_bulk_load_is_refused_when_the_load_ends() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        var load = order.BeginBulkLoad();
        pool.CreateEntity().Add(new Serial(4));
        pool.CreateEntity().Add(new Serial(4));

        Assert.Throws<ArgumentException>(() => load.Dispose());
    }

    [Test]
    public void Entities_that_held_a_key_before_the_order_existed_are_sorted_into_it() {
        var pool = new Pool();
        foreach (var serial in new long[] { 8, 2, 5 }) pool.CreateEntity().Add(new Serial(serial));

        var order = pool.OrderBy<Serial>();

        Assert.That(Walk(order), Is.EqualTo(new long[] { 2, 5, 8 }));
        Assert.That(pool.OrderBy<Serial>(), Is.SameAs(order), "one order per key type");
    }

    [Test]
    public void A_query_over_the_order_keeps_key_order_and_skips_unlisted_entities() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        pool.CreateEntity().Add(new Serial(3)).Add(new Health(1));
        pool.CreateEntity().Add(new Serial(1)).Add(new Health(1));
        pool.CreateEntity().Add(new Serial(2));
        pool.CreateEntity(new IComponent[] { new Serial(0), new Health(1) }, addToPool: false);

        var matched = new List<long>();
        foreach (var entity in order.Where(pool.Query().With<Health>())) matched.Add(entity.Read<Serial>().Value);

        Assert.That(matched, Is.EqualTo(new long[] { 1, 3 }));
        Assert.That(Walk(order), Is.EqualTo(new long[] { 1, 2, 3 }), "the unlisted entity is in the order but never walked");
    }

    [Test]
    public void Changing_the_order_while_walking_it_throws() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        pool.CreateEntity().Add(new Serial(1));
        pool.CreateEntity().Add(new Serial(2));

        Assert.Throws<InvalidOperationException>(() => {
            foreach (var entity in order) entity.Destroy();
        });
    }

    [Test]
    public void A_key_cannot_be_changed_through_ref_so_removal_always_finds_it() {
        var pool = new Pool();
        var order = pool.OrderBy<Serial>();
        var entity = pool.CreateEntity().Add(new Serial(1));
        pool.CreateEntity().Add(new Serial(2));

        Assert.Throws<InvalidOperationException>(() => entity.Ref<Serial>());
        entity.Replace(new Serial(3));
        entity.Destroy();

        Assert.That(Walk(order), Is.EqualTo(new long[] { 2 }));
        Assert.That(pool.StructComponents<Serial>().Count, Is.EqualTo(1));
    }

    [Test]
    public void Walking_an_order_in_a_system_needs_a_read_of_its_key() {
        var checker = new SystemAccessChecker();
        var pool = new Pool { AccessChecker = checker };
        var order = pool.OrderBy<Serial>();
        pool.CreateEntity().Add(new Serial(1)).Add(new Health(1));
        void Run(Type[] reads, Action body) => new SystemSchedule<int>([new Body(reads, body)], checker: checker).Run(0);
        var walked = 0;

        Run([typeof(Serial)], () => { foreach (var _ in order) walked++; });
        Run([typeof(Serial), typeof(Health)], () => { foreach (var _ in order.Where(pool.Query().With<Health>())) walked++; });
        Assert.That(walked, Is.EqualTo(2));

        Assert.Throws<UndeclaredAccessException>(() => Run([], () => { foreach (var _ in order) walked++; }));
        Assert.Throws<UndeclaredAccessException>(() => Run([typeof(Health)], () => { foreach (var _ in order.Where(pool.Query().With<Health>())) walked++; }));
    }

    private sealed class Body(Type[] reads, Action body) : ISystem<int> {
        public SystemDeclaration Declaration { get; } = SystemDeclaration.Create("body", reads, []);

        public int Run(in int context) {
            body();
            return 0;
        }
    }
}
