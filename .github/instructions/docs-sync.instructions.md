---
description: "Documentation synchronization policy for multilingual Mu3Library docs"
---

# Docs Sync Instructions

## Scope

- `README.md`
- `README.ko.md`
- `README.ja.md`
- `CHANGELOG.md`
- `CHANGELOG.ko.md`
- `CHANGELOG.ja.md`

## Rules

1. If behavior/public API changes, update at least the primary English doc in the same task.
2. Keep Korean/Japanese docs aligned when feasible.
3. If localization is deferred, state exactly which files still need updates.
4. Avoid documenting unverified behavior.

## Update Quality

- Keep examples consistent with real code signatures.
- Keep feature lists and define-symbol sections synchronized across languages.
- Prefer concise and explicit wording over marketing language.
