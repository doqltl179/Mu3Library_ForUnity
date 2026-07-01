# URP Package

Use this page when work is clearly inside `Mu3Library_URP`.

## Surface Rules

| Surface | Applies When | Rules |
|---|---|---|
| Runtime | URP runtime C# under `Mu3Library_URP/Runtime/Scripts` | Keep changes URP-specific; avoid pulling base runtime concerns here unless integration work needs both packages; preserve renderer-feature and screen-effect patterns; keep optional integrations split into narrow files. |
| Shaders | Shader assets under `Mu3Library_URP/Runtime/Shaders` | Prefer minimal shader edits; check nearby property names, pass layout, and include patterns; call out variant growth or renderer-feature implications when likely. |
| Samples | Importable sample scenes or demo assets under `Mu3Library_URP/Samples~` | Preserve sample import integrity, `.meta` files, and URP scene/material wiring; keep runtime or shader defects owned by the relevant package surface. |

## Route Away When

- the work is no longer inside the URP package: return to [README.md](README.md).
- package-family selection is unclear: return to [../README.md](../README.md).
