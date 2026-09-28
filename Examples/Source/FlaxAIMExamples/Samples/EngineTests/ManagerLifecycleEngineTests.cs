using System.Collections;
using System.Linq;
using FlaxAIM.State;
using FlaxEngine;

namespace FlaxAIM.Samples.EngineTests;

/// <summary>
/// <see cref="InputManager"/> as a script in a scene: enabled, updated, disabled and destroyed by the engine.
/// </summary>
public class ManagerLifecycleEngineTests : EngineTestFixture
{
    private static int PluginMappingCount() =>
        Input.ActionMappings.Count(c => c.Name?.StartsWith(VirtualInputRegistry.NamePrefix) == true);

    [EngineTest]
    public IEnumerator AManagerInASceneUpdatesItself()
    {
        var devices = UseScriptedInput();
        var jump = ActionAsset("Jump");
        var manager = CreateManager(inScene: true);
        yield return null;

        manager.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar))));
        devices.Press(KeyboardKeys.Spacebar);
        yield return null;
        yield return null;

        Check.Equal(EnhancedInputActionState.Triggered, manager.GetActionState(jump.Instance), "Jump state");
    }

    [EngineTest]
    public IEnumerator DisablingTheScriptWithdrawsItsInputs()
    {
        var manager = CreateManager(inScene: true);
        yield return null;
        manager.AddInputContext(Context("Gameplay", Map(ActionAsset("Jump"), Key(KeyboardKeys.Spacebar))));
        Check.Equal(1, PluginMappingCount(), "Bindings while enabled");

        manager.Enabled = false;
        yield return null;
        Check.Equal(0, PluginMappingCount(), "Bindings while disabled");

        manager.Enabled = true;
        yield return null;
        Check.Equal(1, PluginMappingCount(), "Bindings after enabling again");
    }

    [EngineTest]
    public IEnumerator DestroyingTheManagerRestoresTheTables()
    {
        var before = Input.ActionMappings.Select(c => c.Name).ToArray();
        var manager = CreateManager(inScene: true);
        yield return null;
        manager.AddInputContext(Context("Gameplay", Map(ActionAsset("Jump"), Key(KeyboardKeys.Spacebar))));

        FlaxEngine.Object.DestroyNow(manager.Actor);
        yield return null;

        Check.That(Input.ActionMappings.Select(c => c.Name).SequenceEqual(before), "Action mappings match the ones before the manager");
    }

    [EngineTest]
    public IEnumerator CallbacksOfDestroyedScriptsAreDropped()
    {
        var devices = UseScriptedInput();
        var jump = ActionAsset("Jump");
        var manager = CreateManager(inScene: true);
        var listenerActor = Track(new EmptyActor { Name = "EngineTest_Listener", Parent = Runner.Actor.Scene });
        var listener = listenerActor.AddScript<JumpListener>();
        yield return null;

        manager.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar))));
        manager.BindAction(jump.Instance, EnhancedInputActionState.Triggered, listener.OnJump);

        // Destroyed the usual way: the engine finishes destroying it at the end of the frame
        FlaxEngine.Object.Destroy(listenerActor);
        yield return null;
        yield return null;
        Check.That(!listener, "Listener script destroyed");

        devices.Press(KeyboardKeys.Spacebar);
        yield return null;
        yield return null;

        Check.Equal(0, listener.Calls, "Calls after the listener was destroyed");
    }

    [EngineTest]
    public void ScriptsDestroyedDirectlyAreDroppedToo()
    {
        var devices = UseScriptedInput();
        var jump = ActionAsset("Jump");
        var manager = CreateManager();
        manager.AddInputContext(Context("Gameplay", Map(jump, Key(KeyboardKeys.Spacebar))));
        var listener = Track(new EmptyActor { Name = "EngineTest_Listener" }).AddScript<JumpListener>();
        manager.BindAction(jump.Instance, EnhancedInputActionState.Triggered, listener.OnJump);

        FlaxEngine.Object.DestroyNow(listener);
        Check.That(!listener, "Listener script destroyed");
        devices.Press(KeyboardKeys.Spacebar);
        Tick(manager);

        Check.Equal(0, listener.Calls, "Calls after the listener was destroyed");
    }
}

/// <summary>
/// A script that records jumps, for <see cref="ManagerLifecycleEngineTests"/>.
/// </summary>
public class JumpListener : Script
{
    public int Calls;
    public void OnJump() => Calls++;
}
