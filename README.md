# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](docs/readme/README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](docs/readme/README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

**Mu3Library** is a modular architecture framework for Unity projects. Built around a custom DI (Dependency Injection) system and MVP (Model-View-Presenter) UI pattern, it supports scalable and maintainable game development.

## 📘 Documentation

- Korean README: `docs/readme/README.ko.md`
- Japanese README: `docs/readme/README.ja.md`
- Changelog (EN): `CHANGELOG.md`
- Changelog (KO): `docs/changelog/CHANGELOG.ko.md`
- Changelog (JA): `docs/changelog/CHANGELOG.ja.md`

## ✨ Key Features

- 🏗 **Modular Core System**: Clear separation of concerns through independent `CoreBase` modules
- 💉 **Custom DI Container**: Supports Singleton, Transient, and Scoped lifetimes
- 🎨 **MVP UI Pattern**: Testable UI logic with View-Presenter-Model separation
- 🔄 **Automatic Lifecycle Management**: Interface-based system with `IInitializable`, `IUpdatable`, `IDisposable`
- 📦 **Optional Package Support**: Conditional activation for UniTask, Addressables, Localization
- 🎵 **Audio System**: Separate BGM/SFX management with volume control
- 🌐 **WebRequest**: HTTP GET/POST, download size queries, UniTask support
- 📊 **Observable Pattern**: Data change detection and event-based binding
- 🛠 **Utility Collection**: Extension Methods, ObjectPool, EasingFunctions
- ✅ **Initialization Result Contracts**: Addressables/Localization expose explicit init success/failure state
- 🔁 **Resilient Networking**: WebRequest result-based APIs include status, headers, timeout, and retry options
- 🧭 **Deterministic Core Updates**: Core execution order is explicit and stable
- ⏳ **Scene Async APIs**: UniTask + CancellationToken scene load/unload helpers
- 🎮 **Input System Manager**: Action asset management, interactive rebinding, and binding override persistence (optional)
- 🧰 **Editor Utility Drawers**: Includes Input System and Localization name exporters plus Localization character collection tools

## 📋 Requirements

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 Installation

### Option 1: Package Manager (Git URL)
1. Open `Window` > `Package Manager` in Unity Editor
2. Click `+` button > `Add package from git URL...`
3. Enter the following URL:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base
   ```

### Option 2: Package Manager (Local Disk)
1. Clone this repository
2. Open `Window` > `Package Manager` in Unity Editor
3. Click `+` button > `Add package from disk...`
4. Select `Mu3Library_Base/package.json`

## 📚 Core Modules

### DI (Dependency Injection)
Custom DI container that automates service registration and dependency injection.

```csharp
using Mu3Library.DI;

public class AudioCore : CoreBase
{
    protected override void ConfigureContainer(ContainerScope scope)
    {
        // Register as singleton
        scope.Register<IAudioManager, AudioManager>(ServiceLifetime.Singleton);
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

## 📖 Complete Module List

- **Addressable**: Addressables integration (optional)
- **Attribute**: Custom attributes like `ConditionalHideAttribute`
- **Audio**: BGM/SFX management system
- **DI**: Dependency Injection container
- **Extensions**: Extension methods for GameObject, Transform, Vector3, etc.
- **Localization**: Unity Localization wrapper (optional)
- **ObjectPool**: Generic object pooling
- **Observable**: Data change detection system
- **Preference**: PlayerPrefs wrapper
- **Resource**: Resources folder loading
- **Scene**: Scene loading abstraction
- **UI**: MVP pattern implementation
- **IS**: Unity Input System wrapper and binding manager (optional)
- **Utility**: Singleton, EasingFunctions, Settings
- **WebRequest**: HTTP request management

## 🎓 Samples

You can import the following samples from the **Samples** tab in Package Manager:

- **Sample_Template**: Basic Core structure and usage examples
- **Sample_Attribute**: ConditionalHide usage
- **Sample_UtilWindow**: Custom editor window and utility drawer examples

Or refer to the `UnityProject_BuiltIn/Assets/Mu3LibrarySamples` folder in your project.

**Sample_Template Key Components:**
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

## 📝 Recent Updates (v0.4.7)

**ScriptBuilder Enhancement:**
- `ScriptBuilder`: Added `ArrayBlock` struct (`FieldName`, `Values`) and `AppendArrayBlock` method for concise array emission in code generation.

**Audio Key-Based API:**
- `AudioManager.Resource`: Key-based `AudioClip` registration (`RegisterAudioResource`, `RegisterAudioResources`).
- `WithKey` overloads added across all channel types (BGM, SFX, Environment) to play audio by registered key.

**AddressableGroupNameExporterDrawer:**
- New editor drawer that exports Addressable group names, asset names, addresses, and labels as nested C# static classes (requires `MU3LIBRARY_ADDRESSABLES_SUPPORT`).
- Folder entry support: sub-assets inside a folder are emitted as a nested `Assets` class.
- Sub-asset class names that begin with the parent class name have the parent prefix stripped automatically.

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
- Name: `com.github.doqltl179.mu3library.base`
- Version: `0.4.7`

Made with ❤️ for Unity Developers
