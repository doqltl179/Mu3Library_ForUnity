---
name: code-migration
description: "Move code from one API, framework, library, or pattern to another across many files: map old usage to new, inventory every call site, choose codemod or manual and incremental or big-bang, migrate in verified batches, and remove the old path last. Use when a replacement touches many call sites or a deprecated API must be phased out."
---
<!-- agentkit:generated from .ai/kit/core/skills/code-migration/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Migrate Code

## Use When
- Call sites across many files must move from one API, framework, library, or pattern to another.
- A deprecated internal or external API must be phased out.

## Do Not Use When
- Only the version of the same dependency changes → `dependency-upgrade`; come here when its migration guide requires broad call-site rewrites.
- The change is to a database schema or stored data → `schema-migration`.
- Restructuring without changing which API is used → `refactor-safely`.

## Inputs
- The old and new API, framework, or pattern, and the reason for the move.
- The target's official documentation or migration guide.
- `commands.build`, `commands.typecheck`, `commands.test`, and `commands.lint`.

## Steps
1. Write a mapping from each old usage form to its new form, from the target's official documentation. Mark forms with no direct equivalent. A target library the project does not use yet needs approval per «Dependencies» in [code-changes.md](.ai/kit/core/wiki/workflows/code-changes.md) before step 5.
2. Inventory call sites with `rg` on imports, symbols, configuration keys, and string references, including tests, build scripts, and docs. Record each site's path and usage form, and the total count. Sites in generated output change through their source per «Generated Code» in code-changes.md.
3. Apply «When To Plan» in [planning.md](.ai/kit/core/wiki/workflows/planning.md); when a plan is required, record the mapping, inventory, and strategy in it with `task-plan-write`.
4. Choose the strategy and state the reason. When the old path is a public interface, «Compatibility» in code-changes.md constrains the choice:

   | Situation | Choice |
   |---|---|
   | Many sites share a few mechanical forms | Codemod, then manual fixes for what it misses |
   | Each site needs judgment | Manual edits |
   | Old and new can coexist and the work spans several pull requests | Incremental, behind a compatibility layer |
   | Old and new cannot coexist, or all sites fit one reviewable pull request | Big-bang |

5. Run `commands.test` for a passing baseline; add tests for representative call sites with `test-add` where none exist.
6. Incremental only: add the compatibility layer first and mark the old path deprecated so new uses are visible. Commit it alone.
7. Migrate one batch (one module or directory) at a time: transform, read the full diff including codemod output, run the checks above, and commit with `git-commit`, naming the codemod command in the body. State the source of a third-party codemod before running it (see «Untrusted Content» in [integrity.md](.ai/kit/core/wiki/principles/integrity.md)). Update the inventory count after each batch.
8. When the inventory search finds zero old uses, remove the old path: the compatibility layer, the old dependency, and its configuration. Run the full checks again.
9. Update docs per «Doc Sync Rule» and «Changelog» in [documentation.md](.ai/kit/core/wiki/workflows/documentation.md). Record an architectural choice with `adr-write`.

## Output
- The mapping, the strategy and its reason, and the inventory count before and after (zero when complete).
- Commits per batch and verification evidence per «Evidence Format» in [verification.md](.ai/kit/core/wiki/workflows/verification.md).
- Deferred sites, each registered with `github-issue-create`.
