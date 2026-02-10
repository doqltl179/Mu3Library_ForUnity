# Mu3Library For Unity

This file is the central entry point for instruction routing.

## Core Guardrails

- This file is the single source of truth for repository instructions.
- Prefer edits in `Assets/Mu3LibraryAssets` unless project-level changes are requested.
- Preserve Unity package stability (`.asmdef`, `.meta`, define symbols, public APIs).
- Sync docs/changelog when behavior or public API changes.
- Keep Markdown docs (`README*`, `CHANGELOG*`, and instruction files) encoded as UTF-8 BOM with LF line endings to prevent multilingual text corruption.

## Read Order

1. `.github/agents/task-planner.agent.md`
2. `.github/agents/unity.agent.md`
3. `.github/instructions/unity.instructions.md`
4. `.github/instructions/verification.instructions.md` (when compile/safety verification is needed)
5. `.github/instructions/release.instructions.md` (when release/version/changelog is affected)
6. `.github/instructions/docs-sync.instructions.md` (when README/CHANGELOG updates are needed)
7. `.github/agents/reviewer.agent.md` (when review/audit style output is requested)

## Mu3Library Guardrails

1. Package-first edits:
   - Prefer changes under `Assets/Mu3LibraryAssets`.
   - Avoid unrelated project-level edits unless requested.
2. Public API stability:
   - Do not break existing public APIs without explicit request.
   - Prefer additive, backward-compatible changes.
3. Assembly boundaries:
   - Preserve `.asmdef` boundaries and keep dependencies minimal.
4. Optional dependency gates:
   - Keep integrations behind existing define symbols:
     - `MU3LIBRARY_UNITASK_SUPPORT`
     - `MU3LIBRARY_ADDRESSABLES_SUPPORT`
     - `MU3LIBRARY_LOCALIZATION_SUPPORT`
     - `MU3LIBRARY_INPUTSYSTEM_SUPPORT`
5. Unity asset integrity:
   - Preserve `.meta` files for asset/script add, move, and rename operations.
6. Documentation sync:
   - If behavior or public API changes, update related README and changelog files.

## Workflow Summary

- For multi-step work, provide a short plan, progress updates, and a final verification summary.
- Keep implementation incremental and verifiable.
- If a required tool is unavailable, continue with an equivalent fallback and report it briefly.
