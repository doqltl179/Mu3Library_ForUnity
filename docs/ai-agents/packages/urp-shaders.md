# URP Shaders

Use this page only when editing shader assets under `Mu3Library_URP/Runtime/Shaders`.

## Rules

- Prefer minimal shader edits over full rewrites, and check nearby shaders for property naming, pass layout, and include patterns before changing structure.
- Preserve URP compatibility and call out variant growth or renderer-feature implications when they are likely.
- Keep C# coordination changes in `../Scripts`; do not turn shader work into a cross-folder rewrite unless the task explicitly needs it.

## Route Away When

- the work actually belongs to runtime C# or importable samples: go back to [urp-package-routing.md](urp-package-routing.md).
- the work is no longer inside the URP package: go back to [README.md](README.md).
