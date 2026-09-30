using System.Collections.Generic;
using FlaxAIM.State;
using FlaxAIM.Triggers;
using FlaxEngine;

namespace FlaxAIM.Samples.EngineTests;

/// <summary>
/// Actions, contexts and configs as real (virtual) JSON assets: resolving references and identifying actions by asset.
/// </summary>
public class AssetEngineTests : EngineTestFixture
{
    [EngineTest]
    public void ContextAssetsResolveTheirActionAssets()
    {
        var devices = UseScriptedInput();
        var jump = ActionAsset("Jump");
        var context = Asset(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar))));
        var manager = CreateManager();

        manager.AddInputContext(context.Instance);
        devices.Press(KeyboardKeys.Spacebar);
        Tick(manager);

        Check.Equal(EnhancedInputActionState.Triggered, manager.GetActionState(jump.Instance), "Jump state");
    }

    [EngineTest]
    public void DuplicatedActionAssetsStayDistinct()
    {
        var devices = UseScriptedInput();
        // A duplicated asset copies the serialized ID; the asset IDs still differ
        var original = new InputAction { Name = "Jump" };
        var duplicate = new InputAction { Name = "Crouch", ID = original.ID };
        var jump = Asset(original);
        var crouch = Asset(duplicate);
        var manager = CreateManager();
        manager.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar)), Map(crouch, Key(KeyboardKeys.C))));

        devices.Press(KeyboardKeys.Spacebar);
        Tick(manager);

        Check.Equal(EnhancedInputActionState.Triggered, manager.GetActionState(jump.Instance), "Jump");
        Check.Equal(EnhancedInputActionState.None, manager.GetActionState(crouch.Instance), "Crouch");
    }

    [EngineTest]
    public void BindingBeforeTheContextIsAddedFindsTheAsset()
    {
        var devices = UseScriptedInput();
        var jump = ActionAsset("Jump");
        var manager = CreateManager();
        var calls = 0;

        // The binding resolves the action's identity by searching loaded content; the context later registers it
        // from its asset reference. Both must agree.
        manager.BindAction(jump.Instance, EnhancedInputActionState.Triggered, () => calls++);
        manager.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar))));
        devices.Press(KeyboardKeys.Spacebar);
        Tick(manager);

        Check.Equal(1, calls, "Callbacks");
    }

    [EngineTest]
    public void ChordAssetReferencesResolve()
    {
        var devices = UseScriptedInput();
        var sprint = ActionAsset("Sprint");
        var slide = ActionAsset("Slide");
        var manager = CreateManager();
        manager.AddInputContext(Context("Gameplay",
            Map(slide, Key(KeyboardKeys.C, new TriggerChord { ChordAction = sprint })),
            Map(sprint, Key(KeyboardKeys.Shift))));

        devices.Press(KeyboardKeys.C);
        Tick(manager);
        Check.Equal(EnhancedInputActionState.None, manager.GetActionState(slide.Instance), "Slide without the chord");

        devices.Press(KeyboardKeys.Shift);
        Tick(manager);
        Check.Equal(EnhancedInputActionState.Triggered, manager.GetActionState(slide.Instance), "Slide with the chord");
    }

    [EngineTest]
    public void InputConfigSetBindsEveryAction()
    {
        var devices = UseScriptedInput();
        var jumpTag = T("Input.Jump");
        var crouchTag = T("Input.Crouch");
        var jump = ActionAsset("Jump");
        var crouch = ActionAsset("Crouch");
        var set = Asset(new InputConfigSet
        {
            Configs =
            [
                new InputConfig { InputAction = jump, InputTag = jumpTag },
                new InputConfig { InputAction = crouch, InputTag = crouchTag },
            ],
        });
        var manager = CreateManager();
        manager.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar)), Map(crouch, Key(KeyboardKeys.C))));
        var received = new List<Tag>();

        using var handle = manager.BindActions(set.Instance, EnhancedInputActionState.Started, received.Add);
        devices.Press(KeyboardKeys.Spacebar, KeyboardKeys.C);
        Tick(manager);

        Check.That(received.Contains(jumpTag) && received.Contains(crouchTag), $"Tags received: {string.Join(", ", received)}");
        Check.That(manager.GetActionValue(crouchTag).Digital, "Value by tag");
    }
}
