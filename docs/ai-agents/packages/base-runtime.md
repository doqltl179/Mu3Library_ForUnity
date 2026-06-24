# Base Runtime

Use this page only when editing reusable non-editor C# under `Mu3Library_Base/Runtime/Scripts`.

## Rules

- Keep runtime changes inside `Mu3Library_Base/Runtime/Scripts` unless a cross-boundary task explicitly requires `Editor` or `Samples~`.
- Preserve `Mu3Library.asmdef` boundaries and existing define-gated split files such as `*.UniTask.cs`, `*.Addressables.cs`, and `*.Editor.cs`.
- Prefer interface-first DI patterns and keep `CoreBase` initialization and injection order stable.
- Do not introduce `UnityEditor` dependencies here.

## Route Away When

- the work actually belongs to editor tooling or importable samples: go back to [base-package-routing.md](base-package-routing.md).
- the work is no longer inside the base package: go back to [README.md](README.md).
