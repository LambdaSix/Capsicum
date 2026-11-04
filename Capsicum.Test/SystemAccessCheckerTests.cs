#nullable enable
using System;
using Capsicum.Events;
using Capsicum.Interfaces;
using Capsicum.Systems;
using NUnit.Framework;

namespace Capsicum.Test;

[TestFixture]
public class SystemAccessCheckerTests {
    private readonly record struct Hit(int Damage);

    private sealed class Probe(string name, Type[] reads, Type[] writes, Action<Entity> body) : ISystem<Entity> {
        public SystemDeclaration Declaration { get; } = SystemDeclaration.Create(name, reads, writes);

        public int Run(in Entity context) {
            body(context);
            return 1;
        }
    }

    private Pool _pool = null!;
    private Entity _entity = null!;
    private SystemAccessChecker _checker = null!;

    [SetUp]
    public void Setup() {
        _checker = new SystemAccessChecker();
        _pool = new Pool { AccessChecker = _checker };
        _entity = _pool.CreateEntity(new IComponent[] {new FakeComponent()});
    }

    private void RunChecked(Type[] reads, Type[] writes, Action<Entity> body) =>
        new SystemSchedule<Entity>([new Probe("probe", reads, writes, body)], checker: _checker).Run(_entity);

    [Test]
    public void ADeclaredReadPassesAndAnUndeclaredOneThrows() {
        RunChecked([typeof(FakeComponent)], [], entity => entity.GetComponent<FakeComponent>());

        var thrown = Assert.Throws<UndeclaredAccessException>(() =>
            RunChecked([typeof(OtherComponent)], [], entity => entity.GetComponent<FakeComponent>()));

        Assert.That(thrown!.System.Name, Is.EqualTo("probe"));
        Assert.That(thrown.Type, Is.EqualTo(typeof(FakeComponent)));
        Assert.That(thrown.IsWrite, Is.False);
    }

    [Test]
    public void AReadNeedsTheTypeInReadsEvenWhenTheSystemWritesIt() {
        RunChecked([typeof(FakeComponent)], [typeof(FakeComponent)], entity => entity.HasComponent<FakeComponent>());

        Assert.Throws<UndeclaredAccessException>(() =>
            RunChecked([], [typeof(FakeComponent)], entity => entity.HasComponent<FakeComponent>()));
    }

    [Test]
    public void AddingRemovingAndReplacingNeedADeclaredWrite() {
        RunChecked([], [typeof(OtherComponent)], entity => entity.AddComponent(new OtherComponent()));
        RunChecked([], [typeof(OtherComponent)], entity => entity.ReplaceComponent(new OtherComponent()));
        RunChecked([], [typeof(OtherComponent)], entity => entity.RemoveComponent<OtherComponent>());

        Assert.That(Assert.Throws<UndeclaredAccessException>(() =>
            RunChecked([typeof(OtherComponent)], [], entity => entity.AddComponent(new OtherComponent())))!.IsWrite, Is.True);
        Assert.That(_entity.HasComponent<OtherComponent>(), Is.False);
        Assert.Throws<UndeclaredAccessException>(() =>
            RunChecked([typeof(FakeComponent)], [], entity => entity.ReplaceComponent(new FakeComponent())));
        Assert.Throws<UndeclaredAccessException>(() =>
            RunChecked([typeof(FakeComponent)], [], entity => entity.RemoveComponent<FakeComponent>()));
    }

    [Test]
    public void ReleasingAnEntityWritesEachComponentItHeld() {
        _entity.AddComponent(new OtherComponent());

        Assert.Throws<UndeclaredAccessException>(() => RunChecked([typeof(FakeComponent)], [typeof(OtherComponent)], entity => entity.Destroy()));
        Assert.That(_entity.HasComponent<FakeComponent>(), Is.True);
        Assert.That(_entity.HasComponent<OtherComponent>(), Is.True);

        RunChecked([], [typeof(FakeComponent), typeof(OtherComponent)], entity => entity.Destroy());

        Assert.That(_entity.IsEnabled, Is.False);
    }

    [Test]
    public void EnumeratingAQueryNeedsAReadOfEveryTypeItNames() {
        var matched = 0;
        RunChecked([typeof(FakeComponent), typeof(OtherComponent)], [], entity => {
            foreach (var _ in entity.Pool.Query().With<FakeComponent>().Without<OtherComponent>()) matched++;
        });
        Assert.That(matched, Is.EqualTo(1));

        Assert.Throws<UndeclaredAccessException>(() => RunChecked([typeof(FakeComponent)], [], entity => {
            foreach (var _ in entity.Pool.Query().With<FakeComponent>().Without<OtherComponent>()) { }
        }));
    }

    [Test]
    public void AQueryBuiltBeforeTheRunIsCheckedWhenEnumerated() {
        var cached = _pool.Query().With<FakeComponent>();

        var matched = 0;
        RunChecked([typeof(FakeComponent)], [], _ => {
            foreach (var entity in cached) matched++;
        });
        Assert.That(matched, Is.EqualTo(1));

        Assert.Throws<UndeclaredAccessException>(() => RunChecked([], [], _ => {
            foreach (var entity in cached) { }
        }));
    }

    [Test]
    public void TheOuterDeclarationAppliesAgainAfterANestedSchedule() {
        var inner = new SystemSchedule<Entity>([new Probe("inner", [typeof(OtherComponent)], [], _ => { })], checker: _checker);

        var thrown = Assert.Throws<UndeclaredAccessException>(() => RunChecked([typeof(FakeComponent)], [], entity => {
            entity.GetComponent<FakeComponent>();
            inner.Run(entity);
            entity.HasComponent<OtherComponent>();
        }));

        Assert.That(thrown!.System.Name, Is.EqualTo("probe"));
        Assert.That(thrown.Type, Is.EqualTo(typeof(OtherComponent)));
    }

    [Test]
    public void ClearingAnEventBufferNeedsAWrite() {
        var hits = new EventBuffer<Hit>(4) {AccessChecker = _checker};
        hits.TryEmit(new Hit(3));

        Assert.Throws<UndeclaredAccessException>(() => RunChecked([typeof(Hit)], [], _ => hits.Clear()));
        Assert.That(hits.Count, Is.EqualTo(1));

        RunChecked([], [typeof(Hit)], _ => hits.Clear());
        Assert.That(hits.Count, Is.EqualTo(0));
    }

    [Test]
    public void EmittingNeedsAWriteAndReadingNeedsARead() {
        var hits = new EventBuffer<Hit>(4) {AccessChecker = _checker};
        var reader = hits.CreateReader();

        RunChecked([], [typeof(Hit)], _ => hits.TryEmit(new Hit(3)));
        var read = 0;
        RunChecked([typeof(Hit)], [], _ => read = reader.Read().Length);
        Assert.That(read, Is.EqualTo(1));

        Assert.That(Assert.Throws<UndeclaredAccessException>(() => RunChecked([typeof(Hit)], [], _ => hits.TryEmit(new Hit(1))))!.IsWrite, Is.True);
        Assert.That(hits.Count, Is.EqualTo(1));
        Assert.Throws<UndeclaredAccessException>(() => RunChecked([], [], _ => reader.Read()));
    }

    [Test]
    public void AccessOutsideARunningSystemIsNotChecked() {
        Assert.Throws<UndeclaredAccessException>(() => RunChecked([], [], entity => entity.GetComponent<FakeComponent>()));

        Assert.DoesNotThrow(() => _entity.GetComponent<FakeComponent>());
        Assert.DoesNotThrow(() => _entity.AddComponent(new OtherComponent()));
    }

    [Test]
    public void APoolWithoutACheckerIsNotChecked() {
        _pool.AccessChecker = null;

        Assert.DoesNotThrow(() => RunChecked([], [], entity => entity.GetComponent<FakeComponent>()));
    }
}
