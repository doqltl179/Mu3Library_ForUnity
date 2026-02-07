# Mu3Library For Unity

<div align="center">

### 🌐 Language

[English](README.md) · [한국어](README.ko.md) · [日本語](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

</div>

**Mu3Library** is a modular architecture framework for Unity projects. Built around a custom DI (Dependency Injection) system and MVP (Model-View-Presenter) UI pattern, it supports scalable and maintainable game development.

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

## 📋 Requirements

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 Installation

### Option 1: Package Manager (Git URL)
1. Open `Window` > `Package Manager` in Unity Editor
2. Click `+` button > `Add package from git URL...`
3. Enter the following URL:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets
   ```

### Option 2: Package Manager (Local Disk)
1. Clone this repository
2. Open `Window` > `Package Manager` in Unity Editor
3. Click `+` button > `Add package from disk...`
4. Select `Assets/Mu3LibraryAssets/package.json`

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
    // Auto-injection (within same Core)
    [Inject] private IAudioManager _audioManager;

    // Injection from different Core
    [Inject(typeof(UICore))] private IMVPManager _mvpManager;

    protected override void Start()
    {
        base.Start(); // Injection must be executed first!
        _audioManager.PlayBGM("MainTheme");
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
_mvpManager.LoadAndOpen<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
```

### Audio System
Separate management of BGM and SFX with volume control support.

```csharp
[Inject] private IAudioManager _audioManager;

void Start()
{
    // Set volumes
    _audioManager.MasterVolume = 0.8f;
    _audioManager.BgmVolume = 0.6f;

    // Play BGM
    _audioManager.PlayBgm(bgmClip, fadeTime: 1.0f);

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
- **Utility**: Singleton, EasingFunctions, Settings
- **WebRequest**: HTTP request management

## 🎓 Samples

You can import the following samples from the **Samples** tab in Package Manager:

- **Sample_Template**: Basic Core structure and usage examples
- **Sample_Attribute**: ConditionalHide usage
- **Sample_UtilWindow**: Custom editor window

Or refer to the `Assets/Mu3LibrarySamples` folder in your project.

**Sample_Template Key Components:**
- Scenes: Main, Sample_MVP, Sample_Addressables, Sample_Localization, Sample_WebRequest, Sample_Audio, Sample_Audio3D
- Localization: Locales (KO/JA/EN), String Table samples
- Resources: Prefabs and settings for MVP samples
- Materials: Default color materials (Black, Blue, Green, Magenta, Red, White)

## 🏗 아키텍처 개요

### Core 시스템
각 `CoreBase`는 독립적인 DI 컨테이너(`ContainerScope`)를 소유하며, `CoreRoot`가 생명주기를 관리합니다.

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

## 📝 Recent Updates (v0.1.11)

**UI/MVP:**
- Removed MVPCanvasUtil and integrated into MVPManager - Cleaner API
- Improved MVPCanvasSettings - Enhanced flexibility with settings separation

**Audio System:**
- Improved AudioSourceSettings and updated AudioController
- Added 3D Audio sample - Sample_Audio3D scene and MouseClickHandler example
  - Scenes: Sample_Audio3D.unity
  - Scripts: SampleAudio3DCore, MouseClickHandler
  - Added thumbnail

**Extensions:**
- Added `SetLayerWithChildren` function to GameObjectExtension - Batch layer setting for child objects

**Materials:**
- Added default color materials - Black, Blue, Green, Magenta, Red, White
- Available immediately in Runtime/Materials folder

**Bug Fixes:**
- Fixed lifecycle bugs in DI classes (ContainerScope, CoreBase)

**Previous Updates (v0.1.10):**
- Improved to use the same instance when retrieving classes through Core even if multiple interfaces are applied to a single class
- DI code optimization and refactoring
- Changed Collection to readonly for enhanced stability
- Added CameraExtensions - Camera property copy functionality
- Added bitwise operation Extensions for int type
- Modified MVP Canvas default settings

## 🤝 Contributing

Issues and pull requests are welcome! Please note the following:
- **Coding Style**: Private fields use `_camelCase`, Allman braces
- **Commit Messages**: Clear descriptions (e.g., `feat: Add retry logic to WebRequest`)
- **Tests**: Include test code when possible

## 📄 License

This project is distributed under the MIT License.

## 📞 Contact

- **GitHub Issues**: [https://github.com/doqltl179/Mu3Library_ForUnity/issues](https://github.com/doqltl179/Mu3Library_ForUnity/issues)
- **Author**: Mu3 ([GitHub](https://github.com/doqltl179))

---

**Package Info:**
- Name: `com.github.doqltl179.mu3libraryassets.base`
- Version: `0.1.11`

Made with ❤️ for Unity Developers

