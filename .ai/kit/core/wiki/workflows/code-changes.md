---
owns: "Cross-cutting safety rules for changing code: adding dependencies, compatibility of public interfaces, schema and data migrations, and generated code"
volatility: evolving
reviewed: 2026-10-09
---

# Code Changes

Rules every execution owner follows regardless of surface. Surface-specific rules live in agent cards; language and framework rules live in stack packs.

## When

- adding or upgrading a dependency,
- changing a public interface, data format, or persisted schema,
- touching generated code.

## Route Away When

- verifying the change: [verification.md](verification.md),
- the step-by-step procedures: the `dependency-upgrade`, `schema-migration`, `code-migration`, and `refactor-safely` skills.

## Dependencies

- Prefer what the project already uses. Adding a new runtime dependency, test library, or build tool needs the user's approval: state the package, why existing ones do not suffice, its maintenance and license status.
- Install only with the package manager the lockfile belongs to, and commit the lockfile change with the manifest change.
- Pin or constrain versions the way the project already does.
- Read the official release notes and migration guide for every version boundary crossed before upgrading.
- Treat deprecation warnings an upgrade introduces as breakages: fix them in the same change or register each as an issue.

## Compatibility

- A change to a public API, CLI, configuration format, event, or file format is breaking unless proven otherwise. Breaking changes need the user's approval and a changelog entry that says how to migrate.
- Prefer additive change: new optional fields, new endpoints, deprecation before removal.
- When a shared contract changes, `software-architect` decides it and each side's owner implements it.

## Schema And Data Migrations

- Never edit a migration that has been merged or applied anywhere; write a new one.
- Every migration has a tested rollback, or the plan states why rollback is impossible and how recovery works.
- Breaking schema changes use expand and contract: add the new shape, migrate readers and writers, backfill in batches, then remove the old shape in a later release.
- Run data-changing migrations against a copy before any shared environment.

## Generated Code

- Never hand-edit generated output (compiled bundles, generated clients, lockfile internals, snapshots); change the source or generator and regenerate.
- Commit generated files only when the project already commits them.
