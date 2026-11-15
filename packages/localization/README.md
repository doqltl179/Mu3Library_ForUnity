# Mu3 Library Localization

Localization package of **Mu3Library For Unity**: `LocalizationManager` wraps Unity's Localization package. It initializes the package and reports the result, reads strings and localized assets by table and key, lists the keys of a table and the available locales, and changes the locale by locale, by English name, or to the system language, announcing every locale change, including one made outside the manager. In the editor, the **Localization Data Exporter** drawer generates scripts of the locales, tables, and entries, and the **Localization Character Collector** drawer writes the characters the string tables use to a text file per locale.

- Package name: `com.github.doqltl179.mu3library.localization`
- Assemblies: `Mu3Library.Localization`, with the namespaces `Mu3Library.Localization` and `Mu3Library.Localization.Data`, and the editor assembly `Mu3Library.Localization.Editor`, which holds the drawers in the namespace `Mu3Library.Editor.Window.Drawer`.
- Depends on Unity Localization (`com.unity.localization`) and on Unity Addressables (`com.unity.addressables`), whose operation handles the manager waits on; Package Manager installs both with this package. It also depends on Mu3 Library (Base), whose event and DI contracts the manager implements and whose editor window tools the drawers build on.
- With [UniTask](https://github.com/Cysharp/UniTask) installed, the async variants of the manager are available, as in Base.

## Installation

The package ships with the next release, and its Git URL then joins the others under Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation). Until then, add `packages/localization/package.json` from disk. Install Base first: a Git URL install does not resolve the Base dependency by itself.

Up to Base 0.28.0, `LocalizationManager`, the `Mu3Library.Localization.Data` types, and the drawers came with Base, compiled into the `Mu3Library` and `Mu3Library.Editor` assemblies while the Localization package was installed. A project that uses them installs this package when it updates Base past 0.28.0. The namespaces and the GUIDs in the `.meta` files are unchanged, so code without an assembly definition of its own needs no change, and a drawer asset made from either drawer keeps its script. An assembly definition that references `Mu3Library` for `LocalizationManager` or for the scripts the exporter generated adds a reference to `Mu3Library.Localization`, and one that references `Mu3Library.Editor` for the drawers adds `Mu3Library.Localization.Editor`.

## Usage

Initialize the manager once; the callback, or the result callback, runs when the Localization package has initialized, and at once when it already has. A string or an asset is read from a table by its key.

```csharp
using Mu3Library.Localization;
using UnityEngine;
using UnityEngine.Localization;

public class LanguageMenu : MonoBehaviour
{
    private LocalizationManager _localization;

    private void Start()
    {
        _localization = new LocalizationManager();
        _localization.OnLocaleChanged += OnLocaleChanged;
        _localization.InitializeWithResult((isSuccess, error) =>
        {
            if (!isSuccess)
            {
                Debug.LogError(error);
                return;
            }

            _localization.ChangeLocaleToNative();
            _localization.GetString("UI", "Title", title => Debug.Log(title));
        });
    }

    private void Update()
    {
        _localization.Update();
    }

    public void SelectJapanese()
    {
        _localization.ChangeLocaleWithEnglishName("Japanese");
    }

    private void OnLocaleChanged(Locale locale)
    {
        Debug.Log($"Locale: {locale.Identifier.Code}");
    }

    private void OnDestroy()
    {
        _localization.Dispose();
    }
}
```

`Update` reports the initialization progress through `OnInitializeProgress`. `OnLocaleChanged` reports every selected locale change, one made outside the manager included, and `CurrentLocale` follows it. Without a `LocalizationSettings` asset in the project, a string reads as empty, a list as empty, an asset as `null`, and the current locale as an English locale the manager creates and destroys in `Dispose`.

With UniTask installed, `InitializeAsync`, `GetStringAsync`, `GetAssetAsync`, `GetSelectedLocaleAsync`, and the `ChangeLocale…Async` methods await the same work. `ChangeLocaleAsync` completes once the Localization package has initialized again for the new locale and announced it; a newer call or `CancelChangeLocale` ends the wait early, and `IsLocaleChanging` is true while it waits.

With the Mu3 Library DI package ([README](../di/README.md)), a core registers the manager like any other service, as a singleton under `LocalizationManager` and its interfaces `ILocalizationManager` and `ILocalizationManagerEventBus`, and the core calls its `Update`:

```csharp
using Mu3Library.DI;
using Mu3Library.Localization;

public class LocalizationCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        RegisterClass<LocalizationManager>();
    }
}
```

### Localization Data Exporter

Create the drawer with **Assets > Create > Mu3Library > Editor Window > Window Drawers > Localization Data Exporter** and add it to the drawer list of a `Mu3Window`. Pick a script folder inside `Assets`; the namespace, the class name, which defaults to `LocalizationKeys`, and a define symbol are optional. **Generate C# Scripts** writes a script of `LocaleData` constants for the locales, one script per string table with an `EntryData` for each entry, and a root class that holds the tables, so code reads a string without typing its table and key:

```csharp
string title = localization.GetString(LocalizationKeys.UI.Title);
```

A define symbol wraps every generated script in `#if` on that symbol, so the scripts compile only where it is defined.

### Localization Character Collector

Create the drawer with **Assets > Create > Mu3Library > Editor Window > Window Drawers > Localization Character Collector** and add it to the drawer list of a `Mu3Window`. Pick the string tables and the locales, or all of them, extra characters to include, an output folder, and a file name. **Collect & Save** writes, for each locale, the characters its entries use, each once, to `<file name>_<locale code>.txt`, for example to build a font atlas that holds every character the game shows.

## Documentation

- [Repository README](https://github.com/doqltl179/Mu3Library_ForUnity)
- Release history: this package's [CHANGELOG](CHANGELOG.md), full detail in the repository root `CHANGELOG.md`.
