# Changelog — Mu3 Library (Base)

Release history for `com.github.doqltl179.mu3library.base`.

Detailed per-release notes live in the repository root `CHANGELOG.md` under the `base/x.y.z` tags; this file marks the package-local view of them.

## [Unreleased]

## [0.27.0] - 2026-10-31

- See `base/0.27.0` in the repository root `CHANGELOG.md`. Highlights: Unity 6000.3 becomes the lowest supported Editor; MVP view animations and audio fades run on unscaled time by default, with the new `TimeMode` to choose the clock; a failed Addressables scene load reaches `ISceneLoaderEventBus.OnSceneLoadFailed`, and catalog updates can clean the bundle cache (a class that implements `IAddressablesManager` itself has to add the new overloads); the package compiles on Unity 6.5 and later; and the Template sample shrinks from 30.4 MiB to 15.7 MiB.

## [0.26.0] - 2026-08-17

- See `base/0.26.0` in the repository root `CHANGELOG.md`. Highlights: the new optional `Mu3Library.Notifications` assembly wrapping the Mobile Notifications package's unified `NotificationCenter` — initialization, permission requests, scheduling, cancels, badge clearing, the opened-from notification, a received-notification event bus, and UniTask async variants; compiled only on Android/iOS/Editor while `com.unity.mobile.notifications` is installed.

## [0.25.0] - 2026-08-17

- See `base/0.25.0` in the repository root `CHANGELOG.md`. Highlights: Foundation logging bridge (`Mu3Logger`), audio mixer routing/ducking/persisted volumes and an audio UniTask surface, Localization AssetTable loading, MVP `OpenAsync`/`CloseAsync`, WebRequest PUT/PATCH/DELETE with backoff/cancellation/progress, type-aware Addressables caching, and `Container.CreateScope()` becoming internal (breaking).

## [0.24.1] - 2026-08-16

- See `base/0.24.1` in the repository root `CHANGELOG.md`.
