# Mu3 Library DI

DI package of **Mu3Library For Unity**: a dependency injection container built around `CoreBase` components. Each core owns a container in which it registers services as singletons, transients, or scoped services; the container builds them through their constructors, fills their `[Inject]` fields and properties, and calls their lifecycle interfaces, and `CoreRoot` runs the cores in a stable order and lets one core reach the services of another. In the editor, the package hands the linker a link.xml for each player build, so that managed code stripping keeps what the container reaches through reflection, the **DI Validator** drawer checks the registrations of the running cores, and the **Runtime Diagnostics** drawer shows what their services hold.

- Package name: `com.github.doqltl179.mu3library.di`
- Assemblies: `Mu3Library.DI`, with the namespace `Mu3Library.DI`, and the editor assembly `Mu3Library.DI.Editor`, which holds the link.xml processor in the namespace `Mu3Library.Editor.DI` and the drawers in the namespace `Mu3Library.Editor.Window.Drawer`.
- Depends on Mu3 Library (Base), whose `Mu3Library.Foundation` assembly holds the contracts the container works with, `IInitializable`, `IUpdatable`, `ILateUpdatable`, `IObjectInjector`, and `InjectAttribute`, in the namespace `Mu3Library.DI`, and whose editor window tools the drawers build on.
- With [UniTask](https://github.com/Cysharp/UniTask) installed, a core prepares through UniTask; without it, through a coroutine.

## Installation

Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation) gives the package's Git URL with its latest release tag. Install Base first: a Git URL install does not resolve the Base dependency by itself.

Up to Base 0.28.0, the container came with Base in the `Mu3Library.DI` assembly, and the link.xml processor and the drawers in the `Mu3Library.Editor` assembly. A project that uses them installs this package when it updates Base past 0.28.0. The assembly name `Mu3Library.DI`, the namespaces, and the GUIDs in the `.meta` files are unchanged, so code needs no change, an assembly definition that references `Mu3Library.DI` by name or by GUID keeps working, and a drawer asset made from either drawer keeps its script. The drawers compile into the new editor assembly: an assembly definition that references `Mu3Library.Editor` for `DIValidatorDrawer` or `RuntimeDiagnosticsDrawer` adds `Mu3Library.DI.Editor`.

Base works without this package. Its managers implement the contracts in `Mu3Library.Foundation`, so a project that uses another container registers them there and calls their `Update`, and `MVPManager` leaves the `[Inject]` members of the presenters it creates unset while nothing has given it an `IObjectInjector`.

## Usage

A core registers its services in `ConfigureContainer`; a field or property marked `[Inject]` receives a service of the same core, or, with the core's type, of another core.

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

A core that overrides `Awake`, `Start`, `OnEnable`, `OnDisable`, or `OnDestroy` must call the base method: together they register the core with `CoreRoot`, inject it, prepare it, and take it out again.

`[Inject]` members that a base class declares, private ones included, are injected as well; the search up the class hierarchy stops at `CoreBase`. The editor DI validator checks the same members.

A required `[Inject]` property needs a setter, which may be private, a required `[Inject]` field must not be `readonly`, and neither may be `static`. When the container injects an object with a required `[Inject]` member that it can never fill, a get-only property, one whose setter the linker removed in a stripped player, a `readonly` field, or a `static` field or property, it throws `InvalidOperationException` before it injects any member, as it does for a required member whose service is not registered. A `static` member belongs to the class, so the container never writes one: each object it injects would overwrite the value the other objects of the class read. The editor DI validator reports such a member on the classes the container creates and on the core itself. It does not check registered instances, which matches what runs: `ContainerScope.RegisterInstance` injects an instance as it registers it, so such a member makes it throw and leave nothing registered, and `Container.RegisterInstance` does not inject the instance it registers, so nothing throws for it. `InjectableMembers.GetUnwritableProperties`, `InjectableMembers.GetReadOnlyFields`, `InjectableMembers.GetStaticFields`, and `InjectableMembers.GetStaticProperties` list such members. An optional one (`[Inject(false)]`) is skipped.

Registered services also receive `[Inject]` field and property injection after construction and before lifecycle callbacks. One registration, which is its implementation type, key, and lifetime, yields one instance whatever service type resolves it: one per container for a singleton and one per scope for a scoped service. A lifecycle callback that throws is logged with `Debug.LogException`, and the other services and cores keep running.

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

### Cores and CoreRoot

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

To access services from different Cores:

```csharp
// Method 1: Manual acquisition in Start()
protected override void Start()
{
    base.Start();
    _audioManager = GetClassFromOtherCore<AudioCore, IAudioManager>();
}

// Method 2: Inject attribute (in a core, a registered service, or a presenter)
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

### Managed Code Stripping

In a player built with **Managed Stripping Level** set to Medium or High, the Unity linker removes code that nothing calls, and the container reaches constructors and `[Inject]` members only through reflection. For each such build, this package's editor therefore hands the linker a link.xml. It keeps the constructors of every class the build keeps anyway in the assemblies that reference `Mu3Library.DI` or `Mu3Library.Foundation`, which holds `[Inject]` and the lifecycle interfaces, and in every script or precompiled assembly they reference, directly or not, so a service may live in an assembly that knows nothing of the container, and it keeps the `[Inject]` fields and properties of those classes, `static` ones too when they are required, so that the container finds them and throws in the player as it does in the Editor. A class that declares one of those fields keeps all its fields, since the linker keeps the `[Inject]` attribute of a field that a link.xml names only when code reads the field, and the container finds the field by that attribute: an `[Inject]` field that is there only so that the container creates a service, and that no code reads, would otherwise stay empty. Classes and assemblies that nothing uses are still removed. The `[Inject]` members are read from the assemblies the Editor loads, so an `[Inject]` member that only the player compiles, inside `#if !UNITY_EDITOR` for example, needs `[Preserve]` (`UnityEngine.Scripting`), and so does code that only your own reflection reaches. Precompiled assemblies are matched by file name, so a DLL whose file name differs from its assembly name is skipped without a warning, and a DLL the Editor does not load is covered only when one of those assemblies references it. A `link.xml` under the project's `Assets` folder works as well; Unity does not read one inside a package. The constructor entries cost little: in the Watermelon Game development project on Unity 6000.6.7f1 with Managed Stripping Level High, they added 21,504 bytes (0.19%) to the managed DLLs of a Mono player, most of it in Unity package assemblies such as the render pipeline's and the Input System's, and 123,904 bytes (0.52%) to `GameAssembly.dll` of an IL2CPP one.

### DI Validator and Runtime Diagnostics

Create each drawer with **Assets > Create > Mu3Library > Editor Window > Window Drawers > DI Validator** or **Runtime Diagnostics** and add it to the drawer list of a `Mu3Window`. Both read the cores registered with `CoreRoot` while the game runs and change nothing.

- **DI Validator** lists how many registrations each core has and reports, before the first resolve or injection throws, a registered implementation whose constructor or required `[Inject]` members cannot be resolved, and a required `[Inject]` member the container can never fill on a registered implementation or on the core itself, as described above. Reading the registrations resolves nothing.
- **Runtime Diagnostics** lists, for each core, the presenters its `MVPManager` manages, and, while the Addressables package (`com.github.doqltl179.mu3library.addressables`) is installed, how many assets each `AddressablesManager` caches, how many load handles it tracks, and the keys it caches assets for.

## Documentation

- [Repository README](https://github.com/doqltl179/Mu3Library_ForUnity)
- Release history: this package's [CHANGELOG](CHANGELOG.md), full detail in the repository root `CHANGELOG.md`.
