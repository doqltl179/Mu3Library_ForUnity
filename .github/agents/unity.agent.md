---
description: "Unity architecture and coding standards for Mu3Library"
name: "Unity Development Standards"
---

# Mu3Library Unity Agent

## Project Identity

Mu3Library is a reusable Unity package intended for integration into external projects.
Prioritize package quality, consistency, and stable public APIs.

## Scope and Paths

- Base package root: `Mu3Library_Base`
- Runtime: `Mu3Library_Base/Runtime/Scripts`
- Editor: `Mu3Library_Base/Editor/Scripts`
- Samples: `Mu3Library_Base/Samples~`
- URP package root: `Mu3Library_URP`
- BuiltIn dev project: `UnityProject_BuiltIn`
- URP dev project: `UnityProject_URP`

## Architecture Rules

1. Keep module boundaries clear:
   - DI and MVP modules should remain decoupled.
2. Preserve DI lifecycle behavior:
   - `CoreBase` initialization and injection order must remain stable.
3. Keep optional features gated by define symbols:
   - `MU3LIBRARY_UNITASK_SUPPORT`
   - `MU3LIBRARY_ADDRESSABLES_SUPPORT`
   - `MU3LIBRARY_LOCALIZATION_SUPPORT`
   - `MU3LIBRARY_INPUTSYSTEM_SUPPORT`
4. Avoid adding new hard dependencies across assemblies.

## Assembly and Dependency Rules

- Respect `.asmdef` boundaries.
- Prefer interface-first registration and resolution for DI services.
- Do not add references between runtime and editor assemblies unless required.
- Keep optional package integrations isolated in split files such as:
  - `*.UniTask.cs`
  - `*.Addressables.cs`
  - `*.Editor.cs`

## C# Conventions

- Namespace base: `Mu3Library`.
- Use explicit access modifiers.
- Private serialized fields should use `[SerializeField]` with private scope.
- Interface names use `I` prefix.
- Keep naming and layout consistent with nearby files.

## Unity Asset Rules

- Preserve `.meta` files.
- Do not edit generated directories (`Library`, `Temp`, `Logs`, `obj`, `UserSettings`).
- Keep sample content functional when changes affect public package behavior.

## API and Behavior Change Rules

- Avoid breaking public APIs unless requested.
- For behavior changes, update relevant docs and changelogs in the same task when feasible.

## Verification Expectations

- Validate compile impact on touched assemblies.
- Run available checks relevant to changed modules.
- If verification cannot be run, state the gap explicitly.
