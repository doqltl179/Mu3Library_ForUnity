# Watermelon Game Package

Use this page when work is clearly inside `Mu3Library_Game_WatermelonGame`.

## Surface Rules

| Surface | Applies When | Rules |
|---|---|---|
| Runtime | Reusable board and game C# under `Mu3Library_Game_WatermelonGame/Runtime/Scripts` | Keep board behavior package-owned; preserve `Mu3Library.Game.WatermelonGame.asmdef`, public APIs, serialized fields, and the dependency direction from Watermelon Game to Base and URP; reuse Base services instead of introducing parallel audio, DI, or UI infrastructure. |
| Samples | Importable sample scenes or assets under `Mu3Library_Game_WatermelonGame/Samples~` | Keep sample-only orchestration and presentation here; preserve `.meta` files, scene references, package import integrity, and route reusable board defects to the runtime surface. |

## Route Away When

- the work is no longer inside the Watermelon Game package: return to [README.md](README.md).
- package-family selection is unclear: return to [../README.md](../README.md).
