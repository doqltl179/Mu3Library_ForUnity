# Mu3Library For Unity

This file is the central entry point for instruction routing.

<!-- SHARED-ENTRY-BLOCK:START -->
## Shared Entry Block (Keep In Sync)

- `AGENTS.md` is the primary entry for Codex workflows.
- `.github/copilot-instructions.md` is the primary entry for Copilot workflows.
- Both files must be independently usable with equivalent shared guardrails.
- If shared policy changes in one file, update the other file in the same change.
- Prefer edits in `Assets/Mu3LibraryAssets` unless project-level changes are requested.
- Preserve Unity package stability (`.asmdef`, `.meta`, define symbols, public APIs).
- Sync docs/changelog when behavior or public API changes.
<!-- SHARED-ENTRY-BLOCK:END -->

## Read Order

1. `.github/agents/task-planner.agent.md`
2. `.github/agents/unity.agent.md`
3. `.github/instructions/unity.instructions.md`
4. `.github/instructions/testing.instructions.md` (when verification is needed)
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
