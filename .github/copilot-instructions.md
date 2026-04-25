# Mu3Library For Unity

This file is the central entry point for instruction routing.

## Core Guardrails

- This file is the single source of truth for repository instructions.
- Prefer edits in `Mu3Library_Base` unless project-level changes are requested.
- Preserve Unity package stability (`.asmdef`, `.meta`, define symbols, public APIs).
- Sync docs/changelog when behavior or public API changes.
- Keep Markdown docs (`README*`, `CHANGELOG*`, and instruction files) encoded as UTF-8 BOM with LF line endings to prevent multilingual text corruption.

## Read Order

Always-loaded (auto-injected via `applyTo: '**'`):
1. `.github/instructions/agent-framework.instructions.md`
2. `.github/instructions/memory-policy.instructions.md`
3. `.github/instructions/task-planner.instructions.md`
4. `.github/instructions/unity-architecture.instructions.md`
5. `.github/instructions/reviewer.instructions.md`
6. `.github/instructions/unity.instructions.md` (C# files only, `applyTo: '**/*.cs'`)

Conditionally loaded:
7. `.github/instructions/verification.instructions.md` (when compile/safety verification is needed)
8. `.github/instructions/git-workflow.instructions.md` (when branch/merge/push/release flow is requested)
9. `.github/instructions/release.instructions.md` (when release/version/changelog is affected)
10. `.github/instructions/docs-sync.instructions.md` (when README/CHANGELOG updates are needed)

Subagent-only (invoked via `runSubagent`):
- `.github/agents/task-planner.agent.md`
- `.github/agents/unity.agent.md`
- `.github/agents/unity-runtime.agent.md`
- `.github/agents/unity-editor.agent.md`
- `.github/agents/package-integration.agent.md`
- `.github/agents/docs-sync.agent.md`
- `.github/agents/release-manager.agent.md`
- `.github/agents/sample-integrity.agent.md`
- `.github/agents/reviewer.agent.md`
- `.github/agents/orchestrator.agent.md`
- `.github/agents/cli-platform.agent.md`
- `.github/agents/role-governor.agent.md`

Workspace skills:
- `.github/skills/bootstrap-python-cli/SKILL.md`
- `.github/skills/agent-role-audit/SKILL.md`
- `.github/skills/unity-compile-gate/SKILL.md`

## Mu3Library Guardrails

1. Package-first edits:
   - Prefer changes under `Mu3Library_Base` or `Mu3Library_URP`.
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
- For agent-framework changes, work in one bounded unit at a time and route the structural suitability review through the role-governor before continuing.
- For memory-routing or handoff changes, keep `.github/instructions/memory-policy.instructions.md` as the operative rule and use `docs/ai-agents/handoff-contract.md` as the human-facing contract reference.
- For direct Unity scene or prefab YAML edits, consult `docs/ai-agents/unity-yaml-guide.md` first, prefer cloning verified local subtrees over inventing raw YAML by hand, and update that guide when a new reliable pattern or failure mode is confirmed.
- For multilingual `README` or `CHANGELOG` work, prefer `docs-sync` for synchronization and keep release execution separate.
- For release, versioning, tagging, branch sync, or GitHub Release execution, prefer `release-manager` and keep `docs-sync` plus `reviewer` as adjacent gates.
- For Unity-domain routing, prefer the narrowest owner first: `unity-runtime` for non-gated runtime work, `unity-editor` for non-gated editor work, `package-integration` for define-gated optional packages, `sample-integrity` for sample-only package work, sample manifests, imported sample footprints, and sample smoke checks, and `unity` only for truly cross-boundary Unity tasks.
- For compile-only verification, prefer editor-safe `dotnet build` of the affected generated Unity `.csproj` files when the target Unity editor is open. Use `unity-compile-gate` only when the editor is closed and batch Unity compile evidence is specifically needed; if a batch compile is started, wait until completion is recorded before continuing.
- Keep implementation incremental and verifiable.
- If a required tool is unavailable, continue with an equivalent fallback and report it briefly.
