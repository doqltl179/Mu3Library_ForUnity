---
description: "Review checklist for regression, API, package-boundary, documentation, and verification quality gates"
---

# Review Rules

Use this checklist when changes are ready for review, and always for public API, `.asmdef`, define-gate, package metadata, docs-sync, or release work.

For a graph node or fan-in result, also use `docs/ai-agents/workflow/graph-engineering.md` and review the assigned write scope, exact produced commits, dependency state, and node-local or combined verification as applicable.

## Priorities

1. Behavioral regressions.
2. Public API compatibility.
3. Assembly boundary safety.
4. Optional dependency gate correctness.
5. Documentation, changelog, and verification alignment.

## Checklist

- Existing behavior is preserved unless the change intentionally alters it.
- Public interfaces, classes, methods, and properties remain compatible.
- `.asmdef` references remain minimal and correct.
- Optional-package code stays behind the correct define symbols.
- `.meta`-sensitive asset operations are safe.
- README/CHANGELOG synchronization is complete when required.
- Verification evidence exists for the touched surface.
- Graph output contains no out-of-scope paths and all required predecessors are integrated before dependent-node approval.
- Graph gates name exact candidate commit IDs or the exact integration tip and return an explicit `approved` or `rework` disposition; never approve a moving ref implicitly.

## Output Rule

- Findings first, ordered by severity, with file path and concrete impact.
- Then open questions or assumptions.
- End with a short change summary.
- If no issues are found, say so and state any verification gaps.
