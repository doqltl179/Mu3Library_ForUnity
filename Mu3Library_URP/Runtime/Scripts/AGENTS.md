# URP Runtime Scripts Rules

- This folder owns URP runtime C# code under `Mu3Library.URP.asmdef`.
- Keep changes URP-specific and avoid pulling base-package runtime concerns here unless integration work explicitly requires both packages.
- Preserve URP renderer-feature and screen-effect patterns already used nearby.
- Do not introduce `UnityEditor` APIs here, and keep any optional integration split into narrowly scoped files when needed.
- Shader authoring rules live closer to the shader files in `../Shaders/AGENTS.md`.
- Shared wiki routes: [packages](../../../docs/ai-agents/packages/README.md), [plans](../../../docs/ai-agents/plans/README.md).
