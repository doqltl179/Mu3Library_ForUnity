---
description: "Release and versioning rules for Mu3Library package changes"
---

# Release Instructions

## Versioning

- Change `Assets/Mu3LibraryAssets/package.json` version only when requested or when preparing a release.
- Use semantic versioning intent:
  - Patch: backward-compatible fixes.
  - Minor: backward-compatible features.
  - Major: breaking changes.

## Changelog

When user-facing behavior or API changes:
- Update `CHANGELOG.md`.
- Update localized files in the same task (unless the user explicitly requests otherwise):
  - `docs/changelog/CHANGELOG.ko.md`
  - `docs/changelog/CHANGELOG.ja.md`

## Release Checklist

1. Version updated intentionally.
2. Changelog entries included.
3. Public API compatibility checked.
4. Optional integration gates validated.
5. Basic compile/verification completed or gaps reported.

## GitHub Release Notes Format

When publishing or editing a GitHub Release, keep a consistent format:

```md
## What's Changed
- ...

## Package
- Version: `x.y.z`

## Full Changelog
https://github.com/doqltl179/Mu3Library_ForUnity/compare/vPREV...vNEW
```

- If details are still being refined, publish at least a concise non-empty summary to avoid omission.
