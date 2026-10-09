---
owns: "How the three Mu3Library packages are versioned and released: version files, tag formats, changelog headers, install URLs, and GitHub Release titles and notes"
volatility: evolving
reviewed: 2026-10-09
---

# Package Release

The kit's [release.md](../../kit/core/wiki/workflows/release.md) owns the release unit, promotion, and version rules, and the `release-cut` skill walks it. This page records the formats this repository uses.

## When

- a release, version bump, tag, or GitHub Release of a package is requested.

## Route Away When

- the changelog's localized copies: «Human-Facing Docs» in [overview.md](overview.md).

## Packages

Each package versions and tags independently. Bump only the packages in the release scope; several packages may release from one commit when each of them changed.

| Package | Version file | Tag | Root `CHANGELOG.md` header | Release title |
|---|---|---|---|---|
| Base | `Mu3Library_Base/package.json` | `base/vX.Y.Z` | `## [base/X.Y.Z] - YYYY-MM-DD` | `[Base] vX.Y.Z` |
| URP | `Mu3Library_URP/package.json` | `urp/vX.Y.Z` | `## [urp/X.Y.Z] - YYYY-MM-DD` | `[URP] vX.Y.Z` |
| Watermelon Game | `Mu3Library_Game_WatermelonGame/package.json` | `game/watermelon/vX.Y.Z` | `## [game/watermelon/X.Y.Z] - YYYY-MM-DD` | `[Watermelon Game] vX.Y.Z` |

- Plain `vX.Y.Z` tags (`v0.0.20` through `v0.6.0`) are historical.
- Each package folder also keeps a package-local `CHANGELOG.md` whose version entry points to the root entry; add it in the same commit.
- The tag path is `game/watermelon` while the commit scope is `watermelon`; keep each as written.

## Install URLs

```text
https://github.com/doqltl179/Mu3Library_ForUnity.git?path=<package folder>#<tag>
```

Update `README.md` and its localized copies when a new tag changes the install guidance.

## GitHub Release

A pushed tag is not a GitHub Release; create one per tag after pushing it, then check it with `gh release view <tag>`.

```sh
gh release create base/vX.Y.Z --title "[Base] vX.Y.Z" --notes-file <notes.md>
```

- Pass notes with `--notes-file`, never inline `--notes`: PowerShell corrupts Markdown code spans in inline arguments.
- `PREV_TAG` is the previous GitHub Release of the same package (`gh release list`), not merely the previous Git tag.

```md
## What's Changed
- ...

## Package
- Version: `x.y.z`
- Install: `https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/vX.Y.Z`

## Full Changelog
https://github.com/doqltl179/Mu3Library_ForUnity/compare/PREV_TAG...NEW_TAG
```
