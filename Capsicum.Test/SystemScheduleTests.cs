#nullable enable
using System;
using System.Collections.Generic;
using Capsicum.Systems;
using NUnit.Framework;

namespace Capsicum.Test;

[TestFixture]
public class SystemScheduleTests {
    private sealed class Tick {
        public readonly List<string> Ran = [];
    }

    private sealed class RecordingSystem(string name, Type[] reads, Type[] writes, int visited = 0) : ISystem<Tick> {
        public SystemDeclaration Declaration { get; } = SystemDeclaration.Create(name, reads, writes);

        public int Run(in Tick context) {
            context.Ran.Add(Declaration.Name);
            return visited;
        }
    }

    private sealed class ThrowingSystem : ISystem<Tick> {
        public SystemDeclaration Declaration { get; } = SystemDeclaration.Create("throws", [], []);

        public int Run(in Tick context) => throw new InvalidOperationException("boom");
    }

    private sealed class RecordingObserver : ISystemObserver {
        public readonly List<string> Calls = [];

        public void Starting(int position, SystemDeclaration system) => Calls.Add($"start {position} {system.Name}");

        public void Finished(int position, SystemDeclaration system, int visited) => Calls.Add($"end {position} {system.Name} {visited}");
    }

    private static RecordingSystem Pass(string name, Type[]? reads = null, Type[]? writes = null, int visited = 0) =>
        new(name, reads ?? [], writes ?? [], visited);

    [Test]
    public void RunsSystemsInTheListedOrder() {
        var tick = new Tick();
        var schedule = new SystemSchedule<Tick>([Pass("c"), Pass("a"), Pass("b")]);

        schedule.Run(tick);
        schedule.Run(tick);

        Assert.That(tick.Ran, Is.EqualTo(new[] {"c", "a", "b", "c", "a", "b"}));
    }

    [Test]
    public void AReaderPlacedBeforeItsWriterIsReported() {
        var reader = Pass("mood", reads: [typeof(FakeComponent)]);
        var writer = Pass("burn", writes: [typeof(FakeComponent)]);

        var readerFirst = new SystemSchedule<Tick>([reader, Pass("other"), writer]);

        Assert.That(readerFirst.Findings, Has.Length.EqualTo(1));
        var finding = readerFirst.Findings[0];
        Assert.That(finding.Reader, Is.SameAs(reader.Declaration));
        Assert.That(finding.ReaderPosition, Is.EqualTo(0));
        Assert.That(finding.Writer, Is.SameAs(writer.Declaration));
        Assert.That(finding.WriterPosition, Is.EqualTo(2));
        Assert.That(finding.Type, Is.EqualTo(typeof(FakeComponent)));

        var writerFirst = new SystemSchedule<Tick>([writer, Pass("other"), reader]);

        Assert.That(writerFirst.Findings, Is.Empty);
    }

    [Test]
    public void AReaderIsReportedAgainstEveryLaterWriter() {
        var schedule = new SystemSchedule<Tick>([
            Pass("first-writer", writes: [typeof(FakeComponent)]),
            Pass("reader", reads: [typeof(FakeComponent), typeof(OtherComponent)]),
            Pass("second-writer", writes: [typeof(FakeComponent)]),
            Pass("third-writer", writes: [typeof(FakeComponent), typeof(OtherComponent)]),
        ]);

        Assert.That(schedule.Findings, Has.Length.EqualTo(3));
        Assert.That(schedule.Findings[0].WriterPosition, Is.EqualTo(2));
        Assert.That(schedule.Findings[1].WriterPosition, Is.EqualTo(3));
        Assert.That(schedule.Findings[1].Type, Is.EqualTo(typeof(FakeComponent)));
        Assert.That(schedule.Findings[2].Type, Is.EqualTo(typeof(OtherComponent)));
    }

    [Test]
    public void AReadModifyWriteSystemIsReportedAgainstALaterWriter() {
        var update = Pass("update", reads: [typeof(FakeComponent)], writes: [typeof(FakeComponent)]);
        var later = Pass("later", writes: [typeof(FakeComponent)]);

        var schedule = new SystemSchedule<Tick>([update, later]);

        Assert.That(schedule.Findings, Has.Length.EqualTo(1));
        Assert.That(schedule.Findings[0].Reader, Is.SameAs(update.Declaration));
        Assert.That(schedule.Findings[0].Writer, Is.SameAs(later.Declaration));
    }

    [Test]
    public void WritersThatDoNotReadAreNotReportedAgainstEachOther() {
        var schedule = new SystemSchedule<Tick>([
            Pass("first-emitter", writes: [typeof(FakeComponent)]),
            Pass("second-emitter", writes: [typeof(FakeComponent)]),
        ]);

        Assert.That(schedule.Findings, Is.Empty);
    }

    [Test]
    public void TheObserverSeesEachSystemOncePerRunWithWhatItVisited() {
        var observer = new RecordingObserver();
        var schedule = new SystemSchedule<Tick>([Pass("a", visited: 3), Pass("b", visited: 7)], observer);

        schedule.Run(new Tick());

        Assert.That(observer.Calls, Is.EqualTo(new[] {"start 0 a", "end 0 a 3", "start 1 b", "end 1 b 7"}));
    }

    [Test]
    public void AThrowingSystemStopsTheRunWithoutAFinishedCall() {
        var tick = new Tick();
        var observer = new RecordingObserver();
        var schedule = new SystemSchedule<Tick>([Pass("a"), new ThrowingSystem(), Pass("b")], observer);

        Assert.Throws<InvalidOperationException>(() => schedule.Run(tick));

        Assert.That(tick.Ran, Is.EqualTo(new[] {"a"}));
        Assert.That(observer.Calls, Is.EqualTo(new[] {"start 0 a", "end 0 a 0", "start 1 throws"}));
    }

    [Test]
    public void ExposesTheDeclarationsInRunOrder() {
        var schedule = new SystemSchedule<Tick>([Pass("b"), Pass("a")]);

        Assert.That(schedule.Count, Is.EqualTo(2));
        Assert.That(schedule.Systems[0].Name, Is.EqualTo("b"));
        Assert.That(schedule.Systems[1].Name, Is.EqualTo("a"));
    }

    [Test]
    public void RefusesTheSameSystemTwice() {
        var system = Pass("a");

        Assert.Throws<ArgumentException>(() => new SystemSchedule<Tick>([system, system]));
    }

    [Test]
    public void RefusesTwoSystemsWithOneName() {
        Assert.Throws<ArgumentException>(() => new SystemSchedule<Tick>([Pass("a"), Pass("a")]));
    }

    [Test]
    public void ADeclarationMayReadAndWriteATypeButNotListItTwiceInOneList() {
        Assert.DoesNotThrow(() => SystemDeclaration.Create("a", [typeof(FakeComponent)], [typeof(FakeComponent)]));

        Assert.Throws<ArgumentException>(() => SystemDeclaration.Create("a", [typeof(FakeComponent), typeof(FakeComponent)], []));
        Assert.Throws<ArgumentException>(() => SystemDeclaration.Create("a", [], [typeof(FakeComponent), typeof(FakeComponent)]));
    }

    [Test]
    public void ADeclarationRefusesABlankName() {
        Assert.Throws<ArgumentException>(() => SystemDeclaration.Create(" ", [], []));
    }
}
