# Editor Scripts Rules

- This folder owns base-package editor-only code under `Mu3Library.Editor.asmdef`.
- Keep editor tooling isolated from runtime code and avoid moving shared logic here unless the task is explicitly editor-scoped.
- When a runtime type must be referenced, preserve the existing runtime-to-editor direction and do not leak `UnityEditor` APIs back into runtime assemblies.
- Prefer matching nearby drawer, window, and utility patterns before adding new editor structure.
- Shared wiki routes: [packages](../../../docs/ai-agents/packages/README.md), [plans](../../../docs/ai-agents/plans/README.md).
