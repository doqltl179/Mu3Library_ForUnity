# Coding Rules

## When

- you are about to write or change code, docs, or package files,
- you need the rule against faked success or duplicated ownership,
- you need the file format required for repository prose and agent files.

This page owns the cross-cutting authoring rules. Surface-specific rules stay with their own owner and are linked, not copied.

## Route Away When

- Unity package architecture, `.asmdef`, define symbols, or C# conventions: [unity-architecture.instructions.md](../../.github/instructions/unity-architecture.instructions.md),
- which package family owns a surface: [packages/README.md](packages/README.md),
- README/CHANGELOG localization sync: [docs-sync.instructions.md](../../.github/instructions/docs-sync.instructions.md),
- direct scene or prefab YAML edits: [unity-yaml-guide.md](guides/unity-yaml-guide.md).

## Package-First Edits

- Land behavior in the packages — `Mu3Library_Base`, `Mu3Library_URP`, `Mu3Library_Game_WatermelonGame` — rather than in the development projects.
- Treat `UnityProject_BuiltIn`, `UnityProject_URP`, and `UnityProject_Game_WatermelonGame` as consumers used to exercise a package, not as the place a feature lives.
- Preserve package stability across every edit: public APIs, `.asmdef` boundaries, `.meta` files, define symbols, samples, and package metadata. The detailed rules are in [unity-architecture.instructions.md](../../.github/instructions/unity-architecture.instructions.md).

## No Workaround Rules

- Do not fake success. Never cover a contract error with a special case, a temporary fallback, or a hidden correction.
- Leave the unsupported or failing state visible, with a TODO, instead of making an unimplementable request appear complete.
- Do not invent an alternate procedure for a concern that already has one. Clarify or re-scope the request so the original intent can be met correctly.
- If verification cannot run, state the gap rather than implying it passed.

## SSOT Boundaries

- One rule has one owner. Every other page links to that owner instead of restating the rule.
- Do not copy a value, list, or procedure into a second page. That copy is the most common defect in this repository, because only one side gets fixed.
- Do not duplicate one concern across two owners, and do not implement a domain rule in a router or a router decision inside domain code.
- When implementation changes, update the affected docs in the same unit of work.
- The current user request outranks any record in `tasks/plans/`; those files are working records, not an SSOT.

## File Format

- Use LF line endings in every Markdown file.
- Byte-order mark follows the folder, and each folder stays uniform. Never mix the two inside one folder.

| BOM | Files |
|---|---|
| UTF-8 **with** BOM | `README*`, `CHANGELOG*`, `docs/ai-agents/**`, `.github/instructions/*.instructions.md` |
| UTF-8 **without** BOM | `.github/agents/*.agent.md`, `.github/prompts/*.prompt.md`, `.github/skills/*/SKILL.md` |

- Match the folder you are writing into rather than the last file you happened to open. A BOM sitting ahead of an opening `---` is read differently by different frontmatter parsers, so the artifact folders that several tools load stay BOM-free.
- Do not reflow or reorder a frontmatter block, and keep its `name` and `description` fields intact.
- Match the naming and layout of nearby files rather than introducing a new style in one file.

## Notes

- If a linked owner contradicts this page on its own surface, that owner wins and this page should be narrowed.
- Add a rule here only when it applies across surfaces; a rule that belongs to one surface belongs on that surface's page.
