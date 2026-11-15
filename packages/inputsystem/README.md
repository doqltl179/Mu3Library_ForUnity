# Mu3 Library Input System

Input System package of **Mu3Library For Unity**: `InputSystemManager` keeps the action assets of Unity's Input System under string IDs, finds their action maps and actions by ID or by name, runs interactive rebinds, and saves and restores binding overrides as JSON. In the editor, the **InputSystem Data Exporter** drawer generates a script with the names and IDs of an asset, its action maps, and its actions.

- Package name: `com.github.doqltl179.mu3library.inputsystem`
- Assemblies: `Mu3Library.InputSystem`, with the namespace `Mu3Library.IS`, and the editor assembly `Mu3Library.InputSystem.Editor`, which holds the drawer in the namespace `Mu3Library.Editor.Window.Drawer`.
- Depends on Unity Input System (`com.unity.inputsystem`), which Package Manager installs with this package, and on Mu3 Library (Base), whose editor window tools the drawer builds on.

## Installation

The package ships with the next release, and its Git URL then joins the others under Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation). Until then, add `packages/inputsystem/package.json` from disk. Install Base first: a Git URL install does not resolve the Base dependency by itself.

Up to Base 0.28.0, `InputSystemManager` and the drawer came with Base, compiled into the `Mu3Library` and `Mu3Library.Editor` assemblies while the Input System package was installed. A project that uses them installs this package when it updates Base past 0.28.0. The namespaces and the GUIDs in the `.meta` files are unchanged, so code without an assembly definition of its own needs no change, and a drawer asset made from the exporter keeps its script. An assembly definition that references `Mu3Library` for `InputSystemManager` adds a reference to `Mu3Library.InputSystem`, and one that references `Mu3Library.Editor` for the drawer adds `Mu3Library.InputSystem.Editor`.

## Usage

Hand the manager an asset under an ID; the overloads without an ID use the ID `Default`. An action map or an action is found by its name inside an asset, or by the ID the Input System gave it.

```csharp
using Mu3Library.IS;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControls : MonoBehaviour
{
    private const string AssetId = "Player";
    private const string BindingsKey = "PlayerBindings";

    [SerializeField] private InputActionAsset _actions;

    private InputSystemManager _input;

    private void Start()
    {
        _input = new InputSystemManager();
        _input.AddInputActionAsset(_actions, AssetId, enable: true);

        string savedBindings = PlayerPrefs.GetString(BindingsKey, "");
        if (!string.IsNullOrEmpty(savedBindings))
        {
            _input.ApplyInputActionAssetBindingOverrideFromJson(AssetId, savedBindings);
        }

        _input.GetInputActionWithName(AssetId, "Gameplay", "Jump").performed += _ => Debug.Log("Jump");
    }

    public void RebindJump()
    {
        InputAction jump = _input.GetInputActionWithName(AssetId, "Gameplay", "Jump");
        _input.StartInteractiveRebind(jump, 0, onComplete: () =>
            PlayerPrefs.SetString(BindingsKey, _input.GetOverrideJsonOfInputActionAsset(AssetId)));
    }

    private void OnDestroy()
    {
        _input.Dispose();
    }
}
```

`StartInteractiveRebind` disables the action while the rebind runs and gives it back the enabled state it had once the rebind completes or is canceled. For a binding index out of range, or one that points at a composite, whose parts are rebound instead, it logs an error and returns `null`. `IInputSystemManagerEventBus` reports the assets added and removed and the rebinds started, completed, and canceled.

Adding an asset under an ID already in use replaces the earlier asset, with a warning. `Dispose` disables every asset the manager holds and destroys only the assets it built itself from JSON with `AddInputActionAssetFromJson`; an asset added from outside stays with its owner.

With the Mu3 Library DI package ([README](../di/README.md)), a core registers the manager like any other service, as a singleton under `InputSystemManager` and its interfaces `IInputSystemManager` and `IInputSystemManagerEventBus`:

```csharp
using Mu3Library.DI;
using Mu3Library.IS;

public class InputCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        RegisterClass<InputSystemManager>();
    }
}
```

### InputSystem Data Exporter

Create the drawer with **Assets > Create > Mu3Library > Editor Window > Window Drawers > InputSystem Data Exporter** and add it to the drawer list of a `Mu3Window`. Pick an Input Action Asset, a script folder inside `Assets`, and the asset ID the game passes to `AddInputActionAsset`; the namespace and the class name are optional, and the class name defaults to the asset's name. **Generate C# Script** writes a static class with `Name` and `Id` constants for the asset, and a nested class with the same constants for each action map and each of its actions, so code looks them up without typing their names:

```csharp
InputAction jump = input.GetInputAction(PlayerActions.Gameplay.Jump.Id);
```

## Documentation

- [Repository README](https://github.com/doqltl179/Mu3Library_ForUnity)
- Release history: this package's [CHANGELOG](CHANGELOG.md), full detail in the repository root `CHANGELOG.md`.
