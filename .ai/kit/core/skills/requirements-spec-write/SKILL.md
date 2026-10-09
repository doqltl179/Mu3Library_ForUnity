---
name: requirements-spec-write
description: "Write a requirements specification in docs.specs_dir: problem, users, scope and out-of-scope, numbered functional requirements, testable acceptance criteria, non-functional requirements with thresholds, and open questions, asking the user only for decisions they own. Use when a feature or change needs agreed requirements before design or implementation, or an existing spec must change."
category: product
volatility: evolving
reviewed: 2026-10-09
---

# Write Requirements Spec

## Use When
- A feature or change is not specified well enough to decompose or implement.
- The user asks for a spec or requirements document.
- An existing spec must change because scope or a requirement changed.

## Do Not Use When
- Choosing between technical designs → `adr-write`.
- Splitting agreed requirements into units → `work-decompose`.
- Tracking execution steps → `task-plan-write`.

## Inputs
- The request, linked issues, and any prior specs, research, or support data.
- `docs.specs_dir`, `docs.locales`, and `project.language` from the profile.

## Steps
1. Read «Specifications» in [documentation.md](../../wiki/workflows/documentation.md) for file naming, statuses, and required content. Search `docs.specs_dir` for a spec on the same topic: `rg -il "<topic terms>" <docs.specs_dir>`. Update it instead of adding a neighbor.
2. Read the current behavior the request affects (code, docs, open issues), narrowly, so requirements state the change rather than the status quo.
3. Write Problem: who is affected, what fails today, the evidence, and why it matters now. State no solution here.
4. Write Users: primary and secondary users and the task each needs to accomplish.
5. Write Scope and Out of scope. Give every out-of-scope item a one-line reason.
6. Write Functional requirements, numbered `FR-1`, `FR-2`, …: one observable behavior each, phrased with "must", free of implementation choices.
7. Write Acceptance criteria for each requirement, each testable by a person or an automated test. Use Given/When/Then when the outcome depends on prior state. Replace words like "fast" or "intuitive" with a measurable threshold.
8. Write Non-functional requirements with thresholds for the categories that apply: performance, availability, security, privacy, accessibility, localization (`docs.locales`), compatibility, and observability.
9. Write Open questions: each names the decision, the options, a recommendation, and the decision owner. Ask the user only for decisions they own per «Stop, Pause, or Proceed» in [integrity.md](../../wiki/principles/integrity.md); settle conventional choices yourself and record them as assumptions.
10. Save it in `docs.specs_dir`, written in `project.language`, with the file name and `Status:` line per «Specifications» in documentation.md; it moves past draft only when the user approves scope and the open questions are resolved. Link it from the issue, and commit with `git-commit`.

## Output
- The spec path, its requirement IDs, open questions with their owners, and the decisions requested from the user.
