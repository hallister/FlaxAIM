using FlaxAIM.Triggers;
using NUnit.Framework;
using S = FlaxAIM.State.EnhancedInputActionState;

namespace FlaxAIM.Tests;

/// <summary>
/// Triggers on their own, fed magnitudes directly. <see cref="EvaluationTests"/> covers them inside a processor.
/// </summary>
[TestFixture]
public class TriggerTests
{
    private static S Eval(InputTrigger trigger, float magnitude, float deltaTime = 0.1f) => trigger.Evaluate(null, null, deltaTime, magnitude);

    [Test]
    public void BaseTrigger_IsTriggeredWhileActuated()
    {
        var down = new InputTrigger().CreateInstance();

        Assert.That(Eval(down, 1f), Is.EqualTo(S.Triggered));
        Assert.That(Eval(down, 1f), Is.EqualTo(S.Triggered));
        Assert.That(Eval(down, 0.3f), Is.EqualTo(S.None), "Below the 0.5 actuation threshold");
    }

    [Test]
    public void Pressed_TriggersOnlyOnTheFrameItIsActuated()
    {
        var pressed = new TriggerPressed().CreateInstance();

        Assert.That(Eval(pressed, 1f), Is.EqualTo(S.Triggered));
        Assert.That(Eval(pressed, 1f), Is.EqualTo(S.None), "Still held");
        Assert.That(Eval(pressed, 0f), Is.EqualTo(S.None));
        Assert.That(Eval(pressed, 1f), Is.EqualTo(S.Triggered), "Pressed again");
    }

    [Test]
    public void Hold_IsOngoingUntilTheHoldTimeThenTriggers()
    {
        var hold = new TriggerHold { HoldTimeThreshold = 0.25f }.CreateInstance();

        Assert.That(Eval(hold, 1f), Is.EqualTo(S.Ongoing));
        Assert.That(Eval(hold, 1f), Is.EqualTo(S.Ongoing));
        Assert.That(Eval(hold, 1f), Is.EqualTo(S.Triggered));
        Assert.That(Eval(hold, 0f), Is.EqualTo(S.None));
        Assert.That(Eval(hold, 1f), Is.EqualTo(S.Ongoing), "Timer restarts after release");
    }

    [Test]
    public void Instances_DontShareState()
    {
        var template = new TriggerHold { HoldTimeThreshold = 0.25f };
        var first = template.CreateInstance();
        var second = template.CreateInstance();

        Eval(first, 1f);
        Eval(first, 1f);

        Assert.That(Eval(first, 1f), Is.EqualTo(S.Triggered));
        Assert.That(Eval(second, 1f), Is.EqualTo(S.Ongoing));
        Assert.That(first, Is.Not.SameAs(template));
    }

    [Test]
    public void CreateInstance_ResetsStateCopiedFromTheTemplate()
    {
        var template = new TriggerHold { HoldTimeThreshold = 0.25f };
        Eval(template, 1f);
        Eval(template, 1f);

        var copy = template.CreateInstance();

        Assert.That(Eval(copy, 1f), Is.EqualTo(S.Ongoing));
    }

    [Test]
    public void Chord_IsImplicit_OthersAreExplicit()
    {
        Assert.That(new TriggerChord().TriggerType, Is.EqualTo(TriggerType.Implicit));
        Assert.That(new TriggerHold().TriggerType, Is.EqualTo(TriggerType.Explicit));
        Assert.That(new TriggerPressed().TriggerType, Is.EqualTo(TriggerType.Explicit));
    }
}
