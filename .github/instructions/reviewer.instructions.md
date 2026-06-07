---
applyTo: '**'
---

# Review Rules

Use this checklist when changes are ready for review, and always for public API, `.asmdef`, define-gate, package metadata, docs-sync, or release work.

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

## Output Rule

- Findings first, ordered by severity, with file path and concrete impact.
- Then open questions or assumptions.
- End with a short change summary.
- If no issues are found, say so and state any verification gaps.
