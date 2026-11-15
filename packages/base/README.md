# Mu3 Library (Base)

Core runtime package of **Mu3Library For Unity**: the `CoreBase`/DI container, MVP UI system, audio, scene loading, WebRequest, Observable, player-hosted multiplayer sessions, and the shared editor tooling.

- Package name: `com.github.doqltl179.mu3library.base`
- Optional integrations (enabled by define symbols when the package is present): UniTask, Addressables, Input System, Steamworks.NET (separate `Mu3Library.Multiplayer.Steam` assembly). [Optional Packages](https://github.com/doqltl179/Mu3Library_ForUnity#-optional-packages) in the repository README gives their define symbols and platform limits.
- Local notifications for Android and iOS are in the separate Notifications package, `com.github.doqltl179.mu3library.notifications` ([README](../notifications/README.md)), from the next release.
- `InputSystemManager` and its name exporter drawer are in the separate Input System package, `com.github.doqltl179.mu3library.inputsystem` ([README](../inputsystem/README.md)), from the next release. Base keeps the Input System integration of `MVPManager`, which picks the input module of the `EventSystem` it creates.
- `LocalizationManager` and its data exporter and character collector drawers are in the separate Localization package, `com.github.doqltl179.mu3library.localization` ([README](../localization/README.md)), from the next release.
- `AddressablesManager` and its group data exporter drawer are in the separate Addressables package, `com.github.doqltl179.mu3library.addressables` ([README](../addressables/README.md)), from the next release. Base keeps the Addressables scene loading of `SceneLoader`.

## Documentation

The repository root carries the full documentation:

- [Repository README](https://github.com/doqltl179/Mu3Library_ForUnity)
- Release history: this package's [CHANGELOG](CHANGELOG.md), full detail in the repository root `CHANGELOG.md`.
