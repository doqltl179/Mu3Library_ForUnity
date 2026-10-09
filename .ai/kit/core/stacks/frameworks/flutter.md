---
id: flutter
title: Flutter
kind: framework
applies_to: ["**/lib/**/*.dart"]
related: [dart]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.flutter.dev/", "https://docs.flutter.dev/release/release-notes", "https://docs.flutter.dev/release/breaking-changes", "https://dart.dev/tools/linter-rules"]
---

# Flutter

## Detect
- SDK constraints in `pubspec.yaml` (`environment: sdk`, `flutter`); a pin in `.fvmrc` or `.fvm/` means run `fvm flutter` instead of `flutter`.
- Lint set in `analysis_options.yaml` (for example `flutter_lints`, `very_good_analysis`) and any custom rules.
- State management from dependencies: `provider`, `flutter_riverpod`/`hooks_riverpod`, `flutter_bloc`, or plain `ChangeNotifier`/`ValueNotifier`. Use the one already there.
- Code generation: `build_runner` with `freezed`, `json_serializable`, or `riverpod_generator`, producing `*.g.dart` and `*.freezed.dart`.
- Routing (`go_router`, `auto_route`, or `Navigator`), platform folders (`android/`, `ios/`, `web/`, desktop), flavors, and monorepo tooling such as melos.

## Conventions
- Compose UI from small widget classes rather than methods that return widgets; classes get their own element, rebuild boundary, and `const` support.
- Use `const` constructors and `const` widget instances wherever all arguments are constant.
- Keep `build` pure and cheap: no I/O and no controller creation. Create controllers, focus nodes, and subscriptions in `initState`; release them in `dispose`.
- Use `StatelessWidget` unless the widget owns ephemeral UI state; keep app and shared state in the project's state-management solution.
- After any `await` in a `State` method, check `if (!mounted) return;` before using `context` or calling `setState`; elsewhere check `context.mounted`.
- Read providers per the library: `context.watch`/`ref.watch` in `build`, `context.read`/`ref.read` in callbacks.
- Build long or unbounded lists with `ListView.builder` or slivers; give reorderable items a `ValueKey` from the data.
- Run heavy computation off the UI isolate with `Isolate.run` or `compute`.
- Platform channels: define channel and method names once, keep Dart and native encoding symmetric, and handle `PlatformException` and `MissingPluginException`; use Pigeon when the project does.
- Take colors and text styles from `Theme.of(context)`; give icon-only buttons a `tooltip` or `Semantics` label.

## Verify
- `commands.lint` or `flutter analyze`; `dart format --set-exit-if-changed .`.
- `commands.test` or `flutter test`; integration tests with `flutter test integration_test`.
- After editing annotated sources, regenerate: `dart run build_runner build --delete-conflicting-outputs`.
- Golden tests (`matchesGoldenFile`) depend on platform fonts and rendering; run them on the platform that produced the goldens, and update with `flutter test --update-goldens` only for intended visual changes.

## Pitfalls
- Using `BuildContext` after an async gap without a mounted check (lint `use_build_context_synchronously`) targets a disposed widget.
- Missing `dispose` for controllers, `FocusNode`s, timers, and stream subscriptions leaks memory and fires callbacks on dead widgets.
- A `ListView` or other scrollable inside a `Column` without `Expanded` or a bounded height throws an unbounded-constraints error.
- `MediaQuery.of(context)` rebuilds on every media change; use the specific accessor such as `MediaQuery.sizeOf(context)`.
- Editing generated `*.g.dart` or `*.freezed.dart` files; change the source and rerun `build_runner`.
- Calling `setState` after `dispose` throws; guard with `mounted` and cancel async work in `dispose`.

## Version Notes
- Material 3 is the default theme since Flutter 3.16; `Color.withOpacity` is deprecated in favor of `withValues(alpha: ...)`; `WillPopScope` is replaced by `PopScope`; `MaterialState*` types are renamed `WidgetState*` (as of 2026-10, per docs.flutter.dev/release/breaking-changes). Check the SDK constraint before using the newer API.
