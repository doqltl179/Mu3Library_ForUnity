---
applyTo: "packages/**"
---
<!-- agentkit:generated from .ai/project/wiki/rules/package-architecture.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Package Architecture

What this repository decided for its package code. Generic Unity and UPM rules live in the `unity`, `unity-upm`, and `csharp` stack packs.

## When

- changing or reviewing package code, an `.asmdef`, a define symbol, a sample, or `package.json`.

## Route Away When

- which package or folder: [overview.md](.ai/project/wiki/overview.md),
- compile targets and EditMode tests: [unity-verification.md](.ai/project/wiki/unity-verification.md),
- manager initialization, disposal, and async operations, or the Watermelon board commands: [runtime-design.md](.ai/project/wiki/runtime-design.md),
- sample scenes and prefabs: [unity-yaml-guide.md](.ai/project/wiki/unity-yaml-guide.md),
- splitting a feature out of Base into a package of its own: [package-split.md](.ai/project/wiki/package-split.md).

## Package Surfaces

| Package | Surface | Rules |
|---|---|---|
| Base | Runtime `Runtime`: `Mu3Library.asmdef`, with three folders in assemblies of their own, `Foundation/` (`Mu3Library.Foundation.asmdef`, no engine references, with the DI contracts in `Foundation/DI/`), `DI/` (`Mu3Library.DI.asmdef`, the container), and `Multiplayer/` (`Mu3Library.Multiplayer.asmdef`, player-hosted sessions over a backend interface, with `Multiplayer/Steam/` in `Mu3Library.Multiplayer.Steam.asmdef`, the Steam backend, standalone players and the editor only) | Interface-first DI; preserve the `CoreBase` initialization and injection order |
| Base | Editor `Editor` (`Mu3Library.Editor.asmdef`) | Match nearby drawer, window, and utility patterns. Generated-script helpers live in `Editor/FileUtil`: identifier rules in `ScriptIdentifier`, output in `FileCreator`, asset path checks in `FileFinder` |
| URP | Runtime `Runtime` (`Mu3Library.URP.asmdef`) | URP-specific only; follow the renderer-feature and screen-effect patterns |
| URP | Shaders `Runtime/ScreenEffect/Shaders` | Minimal edits matching nearby property names, pass layout, and includes; state likely variant growth |
| Watermelon Game | Runtime `Runtime` (`Mu3Library.Game.WatermelonGame.asmdef`) | Board behavior stays in the package; reuse Base services instead of parallel audio, DI, or UI infrastructure |
| Notifications | Runtime `Runtime` (`Mu3Library.Notifications.asmdef`, Android, iOS, and Editor only) | Wraps the unified `NotificationCenter` of the Mobile Notifications package; uses no Mu3Library assembly |
| Base | `Samples~` | Sample-only orchestration and presentation; a defect in reusable behavior is fixed in the runtime surface |

## Assemblies And Optional Integrations

- Dependency direction: URP → Base, Watermelon Game → Base and URP, and Notifications references no Mu3Library assembly; inside Base, `Mu3Library` → `Mu3Library.Foundation`, `Mu3Library.DI` → `Mu3Library.Foundation`, `Mu3Library.Multiplayer` → `Mu3Library.Foundation` (for `IUpdatable`) and nothing else in Base, `Mu3Library.Multiplayer.Steam` → `Mu3Library.Multiplayer` and nothing else in Base, and the editor assembly `Mu3Library.Editor` → `Mu3Library`, `Mu3Library.Foundation`, `Mu3Library.DI` (for the DI tools: `DILinkerProcessor` and the DI validator and runtime diagnostics drawers), and `Mu3Library.Multiplayer` (for the Multiplayer Debugger window). Never reverse it, and add no other hard dependency between assemblies. An assembly that uses a DI contract, or converts a Base manager such as `AudioManager` to one of its interfaces, references `Mu3Library.Foundation` itself: Unity hands the compiler only an assembly's direct references, so the compile otherwise fails with CS0012, and only an assembly that uses the container, such as a core, references `Mu3Library.DI`. Diagnose assembly problems with the `asmdef-triage` skill before editing an `.asmdef`.
- Optional packages are gated by these `versionDefines` symbols, declared in the `.asmdef` of each assembly that uses the package:

| Package | Define symbol |
|---|---|
| `com.cysharp.unitask` | `MU3LIBRARY_UNITASK_SUPPORT` |
| `com.unity.addressables` | `MU3LIBRARY_ADDRESSABLES_SUPPORT` |
| `com.unity.localization` | `MU3LIBRARY_LOCALIZATION_SUPPORT` |
| `com.unity.inputsystem` | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` |
| `com.rlabrecque.steamworks.net` | `MU3LIBRARY_STEAMWORKS_SUPPORT` |

- Gated code lives in a split file named after the integration (`*.UniTask.cs`, `*.Addressables.cs`, `*.Addressables.UniTask.cs`, `*.InputSystem.cs`) or in that integration's folder (`Addressable/`, `Localization/`, `IS/`, `Multiplayer/Steam/`). `Multiplayer/Steam/` is its own assembly, `Mu3Library.Multiplayer.Steam`, which lists `MU3LIBRARY_STEAMWORKS_SUPPORT` and `UNITY_STANDALONE` in `defineConstraints`: without the package, or on a build target where Steamworks.NET hides its types, Unity skips the whole assembly, so its files need no `#if` for those symbols. The `full` project config installs Steamworks.NET so the assembly compiles there, and the other configs prove Base compiles without it.
- `package.json` `dependencies` declares every Unity package and built-in module that a package's runtime or editor code uses outside a define gate. A built-in module (`com.unity.modules.*`) is declared at `1.0.0`. A Unity package such as `com.unity.ugui`, `com.unity.render-pipelines.universal`, or `com.unity.mobile.notifications` is declared at the version the `unity` floor bundles (`2.0.0`, `17.3.0`, and `2.4.3` for `6000.3`, read on 6000.3.10f1 for the last); a higher version raises the floor in effect. The project configs enable every built-in module and uGUI, so `commands.build` passes on an undeclared use, and only a consuming project that turned the module off fails, inside a package it cannot edit.
- A module that only a sample under `Samples~` uses is not declared, and an optional integration from the table above never is: it stays behind its define gate. A package split out of Base instead declares the Unity-registry SDK it wraps and keeps no gate for it, since only a project that wants the SDK installs that package (ADR 0001 D21): `Mu3Library.Notifications` compiles wherever the Notifications package is installed, within its `includePlatforms`. UniTask and Steamworks.NET, distributed by Git, cannot be declared and keep their gates.

## DI, CoreBase, And MVP

- The DI contracts `IInitializable`, `IUpdatable`, `ILateUpdatable`, `IObjectInjector`, and `InjectAttribute` live in `Mu3Library.Foundation`, so Base's managers implement them without the container, which `Mu3Library.DI` holds (ADR 0001 D8). Keep the DI and MVP modules decoupled; use the existing DI rather than ad-hoc globals or new singletons. `ContainerScope.InjectInto` stays internal across that boundary: a container-owned service that uses `[Inject]` resolves the narrow `IObjectInjector`, which every scope provides.
- Use `MonoBehaviour` only for scene components; keep service and domain logic in plain C# classes.
- `Singleton<T>` and `GenericSingleton<T>` stay public for consuming projects even though the packages do not use them; do not remove them as unused.

## C# Conventions

- A namespace follows its folder under the `Mu3Library` root: `Foundation/Coroutine` is `Mu3Library.Foundation.Coroutine`, `Runtime/Utility` is `Mu3Library.Utility`. The existing exceptions stay, since renaming a public namespace breaks consumers, and no new one is added beyond the DI contracts in `Foundation/DI/`, which keep the namespace `Mu3Library.DI` they had in the DI assembly (ADR 0001 D8):
  - a leaf named like a Unity type its own code uses: `URP/Runtime/Camera` is `Mu3Library.URP.Cam`;
  - a folder that only groups the parts of one feature keeps its parent's namespace: Base's `UI/Area/Boundary` is `Mu3Library.UI.Area` in the runtime and `Mu3Library.Editor.UI.Area` in the editor;
  - a file of a partial class takes the namespace of the class it extends, since every part shares one: Watermelon Game's `Board/Area/BoardArea.*.cs` and `Board/Controller/BoardController.*.cs` are `Mu3Library.Game.WatermelonGame.Board`, while the other types in `Board/Area` follow the folder rule as `Mu3Library.Game.WatermelonGame.Board.Area`.
- An oversized `MonoBehaviour` keeps its serialized façade at its existing path and GUID and moves responsibilities into partial files grouped under named subfolders, such as `Controller`, `Area`, and `Item`.
- Explicit access modifiers on every member; interface names start with `I`.
- Log actionable problems with `Debug.LogWarning` or `Debug.LogError`; keep hot paths quiet. Catch exceptions at I/O, parsing, and network boundaries.
- Async code follows the coroutine or UniTask pattern already used nearby; reuse the existing pooling and utilities.
- Write three kinds of character as a `\uXXXX` escape, since a diff shows them as nothing or as something else: invisible and private-use characters (format, control, and private-use); data whose code point the code depends on but a reader cannot tell from a look-alike, such as an accented e against e or a full-width A against A in a string-comparison table; and the replacement character U+FFFD, which a lossy conversion leaves where it could not carry a character, so a literal one cannot be told from a lost character and a test meant to hold another character can pass holding it. Other visible symbols in UI text, such as arrows and bullets, and prose in comments, strings, and XML docs stay literal. `mu3-repo check` enforces part of this in package `.cs` files; what it reports is listed under `mu3-repo check` in [tools/cli/README.md](tools/cli/README.md).

## Review Focus

In order of severity:

1. Behavioral regressions.
2. Public API compatibility: interfaces, classes, methods, and properties.
3. Assembly boundary safety: minimal and correct `.asmdef` references, dependency direction.
4. Optional gate and dependency correctness: every optional-package use inside its define symbol, and every Unity package or built-in module newly used outside a gate declared in `package.json` `dependencies`.
5. Alignment: `.meta`-safe asset operations, README and CHANGELOG with their localized copies, and verification evidence.
