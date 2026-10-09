---
id: kotlin
title: Kotlin
kind: language
applies_to: ["**/*.kt", "**/*.kts"]
related: [java, jetpack-compose, spring-boot]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://kotlinlang.org/docs/home.html", "https://kotlinlang.org/docs/releases.html", "https://kotlinlang.org/docs/coroutines-guide.html", "https://kotlinlang.org/docs/gradle.html", "https://developer.android.com/build/migrate-to-ksp"]
---

# Kotlin

## Detect
- Target from Gradle plugins: `kotlin("jvm")` → JVM; `com.android.application`/`com.android.library` → Android; `kotlin("multiplatform")` → Multiplatform, with source sets such as `commonMain`, `androidMain`, `iosMain`, `jvmMain`.
- Versions: Kotlin and plugin versions in `gradle/libs.versions.toml` or `plugins {}`; JVM target from `jvmToolchain(N)` or `compilerOptions.jvmTarget`.
- Build logic: `settings.gradle.kts`, convention plugins in `build-logic/` or `buildSrc/`. Use `./gradlew`.
- Annotation processing: KSP (`com.google.devtools.ksp`) or kapt (`kotlin("kapt")`).
- Style and lint: `.editorconfig` with ktlint, detekt config (`detekt.yml`), Spotless.
- Test stack: JUnit 5, `kotlin.test`, or Kotest; MockK or Mockito; `kotlinx-coroutines-test`.

## Conventions
- Non-null types by default; handle nullability with `?.`, `?:`, smart casts, or `requireNotNull(x) { "reason" }`. Treat Java platform types as nullable unless annotated.
- Prefer `val` and read-only collection types; `data class` for value holders; `sealed` hierarchies with exhaustive `when` and no `else` branch for closed states.
- Structured concurrency: launch coroutines only in a scope tied to a lifecycle (`coroutineScope`, `supervisorScope`, an injected or framework scope); never `GlobalScope`.
- Make `suspend` functions main-safe: move blocking work into `withContext(Dispatchers.IO)` (CPU work into `Dispatchers.Default`); inject dispatchers so tests can replace them.
- Never swallow `CancellationException`: when catching `Exception` around suspend calls, rethrow it or call `ensureActive()`.
- Expose `Flow`/`StateFlow` from public API, not mutable flows.
- In Multiplatform, keep `commonMain` free of JVM and Android APIs; put platform code behind `expect`/`actual` or interfaces.
- Test coroutines with `runTest` and test dispatchers, not `Thread.sleep` or real delays.

## Verify
- `commands.build`, `commands.test`, or `./gradlew check` (tests plus configured linters); list task names with `./gradlew tasks --all`.
- Narrow tests: `./gradlew :module:test --tests 'pkg.ClassTest'`; Android unit tests `./gradlew :app:testDebugUnitTest`; Multiplatform `./gradlew allTests`.
- Lint: `commands.lint` (for example `ktlintCheck`, `detekt`, Android `lint`).

## Pitfalls
- `!!` turns a type error into a runtime crash; restructure, or fail with a message via `requireNotNull`.
- `runBlocking` on a UI or request thread blocks it; use it only in `main` functions, and `runTest` in tests.
- Catching `Throwable`/`Exception` around suspend calls swallows cancellation and keeps cancelled work running.
- Reading a `lateinit` property before assignment throws; prefer constructor injection or `by lazy`.
- `data class` `copy` is shallow; mutable members stay shared.
- Upgrading Kotlin without checking KSP and other compiler-plugin compatibility breaks the build; upgrade them together.

## Version Notes
- Kotlin 2.0+ uses the K2 compiler by default, and the Compose compiler is the Kotlin Gradle plugin `org.jetbrains.kotlin.plugin.compose`, versioned with Kotlin (as of 2026-10, per kotlinlang.org).
- kapt is in maintenance mode; prefer KSP when the processor supports it (Android Data Binding still needs kapt) (as of 2026-10, per developer.android.com/build/migrate-to-ksp).
- Check the project's Kotlin version before using newer language features; the release page lists what each version stabilized (as of 2026-10, per kotlinlang.org/docs/releases.html).
