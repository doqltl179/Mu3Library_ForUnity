---
description: "Unity project architecture, coding conventions, and best practices for Mu3Library"
name: "Unity Development Standards"
---

# Mu3Library For Unity - Project Architecture & Coding Style

## Project Overview

Mu3Library is a **third-party package** for Unity projects, designed as a modular architecture framework that can be independently integrated into external projects. It supports scalable and maintainable game development based on a custom DI (Dependency Injection) system and the MVP (Model-View-Presenter) UI pattern.

## Core Architecture Patterns

### 1. Core-Based Module System
- **CoreBase**: The top-level component for each functional module that owns a DI scope and registers services
- **CoreRoot**: The root container that manages global Cores
- Each Core has an independent DI scope and registers services through the `ConfigureContainer()` method
- Dependencies between Cores are explicitly declared using the `[Inject(typeof(TargetCore))]` attribute

**Example Structure:**
```csharp
// Each Core handles an independent functional domain
public class AudioCore : CoreBase { }
public class UICore : CoreBase { }
public class NetworkCore : CoreBase { }
public class GameCore : CoreBase { }
```

### 2. Dependency Injection (DI) System
- **Custom DI Container** implementation (`Container`, `ContainerScope`)
- Three service lifetime types supported:
  - **Singleton**: Shares a single instance
  - **Transient**: Creates a new instance per request
  - **Scoped**: Shares a single instance within the scope
- Automatic dependency injection through `[Inject]` attribute
- Improved testability through interface-based registration

**DI Lifecycle:**
```csharp
protected override void ConfigureContainer(ContainerScope scope)
{
    // Register interfaces mapped to implementations
    scope.Register<IAudioManager, AudioManager>(ServiceLifetime.Singleton);
    scope.Register<IWebRequestManager, WebRequestManager>(ServiceLifetime.Singleton);
}

// Automatic injection (executed before CoreBase's Start())
[Inject] private IAudioManager _audioManager;
[Inject(typeof(UICore))] private IMVPManager _mvpManager;
```

### 3. MVP (Model-View-Presenter) UI Pattern
- **View**: Only handles references to Unity components and UI elements
- **Presenter**: Handles all business logic and event processing (testable)
- **Model**: Serves as a data container
- **Arguments**: Initial parameters passed when creating a Presenter

**MVP Lifecycle:**
1. `Load` → Resource loading and initialization
2. `Open` → Display UI and start animations
3. `Close` → Hide UI and end animations
4. `Unload` → Release resources

**Separation Principles:**
- View contains only references to Unity components (no logic)
- Presenter handles all business logic and event handling
- Model only stores data (no logic)

### 4. Interface-Based Lifecycle Management
When service classes implement the following interfaces, their lifecycle methods are automatically invoked:

- **IInitializable**: `Initialize()` - Service initialization point
- **IUpdatable**: `Update()` - Called every frame (Update)
- **ILateUpdatable**: `LateUpdate()` - Called on LateUpdate
- **IDisposable**: `Dispose()` - Service disposal point

```csharp
public class GameStateManager : IInitializable, IUpdatable, IDisposable
{
    public void Initialize() { /* Initialization logic */ }
    public void Update() { /* Update logic */ }
    public void Dispose() { /* Cleanup logic */ }
}
```

## Namespace Conventions

### Basic Structure
```
Mu3Library                              // Main library
├── DI                                  // Dependency Injection
├── UI.MVP                              // MVP pattern
├── Audio                               // Audio system
├── WebRequest                          // HTTP requests
├── Observable                          // Observable pattern
├── Extensions                          // Extension methods
└── Utility                             // Utility functions

Mu3Library.Sample.{SampleName}          // Sample projects
Mu3Library.Editor                       // Editor extensions
```

### Namespace Rules
- Base namespace: `Mu3Library`
- Feature-specific sub-namespaces: `Mu3Library.{FeatureName}`
- Editor extensions: `Mu3Library.Editor`
- Sample code: `Mu3Library.Sample.{SampleName}`

## Assembly Definition Structure

The project is separated into the following assemblies:

- **Mu3Library**: Main runtime assembly
- **Mu3Library.DI**: DI system (independent assembly)
- **Mu3Library.Editor**: Editor extension tools
- **Mu3Library.InputSystem**: Input System integration (conditional)
- **Mu3Library.Sample.{Name}**: Sample projects

## Coding Conventions

### 1. Naming Conventions
```csharp
// PascalCase
public class MyClass { }
public interface IMyInterface { }
public enum MyEnum { }
public struct MyStruct { }
public void PublicMethod() { }
public int PublicProperty { get; set; }

// camelCase with underscore prefix
private int _privateField;
private void PrivateMethod() { }

// camelCase (no underscore)
int localVariable;
void MethodParameter(int parameter) { }

// ALL_CAPS
private const int MAX_COUNT = 100;
```

### 2. Explicit Access Modifiers
```csharp
// Always explicitly declare access modifiers for all members
public class Example
{
    private int _value;           // ✓ Explicit
    public int Value { get; }     // ✓ Explicit

    int _implicitValue;           // ✗ Implicit (forbidden)
}
```

### 3. Avoid Singleton Pattern
- Dependency injection through DI system is recommended
- Use `Singleton<T>` or `GenericSingleton<T>` only when unavoidable
- Minimize global state

### 4. MonoBehaviour Usage Rules
```csharp
// Use MonoBehaviour only for components attached to GameObjects
public class PlayerController : MonoBehaviour { }

// Regular service classes should be POCOs
public class GameDataManager // Do NOT inherit from MonoBehaviour
{
    [Inject] private ISaveSystem _saveSystem;
    // ...
}
```

### 5. ScriptableObject Utilization
```csharp
// Data containers, configuration files, shared resources
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Config")]
public class GameConfig : ScriptableObject
{
    public int MaxHealth = 100;
    public float MoveSpeed = 5f;
}
```

### 6. Asynchronous Processing
```csharp
// Use Coroutines (default)
private IEnumerator LoadDataCoroutine()
{
    yield return new WaitForSeconds(1f);
    // Load data...
}

// Use UniTask (when MU3LIBRARY_UNITASK_SUPPORT is enabled)
#if MU3LIBRARY_UNITASK_SUPPORT
private async UniTask LoadDataAsync()
{
    await UniTask.Delay(1000);
    // Load data...
}
#endif
```

## Conditional Compilation

External package dependencies are managed with conditional defines:

```csharp
#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

public partial interface IWebRequestManager
{
    UniTask<TResponse> GetAsync<TResponse>(string url);
}
#endif

#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using UnityEngine.AddressableAssets;

public partial interface IResourceLoader
{
    void LoadAddressable<T>(string key, Action<T> onSuccess);
}
#endif
```

**Supported Define Symbols:**
- `MU3LIBRARY_UNITASK_SUPPORT` - UniTask package
- `MU3LIBRARY_ADDRESSABLES_SUPPORT` - Unity Addressables
- `MU3LIBRARY_LOCALIZATION_SUPPORT` - Unity Localization
- `MU3LIBRARY_INPUTSYSTEM_SUPPORT` - Unity Input System

## Project Structure Principles

### 1. Module Independence
- Each module must be capable of operating independently
- External package dependencies are separated through conditional compilation
- Circular references are prohibited

### 2. Interface-First Approach
```csharp
// Abstraction through interfaces
public interface IAudioManager
{
    void PlayBgm(AudioClip clip, float fadeTime = 0f);
    void PlaySfx(AudioClip clip, float volume = 1f);
}

// Map interface to implementation during DI registration
scope.Register<IAudioManager, AudioManager>(ServiceLifetime.Singleton);

// Inject as interface when using
[Inject] private IAudioManager _audioManager;
```

### 3. Extension Method Utilization
```csharp
// Define extension methods in Extensions namespace
namespace Mu3Library.Extensions
{
    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            return go.GetComponent<T>() ?? go.AddComponent<T>();
        }
    }
}
```

### 4. Observable Pattern Usage
```csharp
// When data change detection is needed
public class PlayerData
{
    public ObservableInt Health = new ObservableInt();
    public ObservableString PlayerName = new ObservableString();
}

// Subscribe and handle events
_playerData.Health.AddEvent(newValue => UpdateUI(newValue));
_playerData.Health.Set(80); // Automatically publishes event
```

## Error Handling and Debugging

### 1. Logging Rules
```csharp
// Use Unity's Debug class
Debug.Log($"[AudioManager] BGM started: {clip.name}");
Debug.LogWarning($"[AudioManager] Volume too high: {volume}");
Debug.LogError($"[AudioManager] Audio clip is null!");

// Conditional logging (removed in Release builds)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
Debug.Log($"[DEBUG] Current state: {state}");
#endif
```

### 2. Using Assertions
```csharp
// Validate logical errors
Debug.Assert(health >= 0, "Health cannot be negative!");
Debug.Assert(player != null, "Player reference is missing!");
```

### 3. Try-Catch Usage
```csharp
// Use for file I/O, network operations, JSON parsing, etc.
try
{
    var data = JsonUtility.FromJson<GameData>(jsonString);
}
catch (Exception ex)
{
    Debug.LogError($"Failed to parse JSON: {ex.Message}");
}
```

## Performance Optimization Guidelines

### 1. Object Pooling
```csharp
// Utilize ObjectPool module
public class BulletPool : ObjectPool<Bullet>
{
    protected override Bullet CreatePooledItem()
    {
        return Instantiate(_bulletPrefab);
    }
}

// Usage
var bullet = _bulletPool.Get();
// ...
_bulletPool.Release(bullet);
```

### 2. Optimize Frequently Called Methods
```csharp
// Cache GetComponent results
private Rigidbody _rb;

private void Awake()
{
    _rb = GetComponent<Rigidbody>(); // Cache
}

private void Update()
{
    _rb.velocity = Vector3.forward; // Use cached reference
}
```

### 3. Asynchronous Loading
```csharp
// Handle heavy operations asynchronously
private IEnumerator LoadLevelAsync()
{
    var asyncLoad = SceneManager.LoadSceneAsync("MainLevel");
    while (!asyncLoad.isDone)
    {
        UpdateLoadingProgress(asyncLoad.progress);
        yield return null;
    }
}
```

## Testing Strategy

### 1. Interface-Based Design Enables Mock Testing
```csharp
// Production code
public class GameController
{
    [Inject] private IAudioManager _audioManager;

    public void OnVictory()
    {
        _audioManager.PlaySfx(_victorySfx);
    }
}

// Test code
public class MockAudioManager : IAudioManager
{
    public bool PlaySfxCalled { get; private set; }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        PlaySfxCalled = true;
    }
}
```

### 2. Unity Test Framework Utilization
- EditMode tests: Unit testing for logic
- PlayMode tests: Integration tests, MonoBehaviour tests

## Important Considerations

### 1. Compatibility with External Projects
- This project is designed as an independent package
- Internal rules take priority over external project coding styles
- Prevent namespace conflicts: All classes must be under the `Mu3Library` namespace hierarchy

### 2. Unity Version Compatibility
- Developed for Unity 6 (6000.0+)
- Uses .NET Standard 2.1
- While backward compatibility is considered, prioritize leveraging latest features

### 3. Package Dependency Management
- Minimize required dependencies
- Separate optional packages with conditional compilation
- Each functional module must be capable of operating independently

### 4. Separate Editor Extensions from Runtime
- Editor-only code should use `Editor/` folder or `#if UNITY_EDITOR`
- Separate editor assemblies using Assembly Definition

## Additional Recommendations

### 1. Write XML Documentation Comments
```csharp
/// <summary>
/// Plays a background music with optional fade-in effect.
/// </summary>
/// <param name="clip">The audio clip to play.</param>
/// <param name="fadeTime">Duration of fade-in effect in seconds.</param>
public void PlayBgm(AudioClip clip, float fadeTime = 0f)
{
    // Implementation
}
```

### 2. Utilize Partial Classes
```csharp
// Can separate files by functionality
public partial class WebRequestManager // WebRequestManager.cs
{
    public void Get<TResponse>(string url, Action<TResponse> onSuccess) { }
}

public partial class WebRequestManager // WebRequestManager.UniTask.cs
{
#if MU3LIBRARY_UNITASK_SUPPORT
    public async UniTask<TResponse> GetAsync<TResponse>(string url) { }
#endif
}
```

### 3. Unsubscribe from Events
```csharp
// Automatic cleanup through IDisposable
public class UIController : IDisposable
{
    [Inject] private IAudioManager _audioManager;

    public void Initialize()
    {
        _audioManager.OnVolumeChanged += OnVolumeChanged;
    }

    public void Dispose()
    {
        _audioManager.OnVolumeChanged -= OnVolumeChanged;
    }

    private void OnVolumeChanged(float volume) { }
}
```

## Summary

Mu3Library follows these core principles:

1. **Modularization**: Functional separation through Core-based system
2. **Dependency Injection**: Reduced coupling with custom DI container
3. **MVP Pattern**: Separation of UI logic and business logic
4. **Interface-First**: Improved testability and extensibility
5. **Conditional Compilation**: Management of optional package dependencies
6. **Independence**: Prioritize self-contained completeness as a third-party package
7. **Unity Best Practices**: Adherence to official Unity guidelines

These principles enable building scalable and maintainable Unity projects.