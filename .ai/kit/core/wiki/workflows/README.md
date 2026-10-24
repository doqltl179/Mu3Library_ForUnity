---
owns: "Which repeatable workflow policy applies: planning, Git, issues and pull requests, code-change safety, verification, review, documentation, product ideas, and release"
volatility: stable
reviewed: 2026-10-09
---

# Workflows

Open this section when the question is what must hold during a recurring kind of work. Pages here own policy; the skills that walk them own the step order.

<!-- agentkit:begin index -->
| Page | Owns |
|---|---|
| [change-scope.md](change-scope.md) | How much a change may touch: assumptions to name before editing, the smallest change that meets the request, edit boundaries inside touched files, cleanup of what the change leaves unused, and findings outside the request |
| [code-changes.md](code-changes.md) | Cross-cutting safety rules for changing code: adding dependencies, compatibility of public interfaces, schema and data migrations, and generated code |
| [documentation.md](documentation.md) | Human-facing project documentation policy: doc sync with behavior, changelog entries, which localized variants exist and when they are updated, specifications, and decision records |
| [domain-terms.md](domain-terms.md) | How project concepts are named: one canonical term per concept, its definition, the synonyms to avoid, and using the terms in code, docs, and conversation |
| [git-workflow.md](git-workflow.md) | Local Git policy: branch roles, preflight, per-unit worktrees and task branches, commit convention, staging, integrating base changes, cleanup, and stop conditions |
| [idea-evaluation.md](idea-evaluation.md) | How a product idea is judged before adoption: the three idea scopes, the project values they read, verdicts, the eight criteria and their order, hand-overs to another scope or owner, choosing when the user delegates the choice, and what idea work must not touch |
| [idea-records.md](idea-records.md) | Where idea proposals, defer records, and their reference files are kept, their file names, the sections a proposal has in each scope, judging an earlier idea again, and the report after a verdict |
| [issues-and-prs.md](issues-and-prs.md) | Hosting-platform conventions: repository settings, issue bodies, labels, blocked issues, triage and closing, leftovers, pull request bodies, issue linking, mergeability, and review feedback |
| [planning.md](planning.md) | When work needs a written plan, where plans live, plan statuses, and how plans are closed |
| [product-evidence.md](product-evidence.md) | What counts as evidence for a product claim and how production data is read for analysis or evidence: evidence kinds and their fields, time windows, production data reads and privacy, read order by cost, blocked sign-in, and attributing an effect |
| [release.md](release.md) | Project release policy: what a release unit is, its scope (whole repository or separately versioned packages), promotion from the integration branch to the release branch, version numbers, changelog finalization, tags and notes, and publishing |
| [review.md](review.md) | Review policy: which review gates a change needs, the review protocol, severity levels, dispositions, and the review output format |
| [translation.md](translation.md) | Translation policy for any text (docs, UI strings, product and game content, messages): the source language, direct-from-source translation, content profiling, localization over literal translation, terminology consistency, protected content, and verification |
| [verification.md](verification.md) | What verification each kind of change needs, how evidence is reported, flaky tests and disabled checks, and what to do when verification cannot run |
<!-- agentkit:end index -->
