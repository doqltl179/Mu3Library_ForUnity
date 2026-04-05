---
description: "Documentation synchronization policy for multilingual Mu3Library docs"
---

# Docs Sync Instructions

## Scope

- `README.md`
- `docs/readme/README.ko.md`
- `docs/readme/README.ja.md`
- `CHANGELOG.md`
- `docs/changelog/CHANGELOG.ko.md`
- `docs/changelog/CHANGELOG.ja.md`

## Rules

1. If behavior/public API changes, update the primary English doc in the same task.
2. When `README.md` is updated, `docs/readme/README.ko.md` and `docs/readme/README.ja.md` must be updated in the same task.
3. When `CHANGELOG.md` is updated, `docs/changelog/CHANGELOG.ko.md` and `docs/changelog/CHANGELOG.ja.md` must be updated in the same task.
4. When `CHANGELOG.md` is updated, evaluate whether `README.md` also needs updating (e.g., new features, changed API signatures, new define symbols, updated usage examples). If so, update `README.md` and its localized files in the same task.
5. Do not defer README/CHANGELOG localization updates unless explicitly requested by the user.
6. Keep section structure and visual style synchronized across languages, not only the text content.
7. Avoid documenting unverified behavior.

## Update Quality

- Keep examples consistent with real code signatures.
- Keep feature lists and define-symbol sections synchronized across languages.
- Prefer concise and explicit wording over marketing language.
