---
owns: "Kit versioning: version numbers, the kit changelog format, migration notes for projects, and how projects consume updates"
volatility: evolving
reviewed: 2026-10-09
---

# Versioning

How the kit itself is versioned and shipped to projects. Project releases follow [release.md](../workflows/release.md).

## When

- releasing a kit version,
- writing a changelog entry for a kit change,
- updating the kit inside a project.

## Route Away When

- deciding the impact class of a change: «Impact Classes» in [change-intake.md](change-intake.md),
- the update procedure in a project: the `kit-update` skill.

## Kit Versions

- `VERSION` at the kit root holds a Semantic Version. The impact class of the changes since the last release decides the bump.
- Every release is tagged `v<version>` in the kit repository.
- Generated files record the kit version in `.ai/generated/manifest.json`, so a project shows which version it renders.

## Changelog Format

`CHANGELOG.md` at the kit root follows Keep a Changelog:

```markdown
## [Unreleased]
### Added | Changed | Deprecated | Removed | Fixed | Reviewed
- <change> (<impact class>)
### Migration
1. <imperative step a project must perform>
```

- `Reviewed` lists files re-verified in freshness reviews.
- `Migration` is required for `minor` releases that need project action and for every `major` release.

## Migration Notes

- Written for an agent in an installed project to execute: imperative, one action per step, naming exact files, keys, and commands.
- Cover renamed or removed agents, skills, stacks, and profile keys, and any generated layout change.
- `agentkit.py update` prints every changelog section between the installed and the new version; the agent applies each migration step, then runs sync and check.

## Consumer Updates

- Projects update with the `kit-update` skill. By default it installs the latest release tag from `evolution.upstream`; unreleased integration-branch changes are installed only when a branch is named explicitly with `--ref`.
- Check for updates when `agentkit.py freshness --kit` reports overdue kit files, when a tool or model the project uses changes, or at least every 90 days.
- A project never patches `.ai/kit/` locally; it proposes the change upstream and updates when it ships.
