---
id: swiftui
title: SwiftUI
kind: framework
applies_to: []
related: [swift]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://developer.apple.com/documentation/swiftui", "https://developer.apple.com/documentation/observation", "https://developer.apple.com/documentation/swiftui/migrating-from-the-observable-object-protocol-to-the-observable-macro", "https://developer.apple.com/documentation/updates/swiftui", "https://www.swift.org/blog/"]
---

# SwiftUI

## Detect
- Deployment target per platform in the Xcode project (`IPHONEOS_DEPLOYMENT_TARGET`, `MACOSX_DEPLOYMENT_TARGET`) or `platforms:` in `Package.swift`; it decides which APIs need `if #available`.
- Swift language mode and concurrency checking (`SWIFT_VERSION`, `SWIFT_STRICT_CONCURRENCY`) and the target's default actor isolation setting.
- Observation model: `@Observable` classes or `ObservableObject` with `@Published`. Follow the model used by the feature being changed.
- Architecture (for example MVVM view models or a unidirectional state library) and navigation (`NavigationStack`/`NavigationSplitView` or `NavigationView`).
- Tests: XCTest or Swift Testing (`import Testing`, `@Test`), UI test targets, snapshot libraries; the schemes and destinations used with `xcodebuild`.

## Conventions
- Keep views small value types with a cheap, side-effect-free `body`; extract subviews instead of growing one `body`.
- Own each piece of state in one place: `@State private var` for view-owned values, `@Binding` for a child that edits parent state, `@Environment` for shared dependencies.
- With `@Observable`: own a model with `@State`, pass it as a plain property, create bindings with `@Bindable`, and inject shared models with `.environment(_:)`.
- With `ObservableObject`: own with `@StateObject`, observe a passed-in object with `@ObservedObject`, inject with `@EnvironmentObject`.
- Preserve identity: `ForEach` over `Identifiable` data or a stable `id:` key path; change `.id(_:)` only to reset a view on purpose. Prefer a modifier with a conditional value over `if`/`else` branches that swap views and reset their state.
- Mark UI-facing models `@MainActor`; start async work in `.task { }` or `.task(id:)`, which cancel automatically, not in `Task { }` inside `onAppear`.
- Provide `#Preview` blocks with in-memory sample data; previews must not hit the network.
- Navigate with `NavigationLink(value:)` plus `navigationDestination(for:)` when the deployment target allows.
- Use `Label`, text-bearing `Button`s, and text styles (`.font(.body)`) so VoiceOver and Dynamic Type work; add `.accessibilityLabel`, `.accessibilityHint`, and `.accessibilityElement(children:)` for custom controls.
- Guard APIs newer than the deployment target with `if #available` and a fallback.

## Verify
- `commands.build` and `commands.test` (for example `xcodebuild test -scheme <Scheme> -destination '<destination>'`, or `swift test` for packages).
- `commands.lint` when SwiftLint or swift-format is configured; confirm changed previews still compile.

## Pitfalls
- `@ObservedObject` on an object the view creates recreates it on every parent update; own it with `@StateObject` (or `@State` with `@Observable`).
- `@State` and `@StateObject` take their initial value from `init` only once; later parameter changes are ignored. Pass a binding, or key the view with `.id` to reset it.
- Mutating UI state from a background task causes runtime warnings and races; isolate the model to `@MainActor`.
- `ForEach(items, id: \.self)` over duplicate or mutable values breaks animations and state; use stable ids.
- `AnyView` erases types and hurts diffing; use `@ViewBuilder` or `Group`.
- Heavy work in view initializers or `body` runs on every re-evaluation; move it into the model or `.task`.
- `onAppear` can fire more than once; make its work idempotent.

## Version Notes
- `@Observable` needs iOS 17 / macOS 14; `NavigationStack` needs iOS 16 / macOS 13 and deprecates `NavigationView`; `#Preview` needs Xcode 15 (as of 2026-10, per developer.apple.com/documentation).
- Swift 6 language mode reports data races as errors; Swift 6.2 adds per-target default actor isolation (SE-0466), which new Xcode 26 projects may set to `MainActor` (as of 2026-10, per swift.org). Check build settings before adding isolation annotations.
