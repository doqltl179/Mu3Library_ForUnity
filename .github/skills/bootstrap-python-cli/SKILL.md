---
name: bootstrap-python-cli
description: "Use when creating or refining auxiliary Python CLI tooling for this repository: virtual environment setup, dependency/bootstrap flow, command tree design, subcommand layout, help UX, and documentation for support scripts."
---

# Bootstrap Python CLI

## Purpose

Create or refine repository-local Python CLI tooling without leaking that tooling into Unity product code.

## Use This Skill When

- a CLI is needed for repo automation,
- a Python environment bootstrap flow must be documented,
- a set of scripts should become one coherent command surface,
- command groups and subcommands need to be designed before implementation.

## Workflow

1. Define the CLI boundary and confirm it is tooling-only.
2. Choose the environment bootstrap approach.
3. Design the command tree and option naming.
4. Confirm safe roots and forbidden roots before editing files.
5. Define file layout and packaging metadata.
6. Add or update usage documentation.
7. Verify that the command surface is coherent and small enough to grow safely.

## Safe Boundary Rule

Unless the user explicitly asks for deeper integration, keep CLI-owned edits in tooling-oriented roots and treat Unity runtime/editor, sample assets, `.asmdef`, and package manifests as escalation surfaces.

## Output Expectations

- environment bootstrap steps,
- command tree proposal,
- tooling file layout,
- dependency choice rationale,
- verification and documentation checklist.