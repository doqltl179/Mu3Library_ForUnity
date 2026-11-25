# Changelog — Mu3 Library Steam

Release history for `com.github.doqltl179.mu3library.steam`.

Detailed per-release notes live in the repository root `CHANGELOG.md` under the `steam/x.y.z` tags; this file marks the package-local view of them.

## [Unreleased]

## [0.4.0] - 2026-11-25

- See `steam/0.4.0` in the repository root `CHANGELOG.md`. Highlights: `SteamManager` starts Steam, runs Steam's callbacks, and shuts Steam down for a game that uses achievements, stats, or Steam Cloud without multiplayer.

## [0.3.0] - 2026-11-22

- See `steam/0.3.0` in the repository root `CHANGELOG.md`. Highlights: achievements and stats through `SteamStatsManager`, Steam Cloud saves for Base's save system through `SteamCloudSaveStorage`, and optional debug console commands, so the package needs Base 0.32.0 or later.

## [0.2.0] - 2026-11-22

- See `steam/0.2.0` in the repository root `CHANGELOG.md`. Highlights: `SteamBackend` implements Base's `IVoiceBackend` with Steam voice, so the package needs Base 0.31.0 or later.

## [0.1.1] - 2026-11-20

- See `steam/0.1.1` in the repository root `CHANGELOG.md`. Highlights: the README no longer points to Base's **Multiplayer** sample, which Base 0.30.0 removes; the code is unchanged.

## [0.1.0] - 2026-11-20

- See `steam/0.1.0` in the repository root `CHANGELOG.md`. First release: `SteamBackend`, which came with Base up to Base 0.28.0, in a package of its own, now in the `Mu3Library.Steam.Multiplayer` assembly and namespace.
