---
applyTo: '**'
---

# Mu3Library Unity Architecture Rules

## Project Identity

Mu3Library is a reusable Unity package for external projects. Prioritize package quality, stable APIs, and clear assembly boundaries.

## Scope

- Base package: `Mu3Library_Base`
- URP package: `Mu3Library_URP`
- Runtime: `Runtime/Scripts`
- Editor: `Editor/Scripts`
- Samples: `Samples~`
- Dev projects: `UnityProject_BuiltIn`, `UnityProject_URP`

## Architecture Rules

- Follow the repository development philosophy in `.github/instructions/agent-framework.instructions.md`.
- Keep DI and MVP modules decoupled.
- Preserve `CoreBase` initialization and injection order.
- Keep optional integrations gated by:
  - `MU3LIBRARY_UNITASK_SUPPORT`
  - `MU3LIBRARY_ADDRESSABLES_SUPPORT`
  - `MU3LIBRARY_LOCALIZATION_SUPPORT`
  - `MU3LIBRARY_INPUTSYSTEM_SUPPORT`
- Avoid new hard dependencies across assemblies.
- Prefer interface-first DI registration and resolution.

## Unity Safety Rules

- Preserve `.asmdef` boundaries and `.meta` files.
- Do not edit generated directories: `Library`, `Temp`, `Logs`, `obj`, `UserSettings`.
- Keep runtime/editor ownership separate unless a cross-boundary task requires `unity`.
- Keep optional package integrations isolated in split files such as `*.UniTask.cs`, `*.Addressables.cs`, or `*.Editor.cs`.

## C# Conventions

- Namespace base: `Mu3Library`.
- Use explicit access modifiers.
- Private serialized fields use `[SerializeField] private`.
- Interface names use `I` prefix.
- Match naming and layout in nearby files.

## Verification

- Validate compile impact on touched assemblies when feasible.
- If verification cannot run, state the gap explicitly.
