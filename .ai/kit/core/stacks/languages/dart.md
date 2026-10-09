---
id: dart
title: Dart
kind: language
applies_to: ["**/*.dart"]
related: [flutter]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://dart.dev/language", "https://dart.dev/effective-dart", "https://dart.dev/tools/analysis", "https://dart.dev/tools/pub/pubspec", "https://github.com/dart-lang/sdk/blob/main/CHANGELOG.md"]
---

# Dart

## Detect
- `pubspec.yaml`: the lower bound of `environment.sdk` sets the language version. A `flutter` SDK dependency means Flutter: use the `flutter` tool and the `flutter` pack.
- Monorepo: pub workspaces (`workspace:` in the root `pubspec.yaml`, `resolution: workspace` in members) or a Melos config.
- `analysis_options.yaml`: included rule set (for example `package:lints/recommended.yaml`, `package:flutter_lints/flutter.yaml`), extra lints, `strict-*` language modes, excluded generated files.
- Code generation: `build_runner` in `dev_dependencies`; outputs such as `*.g.dart` and `*.freezed.dart`.
- Flutter SDK pin: `.fvmrc` or the CI-declared version.

## Conventions
- Non-nullable by default; handle null with checks and type promotion, `??`, or `?.`. Use `late` only when assignment before first read is guaranteed.
- `final` for locals and fields that never change; `const` constructors and literals where possible.
- Await every `Future`; wrap intentional fire-and-forget calls in `unawaited(...)` from `dart:async`.
- Cancel every `StreamSubscription` and close every `StreamController` you create; build streams with `async*`/`yield`.
- Model closed state with sealed classes, records, and patterns in exhaustive `switch`.
- Keep implementation under `lib/src/` and export public API from `lib/<package>.dart`; follow the project lint for `package:` versus relative imports.
- Add dependencies with `dart pub add`, or `flutter pub add` in Flutter packages.

## Verify
- Format: `commands.format`, or `dart format --output=none --set-exit-if-changed <changed paths>`.
- Analyze: `commands.lint`, or `dart analyze` / `flutter analyze`, with no new issues.
- Tests: `commands.test`, or `dart test` / `flutter test`; narrow with a file path or `--name <pattern>`.
- After changing annotated sources: `dart run build_runner build --delete-conflicting-outputs`.

## Pitfalls
- `!` on a nullable value crashes at runtime; copy the value into a local and null-check it to promote.
- Unawaited futures lose errors and race with later code; enable the `unawaited_futures` lint.
- `dart:io` is unavailable on the web; use conditional imports or platform-neutral packages in shared code.
- Editing generated `*.g.dart` files; change the source and rerun `build_runner`.
- Reading a `late` variable before assignment throws `LateInitializationError`.
- Overriding `==` without `hashCode` breaks sets and maps.

## Version Notes
- Dart 3 requires sound null safety; records, patterns, sealed classes, and class modifiers need language version 3.0+ (as of 2026-10, per dart.dev).
- Promotion of private `final` fields needs 3.2+; pub workspaces need 3.6+ (as of 2026-10, per dart.dev).
- From language version 3.7, `dart format` uses the tall style and its output depends on each package's language version; format only the files you change (as of 2026-10, per dart.dev).
