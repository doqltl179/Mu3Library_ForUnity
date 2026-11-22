# Mu3 Library Addressables

Addressables package of **Mu3Library For Unity**: `AddressablesManager` wraps Unity's Addressables package. It initializes the package and reports the result, loads and caches an asset, a list of assets, or a dictionary of assets keyed by their primary keys under one key, queries download sizes and downloads dependencies with progress, checks for and updates catalogs one call at a time, and releases everything a key loaded. In the editor, the **Addressable Group Data Exporter** drawer generates scripts of the labels, groups, and entries of the project's Addressable groups.

- Package name: `com.github.doqltl179.mu3library.addressables`
- Assemblies: `Mu3Library.Addressables`, with the namespaces `Mu3Library.Addressable` and `Mu3Library.Addressable.Data`, and the editor assembly `Mu3Library.Addressables.Editor`, which holds the drawer in the namespace `Mu3Library.Editor.Window.Drawer`.
- Depends on Unity Addressables (`com.unity.addressables`), which Package Manager installs with this package, and on Mu3 Library (Base), whose event and DI contracts the manager implements and whose editor window tools the drawer builds on.
- With [UniTask](https://github.com/Cysharp/UniTask) installed, the async variants of the manager are available, as in Base.

## Installation

Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation) gives the package's Git URL with its latest release tag. Install Base first: a Git URL install does not resolve the Base dependency by itself.

Up to Base 0.28.0, `AddressablesManager`, the `Mu3Library.Addressable.Data` types, and the drawer came with Base, compiled into the `Mu3Library` and `Mu3Library.Editor` assemblies while the Addressables package was installed. A project that uses them installs this package when it updates Base past 0.28.0. The namespaces and the GUIDs in the `.meta` files are unchanged, so code without an assembly definition of its own needs no change, and a drawer asset made from the exporter keeps its script. An assembly definition that references `Mu3Library` for `AddressablesManager` or for the scripts the exporter generated adds a reference to `Mu3Library.Addressables`, and one that references `Mu3Library.Editor` for the drawer adds `Mu3Library.Addressables.Editor`. Base keeps the Addressables scene loading of `SceneLoader`, which still compiles whenever the Addressables package is installed.

## Usage

Initialize the manager once: the callback of `Initialize` runs once Addressables has initialized, and the result callback of `InitializeWithResult` once the initialization has succeeded or failed, each at once when Addressables already has. Load all assets resolved by a key into a `Dictionary<string, T>` indexed by the matching resource location's `PrimaryKey`. `PrimaryKey` values must be non-empty and unique; otherwise the load operation fails.

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

`LoadAssets` hands each asset to its per-asset callback and then the list to its callback, and `LoadAssetsAsync` hands each asset to its per-asset callback before it returns the list. Both do so whether the call starts the load, joins a load of the same key and type still in progress, or is served from the cache. A call that joins a load in progress receives its per-asset calls all at once when the load completes, not as each asset loads.

The manager caches what it loads under the key it was loaded with. `LoadAsset` and the list loads count no callers: `ReleaseCachedAsset(key)` releases every handle the manager holds for that key, for every caller that loaded it, and `ClearCache` does the same for every key, so when several features load the same key that way, one owner releases it, once none of them needs its assets any more.

To let each feature release only its own share of an asset, load it with a handle: `LoadAssetHandle` and `LoadAssetHandleAsync` load as `LoadAsset` and `LoadAssetAsync` do and hand each caller an `AssetHandle<T>`, an `IDisposable` with the `Asset`. The manager counts the handles of each key and type and releases the cached asset when the last one is disposed; disposing a handle again counts nothing, and a call that joins a load in progress gets its handle when the load completes. `ReleaseCachedAsset` and `ClearCache` still release the asset whatever handles remain, which then read `IsValid` false and hand a null `Asset`, and disposing them afterwards releases nothing. A load that fails hands `LoadAssetHandle`'s callback a handle that is not valid, while `LoadAssetHandleAsync` throws, as `LoadAssetAsync` does. Mix the two kinds of load of one key and type only if the handles' owners may release the asset under a caller of `LoadAsset`.

```csharp
AssetHandle<GameObject> enemy = await _addressablesManager.LoadAssetHandleAsync<GameObject>("enemy");
Instantiate(enemy.Asset);
// ...once this screen no longer needs it; another screen's handle keeps the asset loaded.
enemy.Dispose();
```

Catalog checks and updates, `CheckForCatalogUpdates`, `UpdateCatalogs`, and their UniTask variants, reach Addressables one at a time in call order. A call made while another one runs, from that call's callback included, starts on the ResourceManager update after the earlier callback, once its handle is settled, and `Dispose` reports a call that has not reached Addressables yet with null. An update reaches Addressables only once the manager is initialized: a manager that has not initialized yet runs the initialization `Initialize` runs, with the same `IsInitializing` state and events, and an update made while one runs waits for it. If that initialization fails, the update's callback receives null, and later updates fail the same way without another attempt until an initialize call succeeds.

With the Mu3 Library DI package ([README](../di/README.md)), a core registers the manager like any other service, as a singleton under `AddressablesManager` and its interfaces `IAddressablesManager` and `IAddressablesManagerEventBus`, and the core calls its `Update`, which reports the initialization and download progress through `OnInitializeProgress` and `OnDownloadProgress`:

```csharp
using Mu3Library.Addressable;
using Mu3Library.DI;

public class AddressablesCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        RegisterClass<AddressablesManager>();
    }
}
```

While the game runs, the **Runtime Diagnostics** drawer of the DI package lists, for each `AddressablesManager` a registered core holds, how many assets it caches, how many load handles it tracks, and the keys it caches assets for.

### Addressable Group Data Exporter

Create the drawer with **Assets > Create > Mu3Library > Editor Window > Window Drawers > Addressable Group Data Exporter** and add it to the drawer list of a `Mu3Window`. Pick a script folder inside `Assets`; the namespace, the class name, which defaults to `AddressableGroups`, and a define symbol are optional. **Generate C# Scripts** writes a script of the labels, one script per group with an `EntryData` for each entry, its sub-entries nested inside it, and a root class that holds the groups as `GroupData`, so code reads an entry's address and labels without typing them.

A define symbol wraps every generated script in `#if` on that symbol, so the scripts compile only where it is defined.

## Documentation

- [Repository README](https://github.com/doqltl179/Mu3Library_ForUnity)
- Release history: this package's [CHANGELOG](CHANGELOG.md), full detail in the repository root `CHANGELOG.md`.
