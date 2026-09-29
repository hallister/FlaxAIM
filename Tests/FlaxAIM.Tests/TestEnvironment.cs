using System.Collections.Generic;
using System.Linq;
using FlaxAIM.State;
using FlaxEngine;
using NUnit.Framework;

namespace FlaxAIM.Tests;

/// <summary>
/// Replaces the engine-backed pieces (logging, tag names, asset lookup) before any test runs.
/// </summary>
[SetUpFixture]
public class TestEnvironment
{
    public static readonly List<(LogType Type, string Message)> Logs = [];

    public static IEnumerable<string> Warnings => Logs.Where(l => l.Type == LogType.Warning).Select(l => l.Message);

    [OneTimeSetUp]
    public void SetUp()
    {
        InputLog.Output = (type, message) => Logs.Add((type, message));
        InputLog.TagName = tag => $"Tag{tag.Index}";

        // Actions in tests aren't assets, so they're identified by their own ID
        ActionIdentity.AssetLookup = _ => null;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        InputLog.Output = null;
    }
}

/// <summary>
/// Base for tests that drive an <see cref="InputProcessor"/>: each test gets a fresh <see cref="FakeVirtualInput"/>
/// and an enabled processor.
/// </summary>
public abstract class InputTestBase
{
    protected FakeVirtualInput Devices;
    protected InputProcessor Input;

    protected const float Frame = 1f / 60f;

    [SetUp]
    public void SetUpInput()
    {
        TestEnvironment.Logs.Clear();
        Devices = new FakeVirtualInput();
        VirtualInputRegistry.Backend = Devices;

        Input = new InputProcessor();
        Input.Enable();
    }

    [TearDown]
    public void TearDownInput()
    {
        Input.Disable();
        VirtualInputRegistry.Backend = null;
    }

    /// <summary>
    /// Runs one frame of the processor.
    /// </summary>
    protected void Tick(float deltaTime = Frame) => Input.Update(deltaTime);

    /// <summary>
    /// Runs frames covering <paramref name="seconds"/>.
    /// </summary>
    protected void Advance(float seconds, float deltaTime = Frame)
    {
        for (var elapsed = 0f; elapsed < seconds - 0.0001f; elapsed += deltaTime) Tick(deltaTime);
    }

    /// <summary>
    /// Records every event the action raises, in order.
    /// </summary>
    protected List<EnhancedInputActionState> RecordEvents(InputAction action)
    {
        var events = new List<EnhancedInputActionState>();
        foreach (var state in new[] { EnhancedInputActionState.Started, EnhancedInputActionState.Ongoing, EnhancedInputActionState.Triggered, EnhancedInputActionState.Completed, EnhancedInputActionState.Canceled })
        {
            Input.BindAction(action, state, () => events.Add(state));
        }
        return events;
    }
}
