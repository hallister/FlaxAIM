# FlaxAIM: Adaptive Input Manager

This project is an input manager for Flax Engine modeled after Unreal's EnhancedInputSystem.

### AI Disclaimer

I leverage AI assistive technologies for coding and documentation due to a physical medical condition. All logic, architecture, and code execution are verified by me before submission.

# Getting Started

1. Clone this repo into your project plugins folder.
1. Create a new InputAction via New -> Adaptive Input -> Input Action.
1. Set the name to anything (Move).
1. Set the axis to the desired value (Axis 2D).
1. Create a new InputMappingContext via New -> Adaptive Input -> Input Mapping.
1. Add the above InputAction and assign keys to it (A/D with Target X and W/S with Target Y for move input).
1. Add an `InputManager` script to your player, and somewhere in your player controller add the following:

```csharp
public JsonAssetReference<InputMappingContext> GameplayContext;
public JsonAssetReference<InputAction> MoveAction;

private InputManager _input;
private Float2 _move;

public override void OnStart()
{
    _input = Actor.GetScript<InputManager>();
    _input.AddInputContext(GameplayContext.Instance);

    _input.BindAction<Float2>(MoveAction.Instance, EnhancedInputActionState.Triggered, value => _move = value);
    _input.BindAction(MoveAction.Instance, EnhancedInputActionState.Completed, () => _move = Float2.Zero);
}

public override void OnDestroy() => _input?.UnbindAll(this);
```

The [examples project](#examples) has a complete, playable version of this.

## Versions and upgrades

`VERSION` is the authoritative release number. During `0.x`,
breaking changes increment the minor version; compatible additions and fixes increment the patch version.
Pin a published Git tag when using the plugin in a game. See [release instructions](docs/Releasing.md) and the
[changelog and v0.2 migration guide](CHANGELOG.md) before upgrading.

Upgrading from v0.1 to v0.2 requires a one-time rebuild of existing IMC bindings in the editor. Old assets are not
automatically converted; contexts built in C# require the API changes described in the migration guide.

### Basics

FlaxAIM publishes its bindings to Flax's virtual input tables at runtime, next to the project's own input settings,
so contexts can be added and removed dynamically.

#### Input Action

An Input Action is an action tied to an input, like Jump, Move, Look, etc.

#### Input Mapping Context

A list of Input Actions, each with an array of the inputs that drive it (like Unreal's). Every input has its own
modifiers and triggers, so the keyboard and gamepad bindings of one action can behave differently. Pick each input from
the **Device ▸ Control** menu:

| Device   | Controls                                                                  |
|----------|---------------------------------------------------------------------------|
| Keyboard | Key, Key Axis (positive/negative keys), Directional Keys (up/down/left/right) |
| Mouse    | Button, Delta, Wheel                                                      |
| Gamepad  | Button, Button Axis, Left Stick, Right Stick, D-Pad, Left Trigger, Right Trigger |

Directional Keys, the sticks, the D-pad and mouse Delta fill an `Axis2D` action's X and Y from one input. Set **Axes** to
read only X or Y from them. An analog input that produces one value drives the action component chosen by **Target**
(Digital and Axis1D actions ignore it).

Contexts are added to an `InputManager` with a priority; higher-priority contexts consume the keys they use.

## Examples

`Examples/` is a separate Flax project (`Examples/FlaxAIMExamples.flaxproj`) that references the plugin. Games using
FlaxAIM don't need it. It contains:

- **Demo scene** (`Content/Demo.scene`, the default scene): a player moved with WASD or a gamepad. It shows typed
  callbacks, `Pressed` (jump), polling (sprint), a chord (slide = sprint + C), a hold (spin = hold E), action-level
  modifiers (a radial dead zone on the stick), and a pause menu context that consumes the movement keys. An on-screen
  HUD shows each action's state, events and value. The input assets are in `Content/Input`.
- **Engine tests** (`Content/Tests/EngineTests.scene`): see [Testing](#testing).

Open `Examples/FlaxAIMExamples.flaxproj` in the Flax editor and press Play.

## Testing

- **Unit tests** in `Tests/FlaxAIM.Tests` run without the engine (`dotnet test Tests/FlaxAIM.Tests`, which needs a
  Flax installation or `FLAX_ENGINE_PATH`). They drive an `InputProcessor` against simulated devices and cover the
  state machine, triggers, modifiers, binding evaluation, contexts and priority, callbacks, the compiler and the shared
  virtual input tables.
- **Engine tests** in the examples project run in play mode against real engine objects: Flax's virtual input tables,
  actions and contexts as JSON assets, the `InputManager` script's lifecycle, and the demo's content. They run in the
  editor or from the command line; see [engine tests](docs/EngineTests.md).

## Repository

- `Source/FlaxAIM/` — the runtime: `InputProcessor` (the engine-independent core), the `InputManager` script that
  drives it, actions, contexts, triggers and modifiers.
- `Source/FlaxAIMEditor/` — editor integration: the Device ▸ Control picker for mapping inputs.
- `Tests/FlaxAIM.Tests/` — unit tests (see [Testing](#testing)).
- `Examples/` — the examples project (see [Examples](#examples)).

## Roadmap

See [ROADMAP.md](ROADMAP.md) for known issues and planned work.
