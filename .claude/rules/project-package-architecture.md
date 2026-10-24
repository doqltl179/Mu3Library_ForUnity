---
paths:
  - "Mu3Library_*/**"
---
<!-- agentkit:generated from .ai/project/wiki/rules/package-architecture.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Package Architecture

What this repository decided for its package code. Generic Unity and UPM rules live in the `unity`, `unity-upm`, and `csharp` stack packs.

## When

- changing or reviewing package code, an `.asmdef`, a define symbol, a sample, or `package.json`.

## Route Away When

- which package or folder: [overview.md](.ai/project/wiki/overview.md),
- compile targets and EditMode tests: [unity-verification.md](.ai/project/wiki/unity-verification.md),
- sample scenes and prefabs: [unity-yaml-guide.md](.ai/project/wiki/unity-yaml-guide.md).

## Package Surfaces

| Package | Surface | Rules |
|---|---|---|
| Base | Runtime `Runtime/Scripts`: `Mu3Library.asmdef`, with three folders in assemblies of their own, `Foundation/` (`Mu3Library.Foundation.asmdef`, no engine references), `DI/` (`Mu3Library.DI.asmdef`), and `Notifications/` (`Mu3Library.Notifications.asmdef`, Android, iOS, and Editor only) | Interface-first DI; preserve the `CoreBase` initialization and injection order |
| Base | Editor `Editor/Scripts` (`Mu3Library.Editor.asmdef`) | Match nearby drawer, window, and utility patterns. Generated-script helpers live in `Editor/Scripts/FileUtil`: identifier rules in `ScriptIdentifier`, output in `FileCreator`, asset path checks in `FileFinder` |
| URP | Runtime `Runtime/Scripts` (`Mu3Library.URP.asmdef`) | URP-specific only; follow the renderer-feature and screen-effect patterns |
| URP | Shaders `Runtime/Shaders` | Minimal edits matching nearby property names, pass layout, and includes; state likely variant growth |
| Watermelon Game | Runtime `Runtime/Scripts` (`Mu3Library.Game.WatermelonGame.asmdef`) | Board behavior stays in the package; reuse Base services instead of parallel audio, DI, or UI infrastructure |
| All | `Samples~` | Sample-only orchestration and presentation; a defect in reusable behavior is fixed in the runtime surface |

## Assemblies And Optional Integrations

- Dependency direction: URP → Base, Watermelon Game → Base and URP; inside Base, `Mu3Library` → `Mu3Library.DI` → `Mu3Library.Foundation`, and `Mu3Library.Notifications` references none of them. Never reverse it, and add no other hard dependency between assemblies. Diagnose assembly problems with the `asmdef-triage` skill before editing an `.asmdef`.
- Optional packages are gated by these `versionDefines` symbols, declared in the `.asmdef` of each assembly that uses the package:

| Package | Define symbol |
|---|---|
| `com.cysharp.unitask` | `MU3LIBRARY_UNITASK_SUPPORT` |
| `com.unity.addressables` | `MU3LIBRARY_ADDRESSABLES_SUPPORT` |
| `com.unity.localization` | `MU3LIBRARY_LOCALIZATION_SUPPORT` |
| `com.unity.inputsystem` | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` |
| `com.unity.mobile.notifications` | `MU3LIBRARY_NOTIFICATIONS_SUPPORT` |

- Gated code lives in a split file named after the integration (`*.UniTask.cs`, `*.Addressables.cs`, `*.Addressables.UniTask.cs`, `*.InputSystem.cs`) or in that integration's folder (`Addressable/`, `Localization/`, `IS/`, `Notifications/`).
- `Notifications/` is its own assembly, `Mu3Library.Notifications`, which lists `MU3LIBRARY_NOTIFICATIONS_SUPPORT` in `defineConstraints`: without the package Unity skips the whole assembly, so its files need no `#if` for that symbol.

## DI, CoreBase, And MVP

- Keep the DI and MVP modules decoupled; use the existing DI rather than ad-hoc globals or new singletons.
- Use `MonoBehaviour` only for scene components; keep service and domain logic in plain C# classes.
- `Singleton<T>` and `GenericSingleton<T>` stay public for consuming projects even though the packages drive their services through `CoreBase`; do not remove them as unused.

## C# Conventions

- A namespace follows its folder under the `Mu3Library` root: `Foundation/Coroutine` is `Mu3Library.Foundation.Coroutine`, `Runtime/Scripts/Utility` is `Mu3Library.Utility`. The existing exceptions stay, since renaming a public namespace breaks consumers, and no new one is added:
  - a leaf named like a Unity type its own code uses: `URP/Runtime/Scripts/Camera` is `Mu3Library.URP.Cam`;
  - a folder that only groups the parts of one feature keeps its parent's namespace: Base's `UI/Area/Boundary` is `Mu3Library.UI.Area` in the runtime and `Mu3Library.Editor.UI.Area` in the editor, and Watermelon Game's `Board/Area` and `Board/Controller`, which hold the parts of `BoardArea` and `BoardController`, are `Mu3Library.Game.WatermelonGame.Board`.
- Explicit access modifiers on every member; interface names start with `I`.
- Log actionable problems with `Debug.LogWarning` or `Debug.LogError`; keep hot paths quiet. Catch exceptions at I/O, parsing, and network boundaries.
- Async code follows the coroutine or UniTask pattern already used nearby; reuse the existing pooling and utilities.

## Review Focus

In order of severity:

1. Behavioral regressions.
2. Public API compatibility: interfaces, classes, methods, and properties.
3. Assembly boundary safety: minimal and correct `.asmdef` references, dependency direction.
4. Optional gate correctness: every optional-package use inside its define symbol.
5. Alignment: `.meta`-safe asset operations, README and CHANGELOG with their localized copies, and verification evidence.
