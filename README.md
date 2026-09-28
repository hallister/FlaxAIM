# FlaxAIM: Adaptive Input Manager

This project is an input manager for Flax Engine modeled after Unreal's EnhancedInputSystem.

# Getting Started

1. Clone this repo into your project plugins folder.
1. Create a new InputAction via New -> Adaptive Input -> Input Action.
1. Set the name to anything (Move).
1. Set the axis to the desired value (Axis 2D).
1. Create a new InputMappingContext via New -> Adaptive Input -> Input Mapping Context
1. Add the above InputAction and assign keys to it (A/D and W/S for move input)
1. Somewhere in your player controller add the following:

```
public JsonAssetReference<InputAction> MoveAction;
public InputManager InputManager;

// In your startup/initialization process
public void OnStart() 
{
    InputSystem = Actor.AddScript<InputManager>();
    InputManager.BindAction(MoveAction.Instance, EnhancedInputActionState.Triggered, HandleInput, Tags.Get("InputTag.Move"));
}

public void HandleInput(Tag tag)
{
    var actionValues = InputSystem.GetActionValue(tag);
    
    _horizontal = actionValues.Axis2D.X;
    _vertical = actionValues.Axis2D.Y;
}
```

### Basics

This system completely overwrites the virtual inputs at run-time that come with Flax by default, so we can dynamically add multiple maps.

#### Input Action

An Input Action is an action tied to an input, like Jump, Move, Look, etc.

#### Input Mapping Context

Assign keys to actions.

#### Roadmap
See [ROADMAP.md](ROADMAP.md) for known issues and planned work.