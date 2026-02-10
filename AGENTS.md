# AGENTS.md

This is the entry-point instruction file for AI agents in this repository.

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

1. `.github/copilot-instructions.md`
2. `.github/agents/task-planner.agent.md`
3. `.github/agents/unity.agent.md`
4. `.github/instructions/unity.instructions.md`
5. `.github/instructions/testing.instructions.md` (when verification is needed)
6. `.github/instructions/release.instructions.md` (when release/version/changelog is affected)
7. `.github/instructions/docs-sync.instructions.md` (when README/CHANGELOG updates are needed)
8. `.github/agents/reviewer.agent.md` (when review/audit style output is requested)

## Instruction Source

All detailed and maintainable agent guidance should be centralized in `.github/` for this repository.

## Local Notes

- Keep this root file concise.
- Put detailed operational guidance in `.github/` instruction files.

## Explicit Links

- Core routing: `.github/copilot-instructions.md`
- Planning and progress: `.github/agents/task-planner.agent.md`
- Unity architecture/coding rules: `.github/agents/unity.agent.md`
- C# baseline instructions: `.github/instructions/unity.instructions.md`
- Testing and verification policy: `.github/instructions/testing.instructions.md`
- Release/version policy: `.github/instructions/release.instructions.md`
- Multilingual docs sync policy: `.github/instructions/docs-sync.instructions.md`
- Review checklist and output style: `.github/agents/reviewer.agent.md`
