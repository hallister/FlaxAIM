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
}
