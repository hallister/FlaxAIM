using FlaxAIM.Modifiers;
using FlaxEngine;
using NUnit.Framework;

namespace FlaxAIM.Tests;

[TestFixture]
public class ModifierTests
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void DeadZone1D_ZeroesBelowLowerAndRemapsTheRest()
    {
        var deadZone = new ModifierDeadZone { LowerThreshold = 0.2f, UpperThreshold = 1f };

        Assert.That(deadZone.Modify(0.1f), Is.Zero);
        Assert.That(deadZone.Modify(0.6f), Is.EqualTo(0.5f).Within(Tolerance));
        Assert.That(deadZone.Modify(-0.6f), Is.EqualTo(-0.5f).Within(Tolerance));
        Assert.That(deadZone.Modify(1f), Is.EqualTo(1f).Within(Tolerance));
    }

    [Test]
    public void DeadZone_ValuesAboveUpperBecomeFullInput()
    {
        var deadZone = new ModifierDeadZone { LowerThreshold = 0.2f, UpperThreshold = 0.8f };

        Assert.That(deadZone.Modify(0.9f), Is.EqualTo(1f).Within(Tolerance));
        Assert.That(deadZone.Modify(new Float2(0f, 0.95f)).Y, Is.EqualTo(1f).Within(Tolerance));
    }

    [Test]
    public void DeadZoneRadial_WorksOnTheVectorLength()
    {
        var deadZone = new ModifierDeadZone { LowerThreshold = 0.2f, UpperThreshold = 1f };

        // Each component is below the lower threshold, but the length (0.212) isn't
        var result = deadZone.Modify(new Float2(0.15f, 0.15f));

        Assert.That(result.X, Is.GreaterThan(0f));
        Assert.That(result.X, Is.EqualTo(result.Y).Within(Tolerance), "Direction is kept");
        Assert.That(deadZone.Modify(new Float2(0.1f, 0.1f)), Is.EqualTo(Float2.Zero));
    }

    [Test]
    public void DeadZoneAxial_WorksPerComponent()
    {
        var deadZone = new ModifierDeadZone { Type = DeadZoneType.Axial, LowerThreshold = 0.2f, UpperThreshold = 1f };

        var result = deadZone.Modify(new Float3(0.15f, 0.6f, -1f));

        Assert.That(result.X, Is.Zero);
        Assert.That(result.Y, Is.EqualTo(0.5f).Within(Tolerance));
        Assert.That(result.Z, Is.EqualTo(-1f).Within(Tolerance));
    }

    [Test]
    public void DeadZone_WithNoRange_IsAStep()
    {
        var deadZone = new ModifierDeadZone { LowerThreshold = 0.5f, UpperThreshold = 0.5f };

        Assert.That(deadZone.Modify(0.4f), Is.Zero);
        Assert.That(deadZone.Modify(0.5f), Is.EqualTo(1f));
    }

    [Test]
    public void Scale_ScalesEachComponent()
    {
        var scale = new ModifierScale { ScaleX = 2f, ScaleY = -1f, ScaleZ = 0.5f };

        Assert.That(scale.Modify(3f), Is.EqualTo(6f));
        Assert.That(scale.Modify(new Float2(1f, 1f)), Is.EqualTo(new Float2(2f, -1f)));
        Assert.That(scale.Modify(new Float3(1f, 1f, 1f)), Is.EqualTo(new Float3(2f, -1f, 0.5f)));
    }

    [Test]
    public void OverloadsThatArentOverridden_ForwardUpADimension()
    {
        var doubler = new Float3Only();

        Assert.That(doubler.Modify(2f), Is.EqualTo(4f));
        Assert.That(doubler.Modify(new Float2(1f, 2f)), Is.EqualTo(new Float2(2f, 4f)));
    }

    [Test]
    public void AModifierThatOverridesNothing_PassesValuesThrough()
    {
        var nothing = new NoOverrides();

        Assert.That(nothing.Modify(0.3f), Is.EqualTo(0.3f));
        Assert.That(nothing.Modify(new Float2(1f, 2f)), Is.EqualTo(new Float2(1f, 2f)));
    }

    private sealed class Float3Only : InputModifier
    {
        public override string Name => "Double";
        public override Float3 Modify(Float3 value) => value * 2f;
    }

    private sealed class NoOverrides : InputModifier
    {
        public override string Name => "Nothing";
    }
}
