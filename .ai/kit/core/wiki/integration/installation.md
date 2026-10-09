---
owns: "Installing, adopting, updating, and removing the kit in a project, and the resulting layout"
volatility: evolving
reviewed: 2026-10-09
---

# Installation

## When

- adding the kit to a project, or a project already has hand-written agent instruction files,
- updating or removing the kit.

## Route Away When

- the guided procedure: the `kit-install` and `kit-update` skills,
- configuring the overlay after installing: [project-overlay.md](project-overlay.md).

## Requirements

- Python 3.11 or newer on the machine that runs `agentkit.py` (standard library only).
- Git, and the `gh` CLI when `hosting.platform` is `github`.

## Install

From a checkout of the kit repository:

```bash
python tools/agentkit.py install <project-dir> --tools claude,codex,copilot
```

This copies the kit payload (`core/`, `tools/agentkit.py`, `VERSION`, `CHANGELOG.md`, `LICENSE`) into `<project>/.ai/kit/` with an integrity manifest, creates `.ai/project/` from templates, adds `.ai/tasks/` and the worktree root (`policy.worktree_root`) to `.gitignore`, and runs `sync`. Commit `.ai/kit/`, `.ai/project/`, `.ai/generated/`, and the generated entry and tool files, so every collaborator and hosted agent sees the same setup.

On a hosted repository, apply the settings in «Repository Settings» in [issues-and-prs.md](../workflows/issues-and-prs.md) after the user confirms them. Add `python .ai/kit/tools/agentkit.py check` to the project's CI so a hand-edited generated file, a local kit edit, or a broken link fails the build.

The kit is copied, not linked as a Git submodule: tools load instruction files found in subdirectories they read, so a nested kit repository's own entry files would leak into the project's sessions.

## Resulting Layout

```text
<project>/
  AGENTS.md, CLAUDE.md, …      generated entry files
  .claude/ .codex/ .agents/ .github/…   generated tool files (see tool-adapters.md)
  .ai/kit/                     the kit, read-only
  .ai/project/                 the overlay, owned by the project
  .ai/generated/               catalog and manifest, generated
  .ai/tasks/                   plans, not committed
  .worktrees/                  one worktree per unit, not committed
```

## Adopt Existing Instructions

When `sync` refuses to overwrite a hand-written `AGENTS.md`, `CLAUDE.md`, or tool file:

1. Sort its content: rules the kit already owns (drop them), project facts and parameters (move to `.ai/project/profile.toml` or `.ai/project/wiki/`), and rules that would help every project (propose upstream).
2. Show the user what will be dropped and get confirmation before deleting the file.
3. Run `sync` again.

## Update

From the project root:

```bash
python .ai/kit/tools/agentkit.py update [--from <kit-repo-url-or-path>] [--ref <tag-or-branch>]
```

- Without `--from`, the source is `evolution.upstream` from the profile.
- From a git URL without `--ref`, the update installs the latest release tag (`vX.Y.Z`), never an unreleased branch; it stops when the source has no release tag. Pass `--ref` to choose a tag or branch explicitly.
- From a local checkout, the update copies that checkout as it is; check out the wanted version there first (`--ref` is refused).

The update refuses to run when kit files were edited locally, replaces `.ai/kit/`, prints the changelog since the installed version, and runs `sync`. Apply every migration step it prints. See [versioning.md](../evolution/versioning.md).

## Remove

Delete `.ai/kit/`, `.ai/generated/`, and every file listed in `.ai/generated/manifest.json` (confirm the list with the user first), then decide whether `.ai/project/` stays as plain project documentation.
