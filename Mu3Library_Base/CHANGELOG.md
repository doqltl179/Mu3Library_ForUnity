# Changelog — Mu3 Library (Base)

Release history for `com.github.doqltl179.mu3library.base`.

Detailed per-release notes live in the repository root `CHANGELOG.md` under the `base/x.y.z` tags; this file marks the package-local view of them.

## [Unreleased]

## [0.26.0] - 2026-08-17

- See `base/0.26.0` in the repository root `CHANGELOG.md`. Highlights: the new optional `Mu3Library.Notifications` assembly wrapping the Mobile Notifications package's unified `NotificationCenter` — initialization, permission requests, scheduling, cancels, badge clearing, the opened-from notification, a received-notification event bus, and UniTask async variants; compiled only on Android/iOS/Editor while `com.unity.mobile.notifications` is installed.

## [0.25.0] - 2026-08-17

- See `base/0.25.0` in the repository root `CHANGELOG.md`. Highlights: Foundation logging bridge (`Mu3Logger`), audio mixer routing/ducking/persisted volumes and an audio UniTask surface, Localization AssetTable loading, MVP `OpenAsync`/`CloseAsync`, WebRequest PUT/PATCH/DELETE with backoff/cancellation/progress, type-aware Addressables caching, and `Container.CreateScope()` becoming internal (breaking).

## [0.24.1] - 2026-08-16

- See `base/0.24.1` in the repository root `CHANGELOG.md`.
