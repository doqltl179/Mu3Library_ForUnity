# Runtime Scripts Rules

- This folder owns base-package runtime code only. Keep runtime changes inside `Mu3Library_Base/Runtime/Scripts` unless a cross-boundary task explicitly requires `Editor` or `Samples~`.
- Preserve `Mu3Library.asmdef` boundaries and existing define-gated split files such as `*.UniTask.cs`, `*.Addressables.cs`, and `*.Editor.cs`.
- Prefer interface-first DI patterns and keep `CoreBase` initialization and injection order stable.
- Do not introduce `UnityEditor` dependencies here.
- Shared wiki routes: [packages](../../../docs/ai-agents/packages/README.md), [plans](../../../docs/ai-agents/plans/README.md).
