# URP Runtime

Use this page only when editing URP runtime C# under `Mu3Library_URP/Runtime/Scripts`.

## Rules

- Keep changes URP-specific and avoid pulling base-package runtime concerns here unless integration work explicitly requires both packages.
- Preserve URP renderer-feature and screen-effect patterns already used nearby.
- Do not introduce `UnityEditor` APIs here, and keep any optional integration split into narrowly scoped files when needed.
- If the work is shader-only, route to [urp-shaders.md](urp-shaders.md).

## Route Away When

- the work actually belongs to shaders or importable samples: go back to [urp-package-routing.md](urp-package-routing.md).
- the work is no longer inside the URP package: go back to [README.md](README.md).
