# Changelog

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](docs/changelog/CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](docs/changelog/CHANGELOG.ja.md)

</div>

All notable changes to Mu3Library For Unity will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Scene UniTask APIs with cancellation support:
  - `ISceneLoader.LoadSingleSceneAsync`
  - `ISceneLoader.LoadAdditiveSceneAsync`
  - `ISceneLoader.UnloadAdditiveSceneAsync`

### Changed
- Addressables and Localization initialization contracts now expose explicit result state:
  - `IsInitialized`
  - `IsInitializing`
  - `InitializeError`
  - `OnInitializeResult` event
  - `InitializeWithResult(Action<bool, string>)` API
- WebRequest API now provides structured result variants:
  - `WebRequestResult<T>` with `IsSuccess`, `StatusCode`, `ErrorMessage`, `ResponseHeaders`, `Data`
  - Callback APIs: `GetWithResult`, `PostWithResult`, `GetDownloadSizeWithResult`
  - UniTask APIs: `GetResultAsync`, `PostResultAsync`, `GetDownloadSizeResultAsync`
  - Added request timeout and retry options for result-based APIs.
- Core execution order is now deterministic via `CoreBase` serialized execution order setting.
- Scene unload lifecycle now emits explicit events:
  - `OnAdditiveSceneUnloadStart`
  - `OnAdditiveSceneUnloadEnd`
  - `LoadingCount` now includes additive unload operations.
- Separated service event contracts into dedicated event bus interfaces:
  - `IAddressablesManagerEventBus`
  - `ILocalizationManagerEventBus`
  - `ISceneLoaderEventBus`
  - `IMVPManagerEventBus`
  - `IAudioManagerEventBus`
  - Existing service interfaces no longer declare these `event` members directly.

## [0.1.11] - 2026-02-08

### 🌟 Overview

**Major architectural overhaul** - Complete package restructure with new DI Container system, enhanced MVP pattern, and comprehensive manager systems. This release represents a fundamental redesign of the library's core architecture.

### ⚠️ BREAKING CHANGES

#### Package Structure
- **Complete folder reorganization**: Separated into `Runtime/` and `Editor/` directories
- **Assembly definitions updated**: New asmdef files with proper dependencies
- **Namespace changes**: All code moved to `Mu3Library` namespace hierarchy
  - `Mu3Library.DI` for Dependency Injection
  - `Mu3Library.MVP` for UI pattern
  - Module-specific namespaces for Audio, Scene, etc.

#### API Changes
- **MVP Pattern**: Complete rewrite of MVP system
  - `Presenter<TView, TModel, TArgs>` signature changed
  - `View` lifecycle methods restructured
  - `MVPManager` API completely redesigned
  - Animation system integrated into MVP views
- **DI Container**: New injection system replaces manual initialization
  - `[Inject]` attribute for dependency injection
  - Core-based architecture with `CoreBase` and `CoreRoot`
- **ResourceLoader**: Now DI-based, no longer static
- **SceneLoader**: Interface-based with separate implementations
  - `ISceneLoader.Editor` for Editor scenes
  - `ISceneLoader.Addressables` for Addressables scenes

#### Removed Features
- ❌ **InputSystem helpers** (`InputSystem_Actions`, `InputSystemHelper`)
- ❌ **MarchingCubes system** (entire compute shader system removed)
- ❌ **PostEffect/CommandBuffer effects** (Blur, EdgeDetect, GrayScale, Toon shaders)
- ❌ **Camera view components** (FirstPerson, ThirdPerson, FreeView cameras)
- ❌ **Custom UI components** (DatePicker, IntSlider)
- ❌ **Old sample scenes** (Sample_InputAction, Sample_CustomUI, Sample_CommandBufferEffect, Sample_CameraView, Sample_MarchingCubes, Sample_RenderingPipeline)

### ✨ Major Features

#### 🏗️ Dependency Injection (DI) Container
- Custom DI container with three lifetime scopes:
  - `Singleton`: One instance per container
  - `Transient`: New instance per request
  - `Scoped`: One instance per scope
- `CoreBase` architecture for modular system design
- `[Inject]` attribute for automatic dependency resolution
- Cross-core injection support with `[Inject(typeof(OtherCore))]`
- Interface-based lifecycle management:
  - `IInitializable` for initialization
  - `IUpdatable` for Update loop
  - `ILateUpdatable` for LateUpdate loop
  - `IDisposable` for cleanup

#### 🎨 Enhanced MVP Pattern
- Completely redesigned MVP system with DI integration
- **AnimationView** system with configurable animations:
  - `OneCurveAnimation` for single-curve animations
  - `TwoCurveAnimation` for dual-curve animations
  - `AnimationConfig` ScriptableObject for reusable configs
- **MVPCanvasSettings** for fine-grained canvas configuration:
  - Canvas component settings
  - CanvasScaler settings
  - GraphicRaycaster settings
  - OutPanel system for backdrop/dimming
- Enhanced view lifecycle with proper initialization order
- Resource-based and Camera-based view loading
- Loading screen integration

#### 📦 Manager Systems
- **AddressablesManager**: Full Addressables support with caching
  - Load/unload assets with reference counting
  - Scene loading support
  - Progress tracking
  - UniTask integration
- **LocalizationManager**: Unity Localization integration
  - Async initialization
  - Locale switching
  - String table access
  - UniTask support
- **WebRequestManager**: HTTP request handling
  - GET/POST requests
  - Download size queries
  - UniTask integration
  - Callback-based alternatives
- **AudioManager**: Enhanced audio system
  - 3D spatial audio support
  - Separate BGM and SFX controllers
  - Volume management through `IVolumeSettings`
  - AudioSource pooling
- **SceneLoader**: Flexible scene loading
  - Editor scene loading
  - Addressables scene loading
  - Additive scene support
  - Progress events
  - Scene load policies (allow/prevent duplicates)
- **ResourceLoader**: Enhanced Resources folder management
  - Type-safe loading
  - Caching with reference counting
  - UniTask support

#### 🔧 Utility & Extensions
- **Observable types**: Data-binding support
  - `ObservableProperty<T>`, `ObservableBool`, `ObservableInt`, `ObservableFloat`, `ObservableLong`, `ObservableString`
  - `ObservableList<T>` with collection change events
  - `ObservableDictionary<TKey, TValue>` with dictionary events
- **GameObjectPool**: Component pooling system
- **Extensions**: Rich extension methods
  - `CameraExtensions`: Camera property copying
  - `TransformExtensions`: Layer management with children
  - `intExtensions`: Bitwise operations
  - Canvas-related extensions
- **PlayerPrefsLoader**: Type-safe PlayerPrefs access

### 🎯 Added

#### Core Systems
- `CoreBase` and `CoreRoot` for modular architecture
- `ContainerScope` for service registration and resolution
- `ServiceDescriptor` for service configuration
- Automatic lifecycle management through interfaces

#### UI/MVP
- `AnimationHandler` for view animations
- `AnimationConfig`, `OneCurveAnimation`, `TwoCurveAnimation` ScriptableObjects
- `OutPanel` system for UI backdrops
- `MVPCanvasSettings` for granular canvas control
- `IMVPManager` interface with Camera and Resource variants

#### Managers
- `IAddressablesManager` with full CRUD operations
- `ILocalizationManager` for localization
- `IWebRequestManager` for network requests
- `IAudioManager` with volume control interface
- `ISceneLoader` with Editor and Addressables implementations
- `IResourceLoader` for Resources management
- `IPlayerPrefsLoader` for PlayerPrefs

#### Editor Tools
- **Mu3Window**: Unified utility window
  - MVPHelper: Generate MVP boilerplate code
  - SceneList: Quick scene navigation
  - FileFinder: Asset search and organization
  - ScreenCapture: In-editor screenshots
  - DependencyChecker: Package dependency validation
- Custom property drawers for Observable types
- `ScriptBuilder`: Code generation helper

#### Samples
- **Sample_Template**: Comprehensive sample project
  - Sample_MVP: MVP pattern demonstration
  - Sample_Audio: Audio system showcase
  - Sample_Audio3D: 3D spatial audio example
  - Sample_WebRequest: HTTP request examples
  - Sample_Addressables: Asset loading demonstration
  - Sample_AddressablesAdditive: Additive scene loading
  - Sample_Localization: Multi-language support
  - LoadingScreen implementation
  - Splash screen with animations

#### Assets
- Basic color materials (Black, White, Red, Green, Blue, Magenta)
- Sample fonts (NotoSans, NotoSansJP, NotoSansKR with SDF)
- Sample BGM tracks (3 songs)
- Sample SFX sounds (3 effects)
- UI texture assets (circles with shadows, 1px square)
- Scene thumbnails for samples

### 🔧 Changed

#### Architecture
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Unity version requirement: 6000.0+ (Unity 6)
- Namespace restructure: All code under `Mu3Library.*`
- Assembly separation: Runtime and Editor assemblies

#### MVP System
- `Presenter` lifecycle completely redesigned
- `View` now supports animation integration
- Model-View-Presenter binding improved
- Canvas management centralized in `MVPCanvasSettings`
- View instantiation now supports Resources and Camera-based loading

#### Audio System
- Split into `BgmController` and `SfxController`
- Added `AudioSourceSettings` for fine-grained control
- 3D audio positioning support
- Volume change events through `IAudioVolumeSettings`

#### Scene Management
- Interface-based design with multiple implementations
- Progress events for loading operations
- Duplicate scene load policies
- Better async operation support

#### Observable Pattern
- Extended to support multiple primitive types
- Added collection types (List, Dictionary)
- Custom property drawers for editor integration
- Value change callbacks

#### Extensions
- Renamed `Overwrite` to `CopyTo`
- Organized by component type
- Added layer management helpers
- Camera property copying

### 🐛 Fixed

#### Critical Fixes
- **DI Container lifecycle bug**: Fixed service lifetime management issues
- Multiple interface implementation now correctly shares single instance
- Collection immutability: Made collections readonly where appropriate
- SceneLoader event timing: Corrected `OnSceneLoadEnd` callback timing
- Null reference handling throughout codebase

#### Stability Improvements
- AnimationView exception handling enhanced
- LocalizationManager initialization robustness
- Scene loading state management
- MVP view lifecycle edge cases

### 🗑️ Removed

#### Complete System Removals
- InputSystem helper classes and generated code
- MarchingCubes compute shader system
- CommandBuffer post-processing effects
- Camera controller components
- Custom UI components (DatePicker, IntSlider)
- Old sample scenes (6 samples removed)
- Shader collection (Toon, Blur, EdgeDetect, GrayScale, etc.)

#### Code Cleanup
- Removed unused utility functions
- Removed deprecated MVP implementation
- Removed old pool system (replaced with GameObjectPool)
- Removed old singleton implementation
- Removed legacy Observable implementation

### 📦 Dependencies

#### Added
- ✅ **com.cysharp.unitask**: UniTask for async operations
- ✅ **com.coplaydev.unity-mcp**: Unity MCP integration
- ✅ **com.unity.localization** (1.5.9): Localization support
- ✅ **com.unity.addressables** (implicitly through package manager)

#### Updated
- Unity 6000.0+ (Unity 6) required
- .NET Standard 2.1

#### Removed
- ❌ Old Unity-MCP package (IvanMurzak)

### 📚 Documentation

#### Added
- Multi-language README files (English, Japanese, Korean)
- MIT License
- Comprehensive inline documentation (XML comments)
- GitHub Copilot agent files for development assistance
- Unity-specific instruction files

#### Improved
- README with detailed feature descriptions
- Installation instructions (Git URL and local disk methods)
- Code examples for all major features
- Sample scene documentation

### 🔄 Migration Guide

#### For Users of v0.0.20

**⚠️ This is a major breaking release. A full project update is recommended.**

##### Step 1: Clean Installation
1. Remove the old package from your project
2. Delete any cached files in `Library/`
3. Install v0.1.11 using the new Git URL:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets
   ```

##### Step 2: Update Namespaces
```csharp
// Old (0.0.20)
using Mu3LibraryAssets;

// New (0.1.11)
using Mu3Library;
using Mu3Library.DI;
using Mu3Library.MVP;
```

##### Step 3: Migrate to DI Architecture
The new version uses Dependency Injection. Update your initialization code:

```csharp
// Old: Manual initialization
public class GameManager : MonoBehaviour
{
    private AudioManager audioManager;

    void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
    }
}

// New: DI-based approach
public class AudioCore : CoreBase
{
    protected override void ConfigureContainer(ContainerScope scope)
    {
        scope.Register<IAudioManager, AudioManager>(ServiceLifetime.Singleton);
    }
}

public class GameManager : CoreBase
{
    [Inject] private IAudioManager _audioManager;

    protected override void Start()
    {
        base.Start(); // Required for injection
        // Use _audioManager
    }
}
```

##### Step 4: Update MVP Code
If you were using the old MVP pattern:

```csharp
// Old Presenter
public class OldPresenter : Presenter<MyView, MyModel>
{
    // Old structure
}

// New Presenter
public class NewPresenter : Presenter<MyView, MyModel, MyArgs>
{
    // Must define Arguments class
}

public class MyArgs : Arguments { }
```

##### Step 5: Replace Removed Features
- **InputSystem**: Use Unity's Input System directly
- **Camera Controllers**: Implement custom or use third-party solutions
- **PostEffects**: Use Unity's Post Processing Stack or URP/HDRP volume system
- **Custom UI**: Use Unity's UI Toolkit or create custom components

##### Step 6: Update Resource Loading
```csharp
// Old: Static calls
var asset = ResourceLoader.Load<Sprite>("path");

// New: DI-based
public class MyCore : CoreBase
{
    [Inject] private IResourceLoader _resourceLoader;

    void LoadAsset()
    {
        _resourceLoader.Load<Sprite>("path", (sprite) => {
            // Use sprite
        });
    }
}
```

##### Step 7: Test Thoroughly
- Verify all DI injections are working
- Check MVP views are loading correctly
- Test audio playback
- Validate scene transitions
- Check Addressables loading if used

### 🎉 Acknowledgments

Special thanks to the open-source community for:
- UniTask by Cysharp (async/await support)
- Unity MCP by CoplayDev (Model Context Protocol)
- Sample audio assets from various Creative Commons sources

---

## [0.0.20] - Previous Release

### Added
- ObservableProperty implementation

For earlier versions, please refer to commit history.

[0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
