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
- Update localized files when feasible:
  - `CHANGELOG.ko.md`
  - `CHANGELOG.ja.md`

## Release Checklist

1. Version updated intentionally.
2. Changelog entries included.
3. Public API compatibility checked.
4. Optional integration gates validated.
5. Basic compile/verification completed or gaps reported.
