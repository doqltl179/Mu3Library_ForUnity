# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](docs/readme/README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](docs/readme/README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

**Mu3Library** is a modular architecture framework for Unity projects. Built around a custom DI (Dependency Injection) system and MVP (Model-View-Presenter) UI pattern, it supports scalable and maintainable game development.

## 📘 Documentation

### Package Documentation

- [Korean README](docs/readme/README.ko.md) · [Japanese README](docs/readme/README.ja.md)
- [Package changelog (English)](CHANGELOG.md) · [Korean](docs/changelog/CHANGELOG.ko.md) · [Japanese](docs/changelog/CHANGELOG.ja.md)

### Contributor Documentation

- [Repository workflow changelog](docs/repository/CHANGELOG.md)
- [AI-agent and contributor workflow](docs/ai-agents/README.md) — choose task ownership, procedures, and verification routes.
- [Repository tooling](tools/README.md)

## ✨ Key Features

- 🏗 **Modular Core System**: Clear separation of concerns through independent `CoreBase` modules
- 💉 **Custom DI Container**: Supports Singleton, Transient, and Scoped lifetimes
- 🎨 **MVP UI Pattern**: Testable UI logic with View-Presenter-Model separation
- 🔄 **Automatic Lifecycle Management**: Interface-based system with `IInitializable`, `IUpdatable`, `IDisposable`
- 📦 **Optional Package Support**: Conditional activation for UniTask, Addressables, Localization
- 🎵 **Audio System**: Separate BGM/SFX management with volume control
- ✨ **Particle Handler**: Convenient `ParticleSystem` playback, pause, stop, clear, restart, `Loop`/`IsLooping`/`SetLoop`/`PlayLoop`/`PlayOnce` controls, state queries, and lifecycle events on a required component
- 🌐 **WebRequest**: HTTP GET/POST, download size queries, UniTask support
- 📊 **Observable Pattern**: Data change detection and event-based binding
- 🛠 **Utility Collection**: Extension Methods, ObjectPool, EasingFunctions
- ✅ **Initialization Result Contracts**: Addressables/Localization expose explicit init success/failure state
- 🔁 **Resilient Networking**: WebRequest result-based APIs include status, headers, timeout, retry options, and opt-in cancellation propagation
- 🧭 **Deterministic Core Updates**: Core execution order is explicit and stable
- ⏳ **Scene Async APIs**: Phase-based UniTask helpers for built-in and Addressables scenes, plus structured lifecycle callbacks with resolved scene names
- 🎮 **Input System Manager**: Action asset management, interactive rebinding, and binding override persistence (optional)
- 🧰 **Editor Utility Drawers**: Includes Input System and Localization name exporters, Addressable group and Localization data exports with labels/locales, groups/tables, and entries split into focused scripts, and Localization character collection tools
- 🖼 **World-Space Background Utility**: Fits a required `SpriteRenderer` background to the camera viewport with optional automatic fitting, camera-front placement, and renderer display settings
- 🍉 **Watermelon Game Template**: Reusable `Mu3Library.Game.WatermelonGame` runtime assembly with an eleven-item 2D merge board, configurable `BoardConfig`, board fitting and coordinate helpers, and a playable sample scene

## 📋 Requirements

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 Installation

### Option 1: Package Manager (Git URL)
1. Open `Window` > `Package Manager` in Unity Editor
2. Click `+` button > `Add package from git URL...`
3. Enter one of the following URLs:
   ```
    # Base package
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/v0.22.0

    # URP package (install Base first)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_URP#urp/v0.2.1
   ```

### Option 2: Package Manager (Local Disk)
1. Clone this repository
2. Open `Window` > `Package Manager` in Unity Editor
3. Click `+` button > `Add package from disk...`
4. Select one of the following:
    - `Mu3Library_Base/package.json`
    - `Mu3Library_URP/package.json` (install Base first)
    - `Mu3Library_Game_WatermelonGame/package.json` (install Base and URP first)

## 📚 Core Modules

### DI (Dependency Injection)
Custom DI container that automates service registration and dependency injection.

```csharp
using Mu3Library.DI;

public class AudioCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        // Register AudioManager as a singleton — automatically maps to IAudioManager
        RegisterClass<AudioManager>();
    }
}

public class GameCore : CoreBase
{
    [SerializeField] private AudioClip _mainThemeClip;

    // Auto-injection (within same Core)
    [Inject] private IAudioManager _audioManager;

    // Injection from different Core
    [Inject(typeof(UICore))] private IMVPManager _mvpManager;

    protected override void Start()
    {
        base.Start(); // Injection must be executed first!
        _audioManager.PlayBgm(_mainThemeClip);
    }
}
```

Registered services also receive `[Inject]` field and property injection after construction and before lifecycle callbacks.

```csharp
public class AudioPlaybackService
{
    [Inject] private IAudioManager _audioManager;
}

public class AudioCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        RegisterClass<AudioManager>();
        RegisterClass<AudioPlaybackService>(); // [Inject] members are populated automatically
    }
}
```

`MVPManager` also uses the narrow `IObjectInjector` capability to apply `[Inject]` field and property injection to presenters created outside the container. The concrete `ContainerScope` and its internal injection implementation remain private to the DI assembly.

### MVP (Model-View-Presenter)
Separates UI into View, Presenter, and Model for testable business logic.

```csharp
// Model: Data container
public class MainMenuModel : Model<MainMenuArgs>
{
    public string PlayerName { get; set; }
}

// View: Unity component references
public class MainMenuView : View
{
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _titleText;

    public Button StartButton => _startButton;
    public TextMeshProUGUI TitleText => _titleText;
}

// Presenter: Business logic
public class MainMenuPresenter : Presenter<MainMenuView, MainMenuModel, MainMenuArgs>
{
    [Inject(typeof(AudioCore))] private IAudioManager _audioManager;

    protected override void LoadFunc()
    {
        _view.StartButton.onClick.AddListener(OnStartClicked);
        _view.TitleText.text = $"Welcome, {_model.PlayerName}";
    }

    protected override void OpenFunc()
    {
        // Open animations, etc.
    }

    private void OnStartClicked()
    {
        // Game start logic
    }
}

// Usage
_mvpManager.Open<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
```

When `MVPManager` is registered through `CoreBase`, presenter `[Inject]` fields and properties are populated before presenter initialization. Use `Arguments` for data that changes each time a presenter is opened.

For chained presenters, ownership and visual hosting are configured separately.

```csharp
public class InventoryPresenter : Presenter<InventoryView, InventoryModel, InventoryArgs>
{
    private void OpenTooltip()
    {
        OpenAsChild<TooltipPresenter>(new HostOptions
        {
            Host = _view.TooltipHost,
            ApplyLayout = rectTransform =>
            {
                rectTransform.anchoredPosition = new Vector2(24f, -16f);
            },
        });
    }
}
```

`Owner` controls lifecycle chaining, while `HostOptions` controls where the child view is attached and how its root `RectTransform` is laid out.

Direct manager calls can keep the same ownership and host configuration in one overload:

```csharp
_mvpManager.Open<TooltipPresenter>(inventoryPresenter, new HostOptions
{
    Host = tooltipHost,
});
```

### Audio System
Separate management of BGM and SFX with volume control support.

```csharp
[Inject] private IAudioManager _audioManager;
[Inject] private IAudioVolumeSettings _audioVolumeSettings;

void Start()
{
    // Set volumes
    _audioVolumeSettings.MasterVolume = 0.8f;
    _audioVolumeSettings.BgmVolume = 0.6f;

    // Play BGM
    _audioManager.PlayBgm(bgmClip);

    // Play SFX
    _audioManager.PlaySfx(sfxClip, volume: 1.0f);
}
```

### Addressables
With Addressables support enabled, load all assets resolved by a key into a `Dictionary<string, T>` indexed by the matching resource location's `PrimaryKey`. `PrimaryKey` values must be non-empty and unique; otherwise the load operation fails.

```csharp
[Inject] private IAddressablesManager _addressablesManager;

_addressablesManager.LoadAssetsWithKeys<Texture2D>("test-image", textures =>
{
    if (textures != null && textures.TryGetValue("TestImage", out Texture2D texture))
    {
        Debug.Log(texture.name);
    }
});

// When UniTask support is also enabled:
Dictionary<string, Texture2D> textures =
    await _addressablesManager.LoadAssetsWithKeysAsync<Texture2D>("test-image");
```

### Scene Loader
Supports explicit scene commands for both built-in and Addressables scenes.

```csharp
[Inject] private ISceneLoader _sceneLoader;
[Inject] private ISceneLoaderEventBus _sceneLoaderEventBus;

// Legacy callbacks keep the requested target or Addressables key.
_sceneLoaderEventBus.OnSingleSceneLoaded += target =>
{
    Debug.Log($"Loaded target: {target}");
};

// Structured lifecycle callbacks expose the resolved Unity scene name.
// Addressables scenes use "UnnamedAddressableScene" until the runtime name is available.
_sceneLoaderEventBus.OnSingleSceneLifecycle += info =>
{
    Debug.Log($"{info.Phase}: target={info.Target}, resolved={info.ResolvedSceneName}");
};

await _sceneLoader.LoadSingleSceneAsync("Main");
await _sceneLoader.LoadSingleSceneWithAddressablesAsync("Sample_Addressables");
```

### WebRequest
Simplifies HTTP request handling.

```csharp
[Inject] private IWebRequestManager _webRequest;

// GET request
_webRequest.Get<string>("https://api.example.com/data", response =>
{
    Debug.Log(response);
});

// POST request
var requestBody = new { username = "player", score = 100 };
_webRequest.Post<object, ServerResponse>("https://api.example.com/submit", requestBody, response =>
{
    Debug.Log($"Success: {response.message}");
});

// UniTask support (when MU3LIBRARY_UNITASK_SUPPORT is enabled)
var data = await _webRequest.GetAsync<DataModel>("https://api.example.com/data");

// Propagate cancellation only when the caller explicitly needs it
var cancellableData = await _webRequest.GetAsync<DataModel>(
    "https://api.example.com/data",
    cancellationToken: token,
    propagateCancellation: true);
```

### Observable Pattern
Detects value changes and publishes events.

```csharp
public class PlayerData
{
    public ObservableInt Health = new ObservableInt();
    public ObservableString PlayerName = new ObservableString();
}

// Subscribe to events
_playerData.Health.AddEvent(newHealth =>
{
    Debug.Log($"Health changed: {newHealth}");
    UpdateHealthUI(newHealth);
});

// Change value (automatically publishes event)
_playerData.Health.Set(80);
```

## 🔧 Optional Packages

When the following packages are installed, their features are automatically enabled:

| Package | Define | Feature |
|-------|--------|------|
| [UniTask](https://github.com/Cysharp/UniTask) | `MU3LIBRARY_UNITASK_SUPPORT` | async/await asynchronous API |
| Unity Addressables | `MU3LIBRARY_ADDRESSABLES_SUPPORT` | Dynamic asset loading |
| Unity Localization | `MU3LIBRARY_LOCALIZATION_SUPPORT` | Multi-language support |
| Unity Input System | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` | New input system |

## 🧩 Inspector Attributes

Apply `ButtonInvokeAttribute` to a parameterless instance method that returns `void`. Its label and height are optional; an omitted label is displayed as `Invoke {method name}`. Buttons are appended after the default serialized properties by the fallback Inspector, so source declaration order cannot position them among fields. A type with its own custom Inspector must render its `ButtonInvoke` buttons there.

```csharp
[ButtonInvoke("Refresh Data", ButtonHeight = 36f)]
private void RefreshData()
{
}

[ButtonInvoke]
private void ResetData()
{
    // Inspector label: Invoke ResetData
}
```

## 📖 Complete Module List

- **Addressable**: Addressables integration (optional)
- **Attribute**: Custom attributes like `ConditionalHideAttribute` and `ButtonInvokeAttribute`
- **Audio**: BGM/SFX management system
- **Particle**: `ParticleHandler` MonoBehaviour for convenient `ParticleSystem` control, loop configuration, and lifecycle events (`OnPlayed`, `OnStopped`, `OnPaused`, `OnUnPaused`, `OnCleared`, `OnRestarted`, `OnLoopChanged`, `OnCompleted`)
- **DI**: Dependency Injection container
- **Event**: Owner-managed subscription utilities, reusable one-shot helpers, and disposable `ISubscriptionInfo` tokens via `SubscribeHandler`
- **Extensions**: Extension methods for GameObject, Transform, Vector3, etc.
- **Localization**: Unity Localization wrapper (optional)
- **ObjectPool**: Queue-based object pooling with duplicate inactive enqueue protection, batch `List<T>` enqueue and count- or argument-list-based `List<T>` dequeue support, optional no-argument creation callbacks, typed `CreateArguments` initialization callbacks, and `Clear()` cleanup
- **Observable**: Data change detection system
- **Preference**: PlayerPrefs wrapper
- **Resource**: Resources folder loading
- **Scene**: Scene loading abstraction with phase/status queries, lifecycle/progress callbacks, one-shot lifecycle subscription helpers, and unified rejection events
- **UI**: MVP pattern implementation
- **IS**: Unity Input System wrapper and binding manager (optional)
- **Utility**: Singleton, EasingFunctions, Settings, and `Mellow.Utility.WorldSpaceBackground` for fitting a `SpriteRenderer` background to a camera viewport
- **WebRequest**: HTTP request management

## 🎓 Samples

You can import the following samples from the **Samples** tab in Package Manager:

Base package (**Mu3 Library**):
- **Template**: Basic Core structure and usage examples
- **Attribute**: `ConditionalHideAttribute` and `ButtonInvokeAttribute` usage
- **UtilWindow**: Custom editor window and utility drawer examples

URP package (**Mu3 Library URP**):
- **ScreenEffect**: URP screen effect sample scene and supporting scripts with grayscale, shake, gaussian blur, and depth outline effects and matching handler scripts
- **Camera Stack Helper**: `Mu3Library.URP.Cam.CameraStackSetter` adds `SetCameraStackToMain(...)` and `SetCameraStack(...)` helpers so any URP overlay camera can be inserted into `Camera.main` or an explicit root camera stack with optional insertion index control

Watermelon Game package (**Mu3 Library Watermelon Game**):
- **Watermelon Game**: Playable 2D falling-fruit merge scene with a configurable board, fruit sprites, and sample manager/core scripts
- **Fruit item indexes**: The board configuration always contains 11 fruit entries, addressed by zero-based list index
- **Board fitting**: `BoardArea.Fit(...)` fits the board sprite inside the configured camera viewport padding
- **Board collision boundaries**: `BoardArea.SetOutColliders()` creates or updates left, right, and bottom `BoxCollider2D` boundaries from the item-area viewport padding while keeping the top open
- **Board-relative coordinate conversions**: `BoardArea` exposes world, screen, and local normalized-position conversions with initialization-safe `Try...` variants
- **Scoring extension point**: Override `BoardItemScoreRule.GetScore(int)` to customize the default triangular score progression
- **Sample assets**: The package sample includes the `BoardConfig` asset, fruit/background images, sample manager/core scripts, and the `Demo` scene

In this repository, the base sample sources live under `Mu3Library_Base/Samples~`, the URP sample source lives under `Mu3Library_URP/Samples~/ScreenEffect`, and the Watermelon Game sample source lives under `Mu3Library_Game_WatermelonGame/Samples~/WatermelonGame`.

To stack an MVP render camera with the new helper, pass the render camera explicitly:

```csharp
using Mu3Library.URP.Cam;

CameraStackSetter.SetCameraStack(targetCamera, _mvpManager.RenderCamera);
```

**Template Key Components:**
- Scenes: Main, Sample_MVP, Sample_Addressables, Sample_Localization, Sample_WebRequest, Sample_Audio, Sample_Audio3D, Sample_IS
- Localization: Locales (KO/JA/EN), String Table samples
- Resources: Prefabs and settings for MVP samples
- Materials: Default color materials (Black, Blue, Green, Magenta, Red, White)

## 🏗 Architecture Overview

### Core System
Each `CoreBase` owns an independent DI container (`ContainerScope`), and `CoreRoot` manages the lifecycle.

```
CoreRoot (Singleton)
├── AudioCore (Independent ContainerScope)
│   └── AudioManager, BgmController, SfxController
├── UICore (Independent ContainerScope)
│   └── MVPManager, Presenters...
└── NetworkCore (Independent ContainerScope)
    └── WebRequestManager
```

### Cross-Core Communication
To access services from different Cores:

```csharp
// Method 1: Manual acquisition in Start()
protected override void Start()
{
    base.Start();
    _audioManager = GetClassFromOtherCore<AudioCore, IAudioManager>();
}

// Method 2: Inject attribute (CoreBase only)
[Inject(typeof(AudioCore))] private IAudioManager _audioManager;
```

To run code once a specific Core has finished initializing, subscribe through `ICoreRoot`:

```csharp
CoreRoot.Instance.SubscribeOnCoreInitializedOnce<AudioCore>(() =>
{
    _audioManager = CoreRoot.Instance.GetClass<AudioCore, IAudioManager>();
});
```

The callback is invoked once after the target Core has completed its initialization.

For asynchronous preparation work, subscribe to the preparation completion notification:

```csharp
CoreRoot.Instance.SubscribeOnCorePreparedOnce<AudioCore>(() =>
{
    // Run code after AudioCore preparation completes.
});
```

The callback is invoked once after the target Core has completed its preparation.

## 📝 Recent Updates

- Current Base package version in this repository: `0.22.0`
- Current URP package version in this repository: `0.2.1` (manifest dependency: `com.github.doqltl179.mu3library.base` `0.22.0`)
- Current Watermelon Game package version in this repository: `0.1.0` (depends on Base `0.22.0` and URP `0.2.1`)
- See `CHANGELOG.md` for the repository release notes and draft version history.

## 🤝 Contributing

Issues and pull requests are welcome! Please note the following:
- **Coding Style**: Private fields use `_camelCase`, Allman braces
- **Commit Messages**: Clear descriptions (e.g., `feat: Add retry logic to WebRequest`)

## 📄 License

This project is distributed under the MIT License.

## 📞 Contact

- **GitHub Issues**: [https://github.com/doqltl179/Mu3Library_ForUnity/issues](https://github.com/doqltl179/Mu3Library_ForUnity/issues)
- **Author**: Mu3 ([GitHub](https://github.com/doqltl179))

---

**Package Info:**
- Base: `com.github.doqltl179.mu3library.base` `0.22.0`
- URP: `com.github.doqltl179.mu3library.urp` `0.2.1` (manifest dependency: `com.github.doqltl179.mu3library.base` `0.22.0`)
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon` `0.1.0` (depends on Base `0.22.0` and URP `0.2.1`)

Made with ❤️ for Unity Developers
