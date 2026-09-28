using FlaxAIM.State;
using FlaxAIM.Triggers;
using NUnit.Framework;
using S = FlaxAIM.State.EnhancedInputActionState;

namespace FlaxAIM.Tests;

[TestFixture]
public class StateMachineTests
{
    [TestCase(S.None, S.Ongoing, TriggerEvent.Started | TriggerEvent.Ongoing)]
    [TestCase(S.None, S.Triggered, TriggerEvent.Started | TriggerEvent.Triggered)]
    [TestCase(S.Ongoing, S.Ongoing, TriggerEvent.Ongoing)]
    [TestCase(S.Ongoing, S.Triggered, TriggerEvent.Triggered)]
    [TestCase(S.Ongoing, S.None, TriggerEvent.Canceled)]
    [TestCase(S.Triggered, S.Triggered, TriggerEvent.Triggered)]
    [TestCase(S.Triggered, S.Ongoing, TriggerEvent.Ongoing)]
    [TestCase(S.Triggered, S.None, TriggerEvent.Completed)]
    [TestCase(S.None, S.None, TriggerEvent.None)]
    public void Transition_RaisesUnrealEvents(S from, S to, TriggerEvent expected)
    {
        var tracker = new ActionStateTracker();
        tracker.AdvanceStateMachine(from);

        tracker.AdvanceStateMachine(to);

        Assert.That(tracker.Events, Is.EqualTo(expected));
        Assert.That(tracker.TriggerState, Is.EqualTo(to));
    }

    [TestCase(S.Started)]
    [TestCase(S.Completed)]
    [TestCase(S.Canceled)]
    public void TransitionStatesFromTriggers_AreTreatedAsNone(S evaluation)
    {
        var tracker = new ActionStateTracker();
        tracker.AdvanceStateMachine(S.Triggered);

        tracker.AdvanceStateMachine(evaluation);

        Assert.That(tracker.TriggerState, Is.EqualTo(S.None));
        Assert.That(tracker.Events, Is.EqualTo(TriggerEvent.Completed));
    }

    [Test]
    public void MoveToNextFrame_ClearsEventsAndKeepsTheMagnitude()
    {
        var tracker = new ActionStateTracker { CurrentMagnitude = 0.7f };
        tracker.AdvanceStateMachine(S.Triggered);

        tracker.MoveToNextFrame();

        Assert.That(tracker.Events, Is.EqualTo(TriggerEvent.None));
        Assert.That(tracker.PreviousMagnitude, Is.EqualTo(0.7f));
        Assert.That(tracker.CurrentMagnitude, Is.Zero);
        Assert.That(tracker.TriggerState, Is.EqualTo(S.Triggered));
    }
}
