using FlaxAIM.Triggers;
using FlaxEngine;
using NUnit.Framework;
using static FlaxAIM.Tests.TestInput;
using S = FlaxAIM.State.EnhancedInputActionState;

namespace FlaxAIM.Tests;

/// <summary>
/// Adding and removing contexts, priority and input consumption, and per-player device settings.
/// </summary>
[TestFixture]
public class ContextTests : InputTestBase
{
    private InputAction _jump;
    private InputAction _confirm;
    private InputMappingContext _gameplay;
    private InputMappingContext _menu;

    [SetUp]
    public void SetUpContexts()
    {
        _jump = Action("Jump");
        _confirm = Action("Confirm");
        _gameplay = Context("Gameplay", Map(_jump, Key(KeyboardKeys.Spacebar), Button(GamepadButton.A)));
        _menu = Context("Menu", Map(_confirm, Key(KeyboardKeys.Spacebar)));
    }

    [Test]
    public void HigherPriorityContext_ConsumesKeysItShares()
    {
        Input.AddInputContext(_gameplay);
        Input.AddInputContext(_menu, 10);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(Input.GetActionState(_confirm), Is.EqualTo(S.Triggered));
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));

        Input.RemoveInputContext(_menu);
        Tick();
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void ConsumedKeysDontBlockTheRestOfALowerBinding()
    {
        Input.AddInputContext(_gameplay);
        Input.AddInputContext(_menu, 10);

        Devices.Press(GamepadButton.A);
        Tick();

        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered), "Gamepad A isn't used by the menu");
    }

    [Test]
    public void ActionsThatDontConsumeInput_LetKeysThrough()
    {
        _confirm.ConsumeInput = false;
        Input.AddInputContext(_gameplay);
        Input.AddInputContext(_menu, 10);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionState(_confirm), Is.EqualTo(S.Triggered));
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void EqualPriority_MostRecentlyAddedWins()
    {
        Input.AddInputContext(_menu);
        Input.AddInputContext(_gameplay);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered));
        Assert.That(Input.GetActionState(_confirm), Is.EqualTo(S.None));
    }

    [Test]
    public void AddingAnActiveContextAgain_ChangesItsPriority()
    {
        Input.AddInputContext(_gameplay);
        Input.AddInputContext(_menu, 10);
        Input.AddInputContext(_gameplay, 20);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered));
        Assert.That(Input.GetActionState(_confirm), Is.EqualTo(S.None));
    }

    [Test]
    public void AddingSeveralContexts_SkipsNullsAndDuplicatesWithoutStopping()
    {
        Input.AddInputContext([_gameplay, null, _gameplay, _menu]);

        Assert.That(Input.HasInputContext(_gameplay), Is.True);
        Assert.That(Input.HasInputContext(_menu), Is.True);

        Input.RemoveInputContext([null, _gameplay, _menu]);

        Assert.That(Input.HasInputContext(_gameplay), Is.False);
        Assert.That(Input.HasInputContext(_menu), Is.False);
    }

    [Test]
    public void RemovingAContext_CompletesItsActiveActions()
    {
        var completed = 0;
        Input.BindAction(_jump, S.Completed, () => completed++);
        Input.AddInputContext(_gameplay);
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Input.RemoveInputContext(_gameplay);
        Tick();
        Tick();

        Assert.That(completed, Is.EqualTo(1));
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));
    }

    [Test]
    public void ChangingContextsInsideACallback_TakesEffectNextFrame()
    {
        var pause = Action("Pause");
        Input.AddInputContext(Context("Global", Map(pause, Key(KeyboardKeys.Escape))));
        Input.AddInputContext(_gameplay);
        Input.BindAction(pause, S.Started, () => Input.AddInputContext(_menu, 10));

        Devices.Press(KeyboardKeys.Escape);
        Assert.DoesNotThrow(() => Tick());

        Devices.ReleaseAll();
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Assert.That(Input.GetActionState(_confirm), Is.EqualTo(S.Triggered));
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));
    }

    [Test]
    public void AnActionInTwoContexts_IsEvaluatedOncePerFrame()
    {
        var completed = 0;
        Input.BindAction(_jump, S.Completed, () => completed++);
        Input.AddInputContext(_gameplay);
        Input.AddInputContext(Context("Extra", Map(_jump, Key(KeyboardKeys.Return))));

        Devices.Press(KeyboardKeys.Return);
        Tick();
        Devices.ReleaseAll();
        Tick();

        Assert.That(completed, Is.EqualTo(1));
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));
    }

    [Test]
    public void Disabling_WithdrawsMappingsAndClearsState()
    {
        Input.AddInputContext(_gameplay);
        Devices.Press(KeyboardKeys.Spacebar);
        Tick();

        Input.Disable();
        Tick();

        Assert.That(Devices.ActionMappings, Is.Empty);
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));

        Input.Enable();
        Tick();
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered), "Contexts are kept while disabled");
    }

    [Test]
    public void WithoutKeyboardAndMouse_OnlyGamepadBindingsWork()
    {
        Input.UseKeyboardAndMouse = false;
        Input.AddInputContext(_gameplay);

        Devices.Press(KeyboardKeys.Spacebar);
        Tick();
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));

        Devices.Press(GamepadButton.A);
        Tick();
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void MouseButtons_TriggerActions()
    {
        var fire = Action("Fire");
        Input.AddInputContext(Context("Combat", Map(fire, Mouse(MouseButton.Left))));
        var events = RecordEvents(fire);

        Devices.Press(MouseButton.Right);
        Tick();
        Assert.That(events, Is.Empty);

        Devices.Press(MouseButton.Left);
        Tick();
        Devices.Release(MouseButton.Left);
        Tick();
        Assert.That(events, Is.EqualTo(new[] { S.Started, S.Triggered, S.Completed }));
    }

    [Test]
    public void HigherPriorityContext_ConsumesMouseButtonsItShares()
    {
        var fire = Action("Fire");
        var select = Action("Select");
        Input.AddInputContext(Context("Combat", Map(fire, Mouse(MouseButton.Left))));
        Input.AddInputContext(Context("Menu", Map(select, Mouse(MouseButton.Left))), 10);

        Devices.Press(MouseButton.Left);
        Tick();

        Assert.That(Input.GetActionState(select), Is.EqualTo(S.Triggered));
        Assert.That(Input.GetActionState(fire), Is.EqualTo(S.None));
    }

    [Test]
    public void WithoutKeyboardAndMouse_MouseButtonsAreIgnored()
    {
        var fire = Action("Fire");
        Input.UseKeyboardAndMouse = false;
        Input.AddInputContext(Context("Combat", Map(fire, Mouse(MouseButton.Left), Button(GamepadButton.RightShoulder))));

        Devices.Press(MouseButton.Left);
        Tick();
        Assert.That(Input.GetActionState(fire), Is.EqualTo(S.None));

        Devices.Press(GamepadButton.RightShoulder);
        Tick();
        Assert.That(Input.GetActionState(fire), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void AGamepadIndex_OnlyReadsThatGamepad()
    {
        Input.Gamepad = InputGamepadIndex.Gamepad1;
        Input.AddInputContext(_gameplay);

        Devices.Press(GamepadButton.A, gamepad: 0);
        Tick();
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.None));

        Devices.Press(GamepadButton.A, gamepad: 1);
        Tick();
        Assert.That(Input.GetActionState(_jump), Is.EqualTo(S.Triggered));
    }

    [Test]
    public void AKeyHeldWhileItsActionMovesToAnotherContext_IsntPressedAgain()
    {
        // Interacting hands the player over to a seat whose context binds interact to the same key
        var interact = Action("Interact");
        var onFoot = Context("OnFoot", Map(interact, Key(KeyboardKeys.F, new TriggerPressed())));
        var seated = Context("Seated", Map(interact, Key(KeyboardKeys.F, new TriggerPressed())));
        var presses = 0;
        Input.BindAction(interact, S.Triggered, () =>
        {
            presses++;
            var leaving = Input.HasInputContext(onFoot) ? onFoot : seated;
            Input.RemoveInputContext(leaving);
            Input.AddInputContext(leaving == onFoot ? seated : onFoot);
        });
        Input.AddInputContext(onFoot);

        Devices.Press(KeyboardKeys.F);
        Tick();
        Tick();
        Tick();
        Assert.That(presses, Is.EqualTo(1), "Holding F doesn't flip back and forth");
        Assert.That(Input.HasInputContext(seated), Is.True);

        Devices.ReleaseAll();
        Tick();
        Devices.Press(KeyboardKeys.F);
        Tick();
        Assert.That(presses, Is.EqualTo(2), "A new press still counts");
        Assert.That(Input.HasInputContext(onFoot), Is.True);
    }

    [Test]
    public void RemovingAndAddingContextsOutsideAnUpdate_KeepsHeldState()
    {
        var interact = Action("Interact");
        var onFoot = Context("OnFoot", Map(interact, Key(KeyboardKeys.F, new TriggerPressed())));
        var seated = Context("Seated", Map(interact, Key(KeyboardKeys.F, new TriggerPressed())));
        var presses = 0;
        Input.BindAction(interact, S.Triggered, () => presses++);
        Input.AddInputContext(onFoot);
        Devices.Press(KeyboardKeys.F);
        Tick();

        // Two rebuilds between updates: the first has no binding for interact at all
        Input.RemoveInputContext(onFoot);
        Input.AddInputContext(seated);
        Tick();

        Assert.That(presses, Is.EqualTo(1));
    }

    [Test]
    public void AHoldKeepsItsTimeThroughAContextChange()
    {
        var charge = Action("Charge");
        Input.AddInputContext(Context("Gameplay", Map(charge, Key(KeyboardKeys.E, new TriggerHold { HoldTimeThreshold = 0.5f }))));
        Devices.Press(KeyboardKeys.E);
        Advance(0.3f);

        Input.AddInputContext(_menu, 10);
        Advance(0.3f);

        Assert.That(Input.GetActionState(charge), Is.EqualTo(S.Triggered), "0.6 s held in all, across the change");
    }

    [Test]
    public void ABindingToOtherKeys_StartsFresh()
    {
        var interact = Action("Interact");
        var onFoot = Context("OnFoot", Map(interact, Key(KeyboardKeys.F, new TriggerPressed())));
        var seated = Context("Seated", Map(interact, Key(KeyboardKeys.G, new TriggerPressed())));
        var presses = 0;
        Input.BindAction(interact, S.Triggered, () => presses++);
        Input.AddInputContext(onFoot);
        Devices.Press(KeyboardKeys.F);
        Devices.Press(KeyboardKeys.G);
        Tick();

        Input.RemoveInputContext(onFoot);
        Input.AddInputContext(seated);
        Tick();

        Assert.That(presses, Is.EqualTo(2), "G wasn't bound before, so holding it is a new press for the seat");
    }
}
