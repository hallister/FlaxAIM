using System;
using System.Collections.Generic;
using FlaxAIM.Triggers;
using FlaxEngine;
using FlaxEngine.Json;

namespace FlaxAIM.Samples.EngineTests;

public sealed class EngineTestFailure(string message) : Exception(message);

public sealed class EngineTestSkipped(string message) : Exception(message);

/// <summary>
/// Assertions for engine tests. A failed check throws, which fails the test.
/// </summary>
public static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new EngineTestFailure(message);
    }

    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new EngineTestFailure($"{what}: expected {expected}, got {actual}");
    }

    public static void Near(float expected, float actual, string what, float tolerance = 0.001f)
    {
        if (!(Math.Abs(expected - actual) <= tolerance)) throw new EngineTestFailure($"{what}: expected {expected}, got {actual}");
    }

    public static T NotNull<T>(T value, string what) where T : class
    {
        if (value == null) throw new EngineTestFailure($"{what} is null");
        return value;
    }

    public static void Skip(string reason) => throw new EngineTestSkipped(reason);
}

/// <summary>
/// Base for engine test fixtures. A new instance runs each test; everything created through its helpers
/// (actors, virtual assets, scripting objects) is destroyed afterwards.
/// <para>
/// Managers made with <see cref="CreateManager"/> aren't in a scene by default: nothing updates them, so tests advance
/// them with <see cref="Tick"/>. Pass <c>inScene: true</c> to have the engine enable and update the manager.
/// </para>
/// </summary>
public abstract class EngineTestFixture
{
    public EngineTestRunner Runner { get; internal set; }

    /// <summary>
    /// FlaxAIM log output during the test.
    /// </summary>
    public readonly List<(LogType Type, string Message)> Logs = [];

    private readonly List<Actor> _actors = [];
    private readonly List<FlaxEngine.Object> _objects = [];
    private readonly List<Asset> _assets = [];
    private readonly List<Action> _deferred = [];

    /// <summary>
    /// A tag under <c>FlaxAIMTest.</c>, added to the tag list if it doesn't exist yet.
    /// </summary>
    public static Tag T(string name) => Tags.Get("FlaxAIMTest." + name);

    internal void Cleanup()
    {
        // Deferred actions first, newest first (e.g. restoring the input backend before managers are destroyed)
        for (var i = _deferred.Count - 1; i >= 0; i--)
        {
            _deferred[i]();
        }
        _deferred.Clear();

        // Actors first: their managers still reference the assets
        for (var i = _actors.Count - 1; i >= 0; i--)
        {
            if (_actors[i]) FlaxEngine.Object.Destroy(_actors[i]);
        }
        foreach (var obj in _objects)
        {
            if (obj) FlaxEngine.Object.Destroy(obj);
        }
        foreach (var asset in _assets)
        {
            if (asset) FlaxEngine.Object.Destroy(asset);
        }
        _actors.Clear();
        _objects.Clear();
        _assets.Clear();
    }

    /// <summary>
    /// Runs an action after the test, even when it fails.
    /// </summary>
    protected void Defer(Action action) => _deferred.Add(action);

    /// <summary>
    /// Destroys the object after the test.
    /// </summary>
    protected TObject Track<TObject>(TObject obj) where TObject : FlaxEngine.Object
    {
        if (obj is Actor actor) _actors.Add(actor);
        else if (obj is Asset asset) _assets.Add(asset);
        else _objects.Add(obj);
        return obj;
    }

    // ---- Assets ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Wraps an instance in a virtual (runtime-only) JSON asset, so it can be used wherever an asset reference is expected.
    /// </summary>
    protected JsonAssetReference<TData> Asset<TData>(TData instance) where TData : class
    {
        var asset = Track(Content.CreateVirtualAsset<JsonAsset>());
        if (asset == null) throw new EngineTestFailure("Could not create a virtual JSON asset.");
        if (asset.Init(typeof(TData).FullName, JsonSerializer.Serialize(instance)))
        {
            throw new EngineTestFailure($"Could not initialize a virtual {typeof(TData).Name} asset.");
        }

        // Keep the exact instance (the one the test holds) rather than a deserialized copy
        asset.SetInstance(instance);
        return new JsonAssetReference<TData>(asset);
    }

    /// <summary>
    /// An input action in a virtual asset.
    /// </summary>
    protected JsonAssetReference<InputAction> ActionAsset(string name, InputActionType type = InputActionType.Digital)
    {
        return Asset(new InputAction { Name = name, ActionType = type });
    }

    protected static InputMappingEntry Key(KeyboardKeys key, params InputTrigger[] triggers) => new()
    {
        Key = key,
        Triggers = [..triggers],
    };

    protected static InputMappingEntry Button(GamepadButton button) => new()
    {
        GamepadButton = button,
    };

    /// <summary>
    /// Maps an action asset (resolved through the asset, like a context loaded from content).
    /// </summary>
    protected static InputActionEntry Map(JsonAssetReference<InputAction> action, params InputMappingEntry[] bindings) => new()
    {
        InputAction = action,
        InputMapping = [..bindings],
    };

    protected static InputMappingContext Context(string name, params InputActionEntry[] mappings) => new()
    {
        ContextName = name,
        Mappings = [..mappings],
    };

    // ---- Managers --------------------------------------------------------------------------------------------------

    /// <summary>
    /// Creates an actor with an <see cref="InputManager"/>.
    /// </summary>
    /// <param name="inScene">Add the actor to the runner's scene: the engine enables and updates the manager, so wait a frame before using it.
    /// Otherwise the manager is enabled here and advanced with <see cref="Tick"/>.</param>
    /// <param name="configure">Changes to the manager before it's enabled.</param>
    protected InputManager CreateManager(bool inScene = false, Action<InputManager> configure = null)
    {
        var actor = Track(new EmptyActor { Name = $"EngineTest_{GetType().Name}_{_actors.Count}" });
        var manager = actor.AddScript<InputManager>();
        configure?.Invoke(manager);

        if (inScene)
        {
            actor.Parent = Check.NotNull(Runner?.Actor?.Scene, "Runner scene");
        }
        else
        {
            manager.OnEnable();
            Defer(manager.Processor.Disable);
        }

        return manager;
    }

    /// <summary>
    /// Advances a manager that isn't in a scene by one frame.
    /// </summary>
    protected static void Tick(InputManager manager, float deltaTime = 1f / 60f) => manager.Processor.Update(deltaTime);

    /// <summary>
    /// Replaces Flax's virtual input system with simulated devices for the rest of the test. Call it before creating
    /// managers: switching backends forgets every registration.
    /// </summary>
    protected ScriptedInput UseScriptedInput()
    {
        var input = new ScriptedInput();
        VirtualInputRegistry.Backend = input;
        Defer(() => VirtualInputRegistry.Backend = null);
        return input;
    }
}
