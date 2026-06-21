# Base Editor

Use this page only when editing editor tooling under `Mu3Library_Base/Editor/Scripts`.

## Rules

- Keep editor tooling isolated from runtime code under `Mu3Library.Editor.asmdef`.
- Avoid moving shared logic here unless the task is explicitly editor-scoped.
- When a runtime type must be referenced, preserve the existing runtime-to-editor direction and do not leak `UnityEditor` APIs back into runtime assemblies.
- Prefer matching nearby drawer, window, and utility patterns before adding new editor structure.

## Route Away When

- the work actually belongs to runtime code or importable samples: go back to [base-package-routing.md](base-package-routing.md).
- the work is no longer inside the base package: go back to [README.md](README.md).
