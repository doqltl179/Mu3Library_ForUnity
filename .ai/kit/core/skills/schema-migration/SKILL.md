---
name: schema-migration
description: "Change a database schema or its stored data safely: write a forward migration with a rollback, split breaking changes into expand-and-contract phases, backfill in batches, verify forward and back on a disposable copy, and never edit an applied migration. Use when a change adds, alters, renames, or drops tables, columns, indexes, or constraints, or transforms existing rows."
category: code-change
volatility: evolving
reviewed: 2026-10-09
---

# Migrate Database Schema

## Use When
- A change adds, alters, renames, or drops tables, columns, indexes, or constraints.
- Existing rows must be backfilled or transformed.

## Do Not Use When
- Only application code moves between APIs → `code-migration`.
- Upgrading the database driver or ORM version → `dependency-upgrade`.

## Inputs
- The target schema or data end state and the reason.
- The project's migration tool, its migrations directory, and how migrations are applied in each environment.
- How deploys roll out (whether old and new application versions run at the same time), and the size of affected tables when known.

## Steps
1. Apply «Schema And Data Migrations» in [code-changes.md](../../wiki/workflows/code-changes.md) throughout. Find the migration tool from the existing migrations and their configuration. If the project has none, stop and ask; choosing one is a project decision.
2. List the applied and merged migrations; per «Schema And Data Migrations», changes to them go into a new migration.
3. Classify each change: additive (new table, nullable column, index), breaking (drop, rename, type change, new non-null constraint), or data (backfill, transform).
4. Split each breaking change into the expand-and-contract phases that «Schema And Data Migrations» requires, each its own migration and deploy:
   1. Expand: add the new structure; old code keeps working.
   2. Deploy code that writes both shapes and reads the new one with a fallback.
   3. Backfill existing rows.
   4. Deploy code that uses only the new shape.
   5. Contract: drop the old structure in a later release.
5. Write each migration with the tool's forward and rollback parts. For a step that cannot be reversed (for example, dropping data), record the recovery path «Schema And Data Migrations» asks for in the migration and the pull request, and confirm before it runs anywhere shared per «Confirm Before Irreversible Or Outward Actions» in [integrity.md](../../wiki/principles/integrity.md).
6. Write backfills to run in bounded batches, idempotent and resumable, outside the schema transaction. Avoid operations that hold long locks on large tables; use the engine's non-blocking variant where one exists.
7. Verify on a disposable copy with representative data: apply forward, roll back, apply forward again, then run `commands.test`. This task runs migrations only against that copy, never a shared or production database.
8. Update models, queries, fixtures, and tests for the phase being shipped, regenerating generated models per «Generated Code» in code-changes.md; run checks per «Verification By Change Type» in [verification.md](../../wiki/workflows/verification.md).
9. Commit each phase separately with `git-commit`. Register later phases, such as the contract step, with `github-issue-create`.
10. Record the decision with `adr-write` when the change alters the data model's design; update docs per «Doc Sync Rule» in [documentation.md](../../wiki/workflows/documentation.md).

## Output
- The migration files with forward and rollback parts, and the phase plan with its deploy order.
- Verification evidence from the copy per «Evidence Format».
- Irreversible steps flagged, and issues for later phases.
