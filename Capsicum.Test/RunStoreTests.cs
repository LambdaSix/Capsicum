#nullable enable
using System;
using System.Collections.Generic;
using Capsicum.Interfaces;
using NUnit.Framework;

namespace Capsicum.Test;

/// <summary>A struct component that owns a run, the way a Monster's cooldown table would.</summary>
public struct Cooldowns : IComponent {
    public RunHandle Run;
}

[TestFixture]
public class RunStoreTests {
    private static RunStore<int> Store(int runLength = 4, int capacity = 8) => new(new RunStoreOptions(runLength, capacity));

    private static RunHandle Allocate(RunStore<int> store, params int[] values) {
        Assert.That(store.TryAllocate(values.Length, out var handle), Is.True);
        values.CopyTo(store.Get(handle));
        return handle;
    }

    [Test]
    public void An_allocated_run_reads_as_zeroes_and_keeps_what_is_written() {
        var store = Store();

        Assert.That(store.TryAllocate(3, out var handle), Is.True);
        Assert.That(store.Get(handle).ToArray(), Is.EqualTo(new[] { 0, 0, 0 }));

        store.Get(handle)[1] = 42;

        Assert.That(store.Get(handle).ToArray(), Is.EqualTo(new[] { 0, 42, 0 }));
        Assert.That(handle.Length, Is.EqualTo(3));
        Assert.That(store.Count, Is.EqualTo(1));
        Assert.That(store.Available, Is.EqualTo(7));
    }

    [Test]
    public void Runs_do_not_overlap() {
        var store = Store(runLength: 4);
        var first = Allocate(store, 1, 2);
        var second = Allocate(store, 3, 4, 5, 6);

        store.Get(first)[1] = 20;

        Assert.That(store.Get(first).ToArray(), Is.EqualTo(new[] { 1, 20 }));
        Assert.That(store.Get(second).ToArray(), Is.EqualTo(new[] { 3, 4, 5, 6 }));
    }

    [Test]
    public void A_freed_run_is_reused_without_its_old_data_and_its_old_handle_goes_stale() {
        var store = Store(runLength: 4, capacity: 1);
        var old = Allocate(store, 7, 7, 7);
        store.Free(old);

        Assert.That(store.TryAllocate(3, out var reused), Is.True);

        Assert.That(store.IsLive(reused), Is.True);
        Assert.That(store.Get(reused).ToArray(), Is.EqualTo(new[] { 0, 0, 0 }), "the only slot was reused and holds none of the earlier owner's data");
        Assert.That(store.IsLive(old), Is.False, "same slot and length, older generation");
        Assert.Throws<ArgumentException>(() => store.Get(old));
        Assert.Throws<ArgumentException>(() => store.Free(old));
        Assert.That(store.IsLive(reused), Is.True, "freeing the stale handle changed nothing");
        Assert.That(store.Count, Is.EqualTo(1));
    }

    [Test]
    public void A_run_freed_twice_is_refused_the_second_time() {
        var store = Store();
        var handle = Allocate(store, 1);
        store.Free(handle);

        Assert.That(store.Count, Is.EqualTo(0));
        Assert.Throws<ArgumentException>(() => store.Free(handle));
        Assert.That(store.Count, Is.EqualTo(0));
        Assert.That(store.Available, Is.EqualTo(8));
    }

    [Test]
    public void A_full_store_refuses_and_changes_nothing() {
        var store = Store(runLength: 2, capacity: 2);
        var first = Allocate(store, 1, 2);
        var second = Allocate(store, 3, 4);
        Assert.That(store.Available, Is.EqualTo(0));

        Assert.That(store.TryAllocate(1, out var refused), Is.False);

        Assert.That(refused, Is.EqualTo(RunHandle.None));
        Assert.That(store.Count, Is.EqualTo(2));
        Assert.That(store.Get(first).ToArray(), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(store.Get(second).ToArray(), Is.EqualTo(new[] { 3, 4 }));

        store.Free(first);
        Assert.That(store.TryAllocate(1, out _), Is.True, "a freed slot makes room again");
    }

    [Test]
    public void An_empty_run_takes_no_slot() {
        var store = Store(capacity: 1);
        Allocate(store, 1);

        Assert.That(store.TryAllocate(0, out var empty), Is.True, "even a full store hands out the empty run");

        Assert.That(empty.IsNone, Is.True);
        Assert.That(store.Get(empty).IsEmpty, Is.True);
        Assert.That(store.IsLive(empty), Is.True);
        store.Free(empty);
        Assert.That(store.Count, Is.EqualTo(1));
    }

    [Test]
    public void A_run_longer_than_the_run_length_or_negative_is_refused() {
        var store = Store(runLength: 4);

        Assert.That(store.TryAllocate(4, out _), Is.True);
        Assert.Throws<ArgumentOutOfRangeException>(() => store.TryAllocate(5, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.TryAllocate(-1, out _));
        Assert.That(store.Count, Is.EqualTo(1));
    }

    [Test]
    public void Runs_keep_their_data_while_the_store_grows_to_its_capacity() {
        const int capacity = 100;
        var store = Store(runLength: 3, capacity: capacity);
        var handles = new RunHandle[capacity];
        for (var i = 0; i < capacity; i++) handles[i] = Allocate(store, i, i + 1, i + 2);

        Assert.That(store.TryAllocate(1, out _), Is.False);
        for (var i = 0; i < capacity; i++) Assert.That(store.Get(handles[i]).ToArray(), Is.EqualTo(new[] { i, i + 1, i + 2 }), $"run {i}");
    }

    [Test]
    public void A_handle_of_a_slot_never_allocated_is_refused() {
        var small = Store();
        var big = Store(capacity: 32);
        for (var i = 0; i < 20; i++) Allocate(big, i);
        Assert.That(big.TryAllocate(1, out var far), Is.True);

        Assert.That(big.IsLive(far), Is.True);
        Assert.That(small.IsLive(far), Is.False);
        Assert.Throws<ArgumentException>(() => small.Get(far));
    }

    [Test]
    public void Restoring_in_spawn_serial_order_keeps_each_entity_run_and_the_order() {
        var pool = new Pool();
        var store = Store(runLength: 4);
        var bySerial = pool.OrderBy<Serial>();
        Spawn(pool, store, 30, 3, 1, 4);
        var gone = Spawn(pool, store, 10, 9, 9);
        Spawn(pool, store, 20, 2, 6);
        Spawn(pool, store, 40);
        store.Free(gone.Read<Cooldowns>().Run);
        pool.RemoveEntity(gone);

        var saved = new List<(long Serial, int[] Run)>();
        foreach (var entity in bySerial) saved.Add((entity.Read<Serial>().OrderKey, store.Get(entity.Read<Cooldowns>().Run).ToArray()));

        var restoredPool = new Pool();
        var restoredStore = Store(runLength: 4);
        var restoredOrder = restoredPool.OrderBy<Serial>();
        using (restoredOrder.BeginBulkLoad()) {
            foreach (var (serial, run) in saved) Spawn(restoredPool, restoredStore, serial, run);
        }

        var restored = new List<(long Serial, int[] Run)>();
        foreach (var entity in restoredOrder) restored.Add((entity.Read<Serial>().OrderKey, restoredStore.Get(entity.Read<Cooldowns>().Run).ToArray()));

        Assert.That(saved, Has.Count.EqualTo(3));
        Assert.That(saved[0].Serial, Is.EqualTo(20));
        Assert.That(restored, Has.Count.EqualTo(saved.Count));
        for (var i = 0; i < saved.Count; i++) {
            Assert.That(restored[i].Serial, Is.EqualTo(saved[i].Serial));
            Assert.That(restored[i].Run, Is.EqualTo(saved[i].Run), $"serial {saved[i].Serial}");
        }
        Assert.That(restoredStore.Count, Is.EqualTo(2), "the entity with an empty run took no slot");
    }

    [Test]
    public void Options_take_positive_limits_within_their_maximums() {
        var options = new RunStoreOptions(RunStoreOptions.MaximumRunLength, Array.MaxLength / RunStoreOptions.MaximumRunLength);

        Assert.That(options.RunLength, Is.EqualTo(RunStoreOptions.MaximumRunLength));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RunStoreOptions(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RunStoreOptions(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RunStoreOptions(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RunStoreOptions(RunStoreOptions.MaximumRunLength + 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RunStoreOptions(1, RunStoreOptions.MaximumCapacity + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new RunStoreOptions(RunStoreOptions.MaximumRunLength, RunStoreOptions.MaximumCapacity));
        Assert.Throws<ArgumentNullException>(() => _ = new RunStore<int>(null!));
    }

    private static Entity Spawn(Pool pool, RunStore<int> store, long serial, params int[] run) =>
        pool.CreateEntity().Add(new Serial(serial)).Add(new Cooldowns { Run = Allocate(store, run) });
}
