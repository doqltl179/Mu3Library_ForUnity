---
id: swift
title: Swift
kind: language
applies_to: ["**/*.swift"]
related: [swiftui]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://www.swift.org/documentation/", "https://docs.swift.org/swift-book/documentation/the-swift-programming-language/", "https://www.swift.org/migration/documentation/migrationguide/", "https://developer.apple.com/documentation/testing", "https://www.swift.org/blog/"]
---

# Swift

## Detect
- Build system: `Package.swift` → SwiftPM; `*.xcodeproj`/`*.xcworkspace` → Xcode; `Podfile` → CocoaPods. A generator spec (`project.yml` for XcodeGen, `Project.swift` for Tuist) means: edit the spec, then regenerate.
- Versions: the `// swift-tools-version:` line and `platforms:` in `Package.swift`; Xcode build settings `SWIFT_VERSION` and deployment targets.
- Concurrency checking per target: language mode (`swiftLanguageModes`, `SWIFT_VERSION`), `SWIFT_STRICT_CONCURRENCY`, enabled upcoming features, and default actor isolation settings.
- Test framework: `import Testing` (Swift Testing) or `import XCTest`.
- Style: `.swiftlint.yml`, `.swift-format`.

## Conventions
- Unwrap optionals with `guard let`, `if let`, or `??`; no `!`, `try!`, or `as!` outside tests or invariants stated in a comment.
- Value types (`struct`, `enum`) by default; `final class` when identity or reference semantics is required.
- `let` over `var`; narrowest access level; `public` only for module API.
- Write new async code with `async`/`await`; bridge callback APIs with `withCheckedThrowingContinuation`, resuming exactly once.
- Protect shared mutable state with an `actor`; keep UI state on `@MainActor`.
- Values crossing isolation boundaries must be `Sendable`. Fix diagnostics with immutable values, actors, or isolation; use `@unchecked Sendable` or `nonisolated(unsafe)` only with a comment proving safety.
- Prefer structured concurrency (`async let`, `withTaskGroup`) over unstructured `Task {}`; call `try Task.checkCancellation()` in long-running loops.
- Capture `[weak self]` in escaping closures that `self` (or something it owns) retains.

## Verify
- SwiftPM: `swift build`; `swift test`, narrowed with `--filter <pattern>`.
- Xcode: `xcodebuild -list` to find schemes, then `xcodebuild test -scheme <Scheme> -destination '<destination>'`.
- Lint and format: `commands.lint`, `commands.format`; for example `swiftlint` and `swift format lint --recursive <dirs>`.

## Pitfalls
- Actor reentrancy: state read before an `await` inside an actor may have changed after it; re-check after each suspension.
- `Task {}` inherits the current actor; `Task.detached` does not. Do not use `Task.detached` to silence isolation errors.
- Blocking a thread to wait for async work (semaphores, `DispatchGroup.wait()`) can deadlock the cooperative thread pool.
- Hand-editing `project.pbxproj` corrupts projects and merges badly; use Xcode, the generator spec, or minimal targeted edits.
- In Swift Testing `@Test` functions, use `#expect`/`#require`, not `XCTAssert*`.
- Forced unwraps of IBOutlets, dictionary lookups, or `URL(string:)` crash on unexpected input; handle the `nil` case.

## Version Notes
- Swift 6 language mode makes data-race safety diagnostics errors; Swift 5 mode reports them as warnings when strict checking is enabled (as of 2026-10, per swift.org migration guide).
- Swift 6.2 adds opt-in default `@MainActor` isolation per module and `@concurrent` for explicitly running off the actor; check target settings before assuming where nonisolated async code runs (as of 2026-10, per swift.org blog).
- `swift format` ships with the toolchain from Swift 6; Swift Testing ships with Swift 6 toolchains and Xcode 16+ (as of 2026-10, per swift.org).
