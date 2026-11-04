#nullable enable
using System;
using Capsicum.Events;
using NUnit.Framework;

namespace Capsicum.Test;

[TestFixture]
public class EventBufferTests {
    private readonly record struct Died(int Monster);

    [Test]
    public void AReaderSeesEventsEmittedBeforeItReadsInTheSameRun() {
        var buffer = new EventBuffer<Died>(4);
        var reader = buffer.CreateReader();
        buffer.TryEmit(new Died(1));
        buffer.TryEmit(new Died(2));

        Assert.That(reader.Read().ToArray(), Is.EqualTo(new[] {new Died(1), new Died(2)}));

        buffer.TryEmit(new Died(3));

        Assert.That(reader.Read().ToArray(), Is.EqualTo(new[] {new Died(3)}));
        Assert.That(reader.Read().Length, Is.EqualTo(0));
    }

    [Test]
    public void EachReaderKeepsItsOwnPosition() {
        var buffer = new EventBuffer<Died>(4);
        var early = buffer.CreateReader();
        var late = buffer.CreateReader();
        buffer.TryEmit(new Died(1));
        early.Read();
        buffer.TryEmit(new Died(2));

        Assert.That(late.Read().ToArray(), Is.EqualTo(new[] {new Died(1), new Died(2)}));
        Assert.That(early.Read().ToArray(), Is.EqualTo(new[] {new Died(2)}));
    }

    [Test]
    public void AFullBufferRefusesAndCountsTheRefusal() {
        var buffer = new EventBuffer<Died>(2);

        Assert.That(buffer.TryEmit(new Died(1)), Is.True);
        Assert.That(buffer.TryEmit(new Died(2)), Is.True);
        Assert.That(buffer.TryEmit(new Died(3)), Is.False);
        Assert.That(buffer.TryEmit(new Died(4)), Is.False);

        Assert.That(buffer.Count, Is.EqualTo(2));
        Assert.That(buffer.Clear().Refused, Is.EqualTo(2));
    }

    [Test]
    public void ClearCountsEventsSomeReaderNeverRead() {
        var buffer = new EventBuffer<Died>(4);
        var keeping = buffer.CreateReader();
        var lagging = buffer.CreateReader();
        buffer.TryEmit(new Died(1));
        lagging.Read();
        buffer.TryEmit(new Died(2));
        buffer.TryEmit(new Died(3));
        keeping.Read();

        Assert.That(buffer.Clear(), Is.EqualTo(new EventBufferTally(Emitted: 3, Unread: 2, Refused: 0)));
    }

    [Test]
    public void ClearCountsNothingUnreadWhenEveryReaderCaughtUp() {
        var buffer = new EventBuffer<Died>(4);
        var reader = buffer.CreateReader();
        buffer.TryEmit(new Died(1));
        reader.Read();

        Assert.That(buffer.Clear(), Is.EqualTo(new EventBufferTally(Emitted: 1, Unread: 0, Refused: 0)));
    }

    [Test]
    public void WithNoReaderEveryEventIsUnread() {
        var buffer = new EventBuffer<Died>(4);
        buffer.TryEmit(new Died(1));
        buffer.TryEmit(new Died(2));

        Assert.That(buffer.Clear().Unread, Is.EqualTo(2));
    }

    [Test]
    public void ClearEmptiesTheBufferAndRewindsReaders() {
        var buffer = new EventBuffer<Died>(2);
        var reader = buffer.CreateReader();
        buffer.TryEmit(new Died(1));
        buffer.TryEmit(new Died(2));
        buffer.TryEmit(new Died(3));
        reader.Read();

        buffer.Clear();
        buffer.TryEmit(new Died(4));

        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(reader.Read().ToArray(), Is.EqualTo(new[] {new Died(4)}));
        Assert.That(buffer.Clear(), Is.EqualTo(new EventBufferTally(Emitted: 1, Unread: 0, Refused: 0)));
    }

    [Test]
    public void CapacityMustBePositive() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventBuffer<Died>(0));
    }
}
