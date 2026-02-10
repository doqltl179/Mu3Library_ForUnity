---
applyTo: '**/*.cs'
---

Use these baseline coding instructions for C# files in this repository.

## General

- Prefer clear and maintainable code over clever or dense code.
- Keep changes minimal and scoped to the user request.
- Match existing naming and structure in the target folder.

## Unity-Specific

- Use `MonoBehaviour` only for scene components.
- Keep service/domain logic in plain C# classes where possible.
- Prefer DI patterns already used in Mu3Library over ad-hoc globals.
- Keep optional package usage behind existing define symbols.

## Error Handling and Logs

- Use `Debug.LogWarning` or `Debug.LogError` for actionable issues.
- Avoid noisy logs in hot paths.
- Add try/catch for boundaries such as I/O, parsing, and network calls.

## Performance

- Avoid per-frame allocations in update loops.
- Reuse existing pooling/utilities where relevant.
- Keep async usage consistent with existing coroutine/UniTask patterns.

## Safety

- Preserve `.asmdef` boundaries.
- Preserve `.meta` file integrity when touching assets.
- Do not introduce breaking public API changes unless requested.
