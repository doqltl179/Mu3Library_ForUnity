# Changelog

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](docs/changelog/CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](docs/changelog/CHANGELOG.ja.md)

</div>

All notable changes to Mu3Library For Unity will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.5] - 2026-03-14

### Changed
- `AddressableGroupNameExporterDrawer`: Sub-asset class names that begin with the parent class name now have the parent prefix stripped.
  - e.g. parent `Views`, sub-asset `ViewsDialoguePanelPrefab` → emitted as `DialoguePanelPrefab`.
  - Applied recursively through nested folder hierarchies.

## [0.4.4] - 2026-03-14

### Changed
- `AddressableGroupNameExporterDrawer`: Added folder entry support.
  - Folder entries registered in an Addressable group are now detected via `AssetDatabase.IsValidFolder()`.
  - Sub-assets inside a folder are collected with `GatherAllAssets()` and emitted as a nested `Assets` static class inside the folder entry class.
  - The editor preview marks folder entries with a `[Folder]` prefix and shows sub-assets indented beneath them.

## [0.4.3] - 2026-03-14

### Added
- `AddressableGroupNameExporterDrawer`: Added an editor drawer (guarded by `MU3LIBRARY_ADDRESSABLES_SUPPORT`) that reads all Addressable groups at editor time and exports their group names, asset names, addresses (keys), and labels as nested C# static classes.
  - `Labels` inner class provides individual `const string` fields per label and a `static readonly string[] All` containing all label values.
- `Sample_UtilWindow`: Added a sample `AddressableGroupNameExporter` drawer asset to the utility window drawer list.
- `Sample_Template`: Added `AddressableGroupKeys` as a generated example for Addressable group/address constants.
- `Mu3Library.Editor.asmdef`: Added optional references to `Unity.Addressables` and `Unity.Addressables.Editor` with `MU3LIBRARY_ADDRESSABLES_SUPPORT` version define.

## [0.4.2] - 2026-03-08

### Added
- `LocalizationNameExporterDrawer`: Added an editor drawer that exports Localization string table names and entry keys as C# constants for pre-declared lookup.
- `Sample_UtilWindow`: Added a sample `LocalizationNameExporter` drawer asset to the utility window drawer list.
- `Sample_Template`: Added `LocalizationTableKeys` as a generated example for Localization table/key constants.

### Changed
- `InputSystemNameExporterDrawer` and `LocalizationNameExporterDrawer`: Standardized private serialized helper member naming so backing fields and cached accessors are easier to distinguish while keeping behavior unchanged.

### Fixed
- `LocalizationNameExporterDrawer`: Fixed `SanitizeIdentifier` to produce proper PascalCase class names from entry keys. `-` and other non-identifier characters now act as word boundaries (dropped, next letter capitalized). `_` is preserved as-is and also capitalizes the next letter (e.g. `my-key_name` → `MyKey_Name`).

## [0.4.0] - 2026-03-08

### Added
- `AudioSourceSettings`: Added `LoopCount` and `LoopInterval` properties to control looping behavior per settings instance.
  - `LoopCount`: number of play cycles (`≤0` = infinite, `1` = one-shot).
  - `LoopInterval`: wait time in seconds between loop cycles.
- `AudioSourceSettings`: Added named preset instances for common use cases.
  - `Standard` (infinite loop, 2D), `OneShot` (play once, 2D)
  - `BgmStandard`, `BgmStandard3D`
  - `SfxStandard`, `SfxStandard3D`
  - `EnvironmentStandard`, `EnvironmentStandard3D`
- `Audio3dSoundSettings.Standard3D`: New preset with full 3D spatial blend (`spatialBlend = 1`).
- `AudioController`: Loop-with-interval playback driven by `LoopCount` and `LoopInterval` from `AudioSourceSettings`.
- `AudioController`: `FadeIn` / `FadeOut` coroutine API with optional completion callback.

### Changed
- `FadeInFirstSfx(AudioClip, float)` renamed to `FadeInSfx(AudioClip, float)` and behavior changed: now **plays a new SFX instance** from volume `0` and fades in, instead of targeting an already-playing instance.
- `FadeInFirstEnvironment(AudioClip, float)` renamed to `FadeInEnvironment(AudioClip, float)` with the same behavior change.
- `IAudioManager`: Removed `SourceSettings`, `BaseSettings`, and `SoundSettings` properties (superseded by per-call `AudioSourceSettings` parameter).
- `AudioManager` and `IAudioManager` refactored into partial class files split by category (`Bgm`, `Sfx`, `Environment`) for maintainability. No public API change.

## [0.3.3] - 2026-03-02

### Added
- `AudioManager`: Added `EnvironmentController` support for environment audio playback.
  - New `EnvironmentController` class: plays audio using `EnvironmentVolume` as its category volume.
  - `EnvironmentInstanceCountMax` property added (default: `3`, max: `5`).
  - `EnvironmentVolume`, `CalculatedEnvironmentVolume`, `ResetEnvironmentVolume()` added to `AudioManager` and `IVolumeSettings`.
  - `PlayEnvironment`, `StopFirstEnvironment`, `StopEnvironmentAll`, `PauseEnvironmentAll`, `UnPauseEnvironmentAll` methods added to `AudioManager` and `IAudioManager`.
  - `OnEnvironmentVolumeChanged` event added to `IAudioManagerEventBus`.
  - `Stop()`, `Pause()`, and `UnPause()` now include environment audio.

## [0.3.2] - 2026-03-02

### Fixed
- `Mu3WindowDrawer`: Added `DrawWithUndo<T>(Func<T>, Action<T>, string)` helper to the base class to eliminate repetitive `BeginChangeCheck` / `RecordObject` / `SetDirty` boilerplate in derived drawers.
- `Mu3WindowDrawer`: `DrawFoldoutHeader1` and `DrawFoldoutHeader2` now use `EditorGUI.BeginChangeCheck` / `EndChangeCheck` consistently instead of an explicit `!=` comparison.
- `DependencyCheckerDrawer`, `FileFinderDrawer`, `InputSystemNameExporterDrawer`, `MVPHelperDrawer`, `ScreenCaptureDrawer`: All interactive fields now correctly record undo/redo state via the new `DrawWithUndo<T>` helper.

## [0.3.1] - 2026-03-02

### Fixed
- `MVPManager`: Fixed a one-frame sync issue where the View rendered in its default prefab state before the open animation started.
  - Changed `Open()` to call `SetActiveView(false)` instead of `SetActiveView(true)`, and deferred `SetActiveView(true)` to just before `Open()` begins (after Load completes).
  - Animations (e.g. alpha 0→1) now start in sync with the View's intended initial state.

## [0.3.0] - 2026-03-01

### Added
- `InputSystemManager`: New Input System module (requires `MU3LIBRARY_INPUTSYSTEM_SUPPORT`):
  - Register `InputActionAsset` instances by custom ID; supports GUID-based and name-based action/map lookup.
  - Interactive rebinding via `StartInteractiveRebind(...)` with optional device-type filtering and cancel-control support.
  - Binding override serialization for per-asset, per-action-map, and per-action levels: save and load as JSON.
  - Enable/disable entire asset or individual action maps.
- `InputSystemNameExporterDrawer`: Editor drawer for exporting Input System action names as string constants.
- `LocalizationCharacterCollectorDrawer`: Editor drawer for collecting and reviewing characters across Localization string tables.
- `PresenterBase.CloseSelf(bool forceClose = false)`: A presenter can now close itself via the injected `IMVPManager` reference without needing an external caller.

### Changed
- `PresenterBase.Initialize(View, Arguments)` and `PresenterBase.Initialize(Arguments)` changed from `public` to `internal`.
  - Initialization is now managed exclusively by `MVPManager`; external code can no longer call these methods directly.
- `LayerCanvas` now synchronizes its Layer value to each child item automatically.

## [0.2.3] - 2026-02-16
### Changed
- Audio volume contract decoupled from event bus:
  - `IAudioVolumeSettings` no longer inherits `IAudioManagerEventBus`.
- Observable API now supports read-only exposure for external consumers:
  - Added `IObservableValue<TValue>` for `Value` + `Subscribe(...)` access.
  - `ObservableProperty<T>` and `ObservableDictionary<TKey, TValue>` now expose `ReadOnly`.
  - Subscription token handling was extracted to a dedicated `SubscriptionToken` file.
- MVP UI settings and runtime safety were improved:
  - `OutPanelSettings` is now serializable with explicit serialized backing fields.
  - `OutPanelSettings.Standard` default dim color alpha changed to `0.5f`.
  - `MVPManager` now validates `EventSystem` during focus updates and logs explicit errors if missing.

## [0.2.0] - 2026-02-14

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

[Unreleased]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.3...HEAD
[0.2.3]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.0...v0.2.3
[0.2.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.1.11...v0.2.0
[0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
