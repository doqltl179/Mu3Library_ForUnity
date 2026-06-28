# Base Package

Use this page when work is clearly inside `Mu3Library_Base`.

## Surface Rules

| Surface | Applies When | Rules |
|---|---|---|
| Runtime | Reusable non-editor C# under `Mu3Library_Base/Runtime/Scripts` | Keep runtime code inside runtime roots unless the task explicitly needs `Editor` or `Samples~`; preserve `Mu3Library.asmdef` boundaries, define-gated split files, interface-first DI, and `CoreBase` initialization order; do not introduce `UnityEditor` dependencies. |
| Editor | Editor tooling under `Mu3Library_Base/Editor/Scripts` | Keep tooling isolated under `Mu3Library.Editor.asmdef`; preserve runtime-to-editor dependency direction; match nearby drawer, window, and utility patterns before adding structure. |
| Samples | Importable sample scenes or assets under `Mu3Library_Base/Samples~` | Preserve `.meta` files, scene references, sample structure, and importable package integrity; keep sample edits focused and route package code defects to the owning runtime or editor surface. |

## Route Away When

- the work is no longer inside the base package: return to [README.md](README.md).
- package-family selection is unclear: return to [../README.md](../README.md).
