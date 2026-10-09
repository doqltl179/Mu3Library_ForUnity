---
id: jetpack-compose
title: Jetpack Compose
kind: framework
applies_to: []
related: [kotlin]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://developer.android.com/develop/ui/compose", "https://developer.android.com/jetpack/androidx/releases/compose", "https://developer.android.com/develop/ui/compose/side-effects", "https://developer.android.com/develop/ui/compose/performance/stability", "https://developer.android.com/develop/ui/compose/testing", "https://kotlinlang.org/docs/compose-compiler-migration-guide.html"]
---

# Jetpack Compose

## Detect
- Compose BOM (`androidx.compose:compose-bom`) and Kotlin versions in `gradle/libs.versions.toml` or `build.gradle.kts`; the Compose compiler plugin (`org.jetbrains.kotlin.plugin.compose`) on Kotlin 2.0+.
- Android-only Compose or Compose Multiplatform (`org.jetbrains.compose`); Material 3 (`material3`) or Material 2 (`material`).
- ViewModel and DI wiring: Hilt (`hiltViewModel()`), Koin, or manual factories; the navigation library and its route style.
- Compiler stability settings: `stabilityConfigurationFile`, compiler reports or metrics options.
- Tests: `ui-test-junit4` rules, Robolectric, and screenshot tooling (for example Paparazzi, Roborazzi, or Compose Preview Screenshot Testing).

## Conventions
- Hoist state: screen-level composables own state and pass `value` plus event lambdas (`onValueChange`) to stateless children.
- Give every reusable composable a `modifier: Modifier = Modifier` parameter, the first optional one, applied to its root layout.
- Keep composables free of side effects; they may run often, in any order, or be skipped.
- `remember { }` keeps values across recompositions; `rememberSaveable` also survives configuration change and process death; pass keys (`remember(key) { }`) when the inputs change.
- Use `derivedStateOf` when a state changes more often than the value derived from it (for example scroll offset to "show button").
- Side effects: `LaunchedEffect(keys)` for suspend work tied to keys; `DisposableEffect` with `onDispose` for register/unregister; `rememberCoroutineScope` to launch from callbacks; `rememberUpdatedState` for the latest lambda inside a long-running effect; `SideEffect` to publish state to non-Compose code.
- Expose screen state from the ViewModel as `StateFlow` and collect it with `collectAsStateWithLifecycle()`; below the screen pass state and lambdas, not the ViewModel.
- Model UI state with immutable `val` properties and immutable collections; add `@Immutable` or `@Stable` only when the contract truly holds.
- Give `LazyColumn`/`LazyRow` items a stable `key` (and `contentType` for mixed rows).
- Modifier order is significant: modifiers apply outside-in, so `padding` before `clickable` shrinks the touch area and `background` before `padding` colors the padding.
- Defer reads of fast-changing state to later phases with lambda modifiers (`Modifier.offset { }`, `drawBehind { }`).
- Previews: annotate stateless composables with `@Preview` and fake data (`@PreviewParameter`); never construct ViewModels in previews.
- Accessibility: set `contentDescription` on meaningful icons and images (`null` for decorative ones); merge semantics for compound controls.

## Verify
- `commands.build` (for example `./gradlew assembleDebug`), `commands.test` for JVM tests, `commands.e2e` for instrumented tests (`connectedDebugAndroidTest`), `commands.lint`.
- Compose tests use `createComposeRule()`, find nodes by semantics (`onNodeWithText`, `onNodeWithContentDescription`, `onNodeWithTag` with `Modifier.testTag`), then act (`performClick()`) and assert.
- For recomposition problems, read the Compose compiler reports for unstable parameters before adding annotations.

## Pitfalls
- `mutableStateOf` without `remember` resets on every recomposition; wrap it in `remember`.
- Launching coroutines or calling suspend APIs directly in a composable body; use `LaunchedEffect` or `rememberCoroutineScope`.
- `LaunchedEffect(Unit)` that reads changing values uses stale ones; add them as keys or wrap with `rememberUpdatedState`.
- `collectAsState()` on Android keeps collecting while the app is in the background; use `collectAsStateWithLifecycle()`.
- Mutating a `MutableList` held in `mutableStateOf` does not trigger recomposition; replace the list or use `mutableStateListOf`.
- Writing to state already read earlier in the same composition loops recomposition; move the write to an event handler or effect.
- Lazy items without keys keep `remember` state at the wrong position after a reorder; set `key`.

## Version Notes
- From Kotlin 2.0 the Compose compiler ships with Kotlin: apply `org.jetbrains.kotlin.plugin.compose` at the Kotlin version; strong skipping is on by default since Kotlin 2.0.20 (as of 2026-10, per kotlinlang.org and developer.android.com).
- Align Compose library versions through the BOM; check the BOM mapping before upgrading a single artifact (as of 2026-10, per developer.android.com/jetpack/androidx/releases/compose).
