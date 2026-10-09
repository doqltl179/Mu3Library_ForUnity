---
name: github-issue-create
description: "Open a GitHub issue with the gh CLI: search for duplicates first, write the body from the required fields, apply existing labels, and mark it blocked when it depends on unfinished work. Use when a defect, request, or leftover task needs a tracked record on GitHub."
---
<!-- agentkit:generated from .ai/kit/core/skills/github-issue-create/SKILL.md. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# Create Issue

## Use When
- A defect, feature request, or task needs a tracked record.
- Out-of-scope work found during a task must become an issue per «Leftovers Become Issues» in [issues-and-prs.md](.ai/kit/core/wiki/workflows/issues-and-prs.md).

## Do Not Use When
- `hosting.platform` is not `"github"` → list the item in the task report instead.
- Reviewing, relabeling, or closing existing issues → `github-issue-triage`.
- The request needs a full requirements specification → `requirements-spec-write`, then link the spec from the issue.

## Inputs
- What is wrong or wanted, and the evidence: reproduction steps, command output, paths, links.
- Related issues and pull requests, and the blocking issue if any.

## Steps
1. Run «Platform Preflight» in [issues-and-prs.md](.ai/kit/core/wiki/workflows/issues-and-prs.md).
2. Search for duplicates with at least two distinct keyword sets: `gh issue list --state all --search "<keywords>" --limit 30`. Read each candidate: `gh issue view <number>`.
3. If an open issue already covers it, add only the new facts as a comment (`gh issue comment <number> --body-file <file>`), report that number, and stop. If a closed issue covers it, link it in the new body.
4. Compose the title and body per «Issue Body» in issues-and-prs.md, in `project.language`. Redact secrets from quoted output per «Secrets And Sensitive Data» in [integrity.md](.ai/kit/core/wiki/principles/integrity.md). Write the body to a temporary file.
5. Choose labels per «Labels» from those that exist: `gh label list --limit 100`.
6. If the work cannot start until other work lands, mark it per «Blocked Issues».
7. Create the issue: `gh issue create --title "<title>" --body-file <file>` plus one `--label <name>` per label.
8. Confirm it: `gh issue view <number> --json number,url,labels`.

## Output
- The issue number and URL, its labels, and its blocked state; or the existing issue that was commented on instead.
- The duplicates found and how each was handled.
