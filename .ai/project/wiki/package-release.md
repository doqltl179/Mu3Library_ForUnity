---
owns: "How the Mu3Library packages are released beyond the profile's [[release.packages]]: the root changelog headers, when the ko and ja copies of README and CHANGELOG follow their sources, install URLs, the dependency versions checked at a package's first release, and GitHub Release titles and notes"
volatility: evolving
reviewed: 2026-11-19
---

# Package Release

The kit's [release.md](../../kit/core/wiki/workflows/release.md) owns the release unit, promotion, and version rules, and the `release-cut` skill walks it. Each package's version file, package-local changelog, and tag pattern are in `[[release.packages]]` in the profile. This page records the remaining formats.

## When

- a release, version bump, tag, or GitHub Release of a package is requested.

## Route Away When

- where the localized copies lie and the other doc layout rules: «Human-Facing Docs» in [overview.md](overview.md).

## Changelogs And Titles

The detailed notes go in the root `CHANGELOG.md` under a per-package header; its localized copies follow per «Localized Copies» below. The package-local `CHANGELOG.md` gets a short entry for the same version that points to the root entry, in the same commit. In every package its header is `## [X.Y.Z] - YYYY-MM-DD`, the version without the tag path, and its entry reads ``- See `<package>/X.Y.Z` in the repository root `CHANGELOG.md`.``, optionally followed by highlights, with `<package>` the tag path of the root header (`game/watermelon` for Watermelon Game).

| Package | Root `CHANGELOG.md` header | Release title |
|---|---|---|
| `base` | `## [base/X.Y.Z] - YYYY-MM-DD` | `[Base] vX.Y.Z` |
| `urp` | `## [urp/X.Y.Z] - YYYY-MM-DD` | `[URP] vX.Y.Z` |
| `watermelon` | `## [game/watermelon/X.Y.Z] - YYYY-MM-DD` | `[Watermelon Game] vX.Y.Z` |
| `notifications` | `## [notifications/X.Y.Z] - YYYY-MM-DD` | `[Notifications] vX.Y.Z` |
| `inputsystem` | `## [inputsystem/X.Y.Z] - YYYY-MM-DD` | `[Input System] vX.Y.Z` |
| `localization` | `## [localization/X.Y.Z] - YYYY-MM-DD` | `[Localization] vX.Y.Z` |
| `addressables` | `## [addressables/X.Y.Z] - YYYY-MM-DD` | `[Addressables] vX.Y.Z` |
| `di` | `## [di/X.Y.Z] - YYYY-MM-DD` | `[DI] vX.Y.Z` |
| `steam` | `## [steam/X.Y.Z] - YYYY-MM-DD` | `[Steam] vX.Y.Z` |

- Plain `vX.Y.Z` tags (`v0.0.20` through `v0.6.0`) are historical.
- The Watermelon Game tag path is `game/watermelon` while its commit scope is `watermelon`; keep each as written.

## Localized Copies

Units change only `README.md` and `CHANGELOG.md`, in English, and leave `docs/readme/README.{ko,ja}.md` and `docs/changelog/CHANGELOG.{ko,ja}.md` as they are (`project.guardrails`). Package users read the docs at release tags, and translating in every unit repeated the work at each review fix and again when the release reworded the Unreleased entries.

- Each release unit brings the four copies up to their English sources in its release commit, after the changelog is finalized: translate every difference per the `translate` skill and the glossary at `docs.glossary`, the sections of packages this release does not ship included, so that each copy matches its source at the tag.
- Keep the same section structure as the source and the links between the copies; `mu3-repo check` verifies the README links.
- A request that asks for a translation outside a release, such as a fix to a copy, is done in that unit.

## Install URLs

```text
https://github.com/doqltl179/Mu3Library_ForUnity.git?path=<package folder>#<tag>
```

- In the same release commit, point the package's URL under «Option 1: Package Manager (Git URL)» in `README.md` and `docs/readme/README.{ko,ja}.md` at the new tag. These URLs are the only version strings the READMEs carry: the versions themselves stay in each `package.json` and the release notes in `CHANGELOG.md`, so a README section never repeats a version number.
- A package README that shows install URLs (Watermelon Game) writes them with version placeholders and links to the root README, so a release leaves it unchanged.
- The first release of a package that no release has shipped yet, such as one split out of Base ([package-split.md](package-split.md)), adds its URL there in place of the sentence that it ships with the next release, and drops that wording from the package list at the end of the root READMEs, from Base's README, and from «Installation» in the package's own README, which then sends the reader to the root README for the URL.
- The first release of a package from `packages/<name>` also changes `?path=` in its URLs, in the root READMEs and in the Watermelon Game README, from the former `Mu3Library_*` folder to that folder; a tag released before keeps the folder its tree holds.

## Dependency Versions

- A package names the lowest version of another Mu3Library package that it works with. URP names Base 0.25.0. Watermelon Game names Base 0.29.0, the first Base whose `Mu3Library.Foundation` holds the `IUpdatable` that its use of `AudioManager` needs; Watermelon Game 0.7.0 fails to compile with CS0012 against Base 0.29.0, so the two update together.
- The release that first ships a package split out of Base checks each `dependencies` entry of its `package.json`: Base at the version this release gives Base, the first without the moved code, and each Unity SDK at the version the `unity` floor bundles ([package-split.md](package-split.md) «Dependencies»). Correct an entry that names another version in the release commit, before the tag.

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
- Install: `https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/base#base/vX.Y.Z`

## Full Changelog
https://github.com/doqltl179/Mu3Library_ForUnity/compare/PREV_TAG...NEW_TAG
```
