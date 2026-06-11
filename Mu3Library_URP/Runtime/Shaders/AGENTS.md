# URP Shaders Rules

- This folder owns URP shader assets only.
- Prefer minimal shader edits over full rewrites, and check nearby shaders for property naming, pass layout, and include patterns before changing structure.
- Preserve URP compatibility and call out variant growth or renderer-feature implications when they are likely.
- Keep C# coordination changes in `../Scripts`; do not turn shader work into a cross-folder rewrite unless the task explicitly needs it.
- Shared wiki routes: [packages](../../../docs/ai-agents/packages/README.md), [plans](../../../docs/ai-agents/plans/README.md), [unity-yaml-guide](../../../docs/ai-agents/guides/unity-yaml-guide.md).
