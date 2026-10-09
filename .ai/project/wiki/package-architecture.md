---
owns: "Mu3Library package rules: surfaces per package, assembly boundaries and dependency direction, define-gated optional integrations, DI and CoreBase, project C# conventions, and the review focus for package changes"
volatility: evolving
reviewed: 2026-10-09
---

# Package Architecture

Rules for the package code itself. Unity and C# rules that hold in every project live in the `unity` and `csharp` stack packs; this page adds only what this repository decided.

## When

- changing package code, an `.asmdef`, a define symbol, a sample, or `package.json`,
- reviewing such a change.

## Route Away When

- which package or folder: [overview.md](overview.md),
- compile targets and EditMode tests: [unity-verification.md](unity-verification.md),
- direct scene or prefab YAML edits: [unity-yaml-guide.md](unity-yaml-guide.md).

## Package Surfaces

| Package | Surface | Rules |
|---|---|---|
| Base | Runtime `Runtime/Scripts` (`Mu3Library.asmdef`) | Interface-first DI; preserve the `CoreBase` initialization and injection order; no `UnityEditor` dependency |
| Base | Editor `Editor/Scripts` (`Mu3Library.Editor.asmdef`) | Editor depends on runtime, never the reverse; match nearby drawer, window, and utility patterns |
| URP | Runtime `Runtime/Scripts` (`Mu3Library.URP.asmdef`) | URP-specific only; follow the renderer-feature and screen-effect patterns |
| URP | Shaders `Runtime/Shaders` | Minimal edits matching nearby property names, pass layout, and includes; state likely variant growth |
| Watermelon Game | Runtime `Runtime/Scripts` (`Mu3Library.Game.WatermelonGame.asmdef`) | Board behavior stays in the package; reuse Base services instead of parallel audio, DI, or UI infrastructure |
| All | `Samples~` | Sample-only orchestration and presentation; a defect in reusable behavior is fixed in the runtime surface |

## Assembly Boundaries

- Dependency direction: URP → Base, Watermelon Game → Base and URP. Never reverse it and add no other hard dependency between assemblies.
- Keep `.asmdef` references minimal; diagnose assembly problems with the `asmdef-triage` skill before editing one.
- Generated-script helpers live in `Base/Editor/Scripts/FileUtil`, not in the drawer that needs them: identifier rules in `ScriptIdentifier`, script output in `FileCreator`, asset path checks in `FileFinder`.

## Optional Integrations

| Package | Define symbol |
|---|---|
| `com.cysharp.unitask` | `MU3LIBRARY_UNITASK_SUPPORT` |
| `com.unity.addressables` | `MU3LIBRARY_ADDRESSABLES_SUPPORT` |
| `com.unity.localization` | `MU3LIBRARY_LOCALIZATION_SUPPORT` |
| `com.unity.inputsystem` | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` |

- The symbols come from `versionDefines` in the package `.asmdef`, so they are set only when the consuming project has the package.
- Code that touches an optional package sits behind its symbol, in a split file named after it (`*.UniTask.cs`, `*.Addressables.cs`, `*.Addressables.UniTask.cs`) or in that integration's folder (`Addressable/`, `Localization/`, `IS/`).
- Never add an optional package as an unconditional reference.

## DI, CoreBase, And MVP

- Keep the DI and MVP modules decoupled; use the existing DI rather than ad-hoc globals or new singletons.
- Use `MonoBehaviour` only for scene components; keep service and domain logic in plain C# classes.
- `Singleton<T>` and `GenericSingleton<T>` stay public for consuming projects even though the packages drive their services through `CoreBase`; do not remove them as unused.

## C# Conventions

- A namespace follows its folder under the `Mu3Library` root: `Foundation/Coroutine` is `Mu3Library.Foundation.Coroutine`, `Runtime/Scripts/Utility` is `Mu3Library.Utility`. The one exception is a leaf named like a Unity type its own code uses: `URP/Runtime/Scripts/Camera` is `Mu3Library.URP.Cam`.
- Explicit access modifiers on every member; interface names start with `I`.
- Log actionable problems with `Debug.LogWarning` or `Debug.LogError`; keep hot paths quiet. Catch exceptions at I/O, parsing, and network boundaries.
- Async code follows the coroutine or UniTask pattern already used nearby; reuse the existing pooling and utilities.
- No breaking public API change unless the request asks for it.

## Review Focus

Review package changes in this order of severity:

1. Behavioral regressions.
2. Public API compatibility: interfaces, classes, methods, and properties.
3. Assembly boundary safety: minimal and correct `.asmdef` references, dependency direction.
4. Optional gate correctness: every optional-package use inside its define symbol.
5. Alignment: `.meta`-safe asset operations, README and CHANGELOG with their localized copies, and verification evidence for the touched surface.
