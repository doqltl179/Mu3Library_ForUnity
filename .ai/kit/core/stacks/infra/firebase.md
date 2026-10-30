---
id: firebase
title: Firebase
kind: infra
applies_to: ["**/firebase.json", "**/.firebaserc", "**/firestore.rules", "**/firestore.indexes.json", "**/storage.rules", "**/database.rules.json", "**/functions/src/**", "**/functions/main.py", "**/firebase_options.dart", "**/google-services.json", "**/GoogleService-Info.plist"]
related: [flutter, unity, typescript, python, terraform]
volatility: volatile
reviewed: 2026-10-28
sources: ["https://firebase.google.com/docs/cli", "https://firebase.google.com/support/release-notes/cli", "https://firebase.google.com/docs/emulator-suite/install_and_configure", "https://firebase.google.com/docs/firestore/quotas", "https://firebase.google.com/docs/firestore/best-practices", "https://firebase.google.com/docs/firestore/security/rules-query", "https://firebase.google.com/docs/firestore/editions", "https://firebase.google.com/docs/rules/manage-deploy", "https://firebase.google.com/docs/rules/unit-tests", "https://firebase.google.com/docs/storage/security/rules-conditions", "https://firebase.google.com/docs/functions/config-env", "https://github.com/firebase/firebase-functions/releases", "https://firebase.google.com/docs/functions/retries", "https://docs.cloud.google.com/functions/docs/runtime-support", "https://firebase.google.com/support/release-notes/admin/node", "https://firebase.google.com/docs/storage/faqs-storage-changes-announced-sept-2024", "https://firebase.google.com/docs/cloud-messaging/manage-tokens", "https://firebase.google.com/docs/cloud-messaging/migrate-off-device-groups", "https://firebase.google.com/docs/flutter/setup", "https://firebase.google.com/support/release-notes/flutter", "https://firebase.google.com/docs/unity/setup", "https://firebase.google.com/support/release-notes/unity", "https://firebase.google.com/docs/projects/billing/avoid-surprise-bills", "https://firebase.google.com/docs/projects/terraform/get-started"]
---

# Firebase

## Detect
- `firebase.json`: `firestore` (rules and indexes; an array of `{database, rules, indexes}` for named databases), `functions` (`source`, `codebase`, `runtime`), `storage`, `hosting` or `apphosting`, `dataconnect`, and `emulators`. `.firebaserc` maps aliases to project IDs; confirm which project the active alias targets (`firebase use`) before any command against a project.
- CLI version from `firebase --version`, `firebase-tools` in `package.json`, or CI.
- Functions: language and SDKs (`functions/package.json` with `firebase-functions` and `firebase-admin`, or `functions/requirements.txt`), runtime (`engines.node` or `runtime` in `firebase.json`), and 1st or 2nd gen per function.
- Firestore databases (default or named) and edition (Standard or Enterprise; Enterprise does not create single-field indexes automatically).
- Flutter: `lib/firebase_options.dart` from `flutterfire configure` and the Firebase plugins in `pubspec.yaml`. Unity: `.unitypackage` imports under `Assets/Firebase` or UPM `.tgz` entries in `Packages/manifest.json`, External Dependency Manager, and `google-services.json` and `GoogleService-Info.plist` under `Assets/`.
- Other products in use, each following its official docs: Remote Config, Analytics, Hosting (static) or App Hosting (server-rendered), Realtime Database (`database.rules.json`), SQL Connect (`dataconnect/`), App Check.
- Whether Terraform provisions the Firebase projects and apps, and whether through the `google` or `google-beta` provider; follow the `terraform` pack for those files.

## Conventions
- Develop and test against the Emulator Suite: `firebase emulators:start`, or `firebase emulators:exec "<test command>"` in CI. A project ID starting with `demo-` reaches only emulators; persist seed data with `--import <dir> --export-on-exit`.
- Deploy only what changed: `--only firestore:rules`, `storage`, `functions:<name>`, or `functions:<codebase>`. Rules files in the repository are the source of truth: a CLI deploy overwrites console edits, a release takes several minutes to propagate, and reverting means redeploying the previous file from version control.
- Security Rules: `rules_version = '2';`, deny by default, grant per path from `request.auth`, and validate `request.resource.data` (allowed keys, types, sizes) on create and update; `resource` is the stored document.
- Rules are not filters: a query must constrain itself to documents the rules allow, or the whole query is denied.
- Keep `get()` and `exists()` lookups in rules few (10 per request, 20 per transaction or batch); prefer custom claims or fields on the document itself.
- Storage rules check `request.auth`, `request.resource.size`, and `request.resource.contentType`.
- Firestore data: documents stay under 1 MiB; avoid monotonically increasing document IDs and sequentially increasing indexed fields, which create hotspots; load bulk data with a bulk writer, not oversized batches.
- Keep composite indexes in `firestore.indexes.json`; export the deployed ones with `firebase firestore:indexes` before editing it.
- Give every client query a `limit`, and detach listeners when their screen or object goes away; each change a listener receives is billed.
- Functions: 2nd gen for new functions; set `region` explicitly or through `setGlobalOptions`; configure with params (`defineSecret`, `defineString`) read with `.value()` inside handlers or `onInit()`, never at global scope.
- Event functions run at least once and out of order: make handlers idempotent on the event ID, turn on retries only for idempotent handlers, and never write to the triggering document without a guard that ends the loop.
- `minInstances` bills while idle; keep it at 0 outside production through a per-environment param.
- Authorize server-side with `verifyIdToken` (`checkRevoked` for sensitive actions) or Security Rules; custom claims stay under 1000 bytes and reach clients only after a token refresh.
- App Check: watch its metrics before enforcing (enforcement takes up to 15 minutes), set `enforceAppCheck: true` on callables, and use the debug provider in CI.
- Flutter: `await Firebase.initializeApp(options: DefaultFirebaseOptions.currentPlatform)` before any Firebase call; re-run `flutterfire configure` after adding a platform or product.
- Unity: gate every Firebase call on `FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(...)` returning `DependencyStatus.Available`; install all Firebase packages with one method and one version.
- Crashlytics: in Flutter set `FlutterError.onError = FirebaseCrashlytics.instance.recordFlutterFatalError` and report `PlatformDispatcher.instance.onError` with `fatal: true`. Upload symbols where it is not automatic: Flutter `--split-debug-info` and Unity Android IL2CPP ("Create symbols.zip") need `firebase crashlytics:symbols:upload --app=<app-id> <path>`.
- FCM: send through the Admin SDK (HTTP v1); store each registration with a timestamp, treat one silent for a month as stale, and delete it on `UNREGISTERED` (404). Request `POST_NOTIFICATIONS` on Android 13+, upload an APNs auth key for iOS, and keep payloads within 4096 bytes.

## Verify
- Rules: tests with `@firebase/rules-unit-testing` (`assertSucceeds` and `assertFails` for each allowed and denied case), run through `firebase emulators:exec --only firestore,storage "<test command>"`.
- Functions: the language pack's lint and tests, then the emulator flow that triggers them.
- Never run commands against a real project (`deploy`, `firestore:delete`, `functions:delete`, `auth:import`, `database:set` or `remove`) without explicit user confirmation.

## Pitfalls
- Opening rules (`allow read, write: if true;` or expiring test-mode rules) to silence a client error; write the narrow rule and its test.
- Checking `resource.data` on create, where no stored document exists; use `request.resource.data`.
- Trusting client-sent `uid`, role, or price fields; derive them from `request.auth` or server code. The Admin SDK bypasses Security Rules, so functions validate their own inputs.
- A non-interactive `firebase deploy --force` deletes deployed indexes missing from `firestore.indexes.json`.
- Storage rules' `firestore.get()` reads only the default database, and each read is billed.
- In Unity, calling Firebase before the dependency check completes, or touching Unity objects from a continuation that is not on the main thread.

## Version Notes
- firebase-tools 15 requires Node 20+, and its emulators require Java 21+; some docs pages still list older minimums (as of 2026-10, per the CLI release notes).
- Functions runtimes: nodejs20 is decommissioned on 2026-10-30 and nodejs22 is deprecated on 2027-04-30; python310 is deprecated, and Python 3.11 to 3.14 are supported (as of 2026-10, per the Cloud Run functions runtime schedule).
- firebase-functions v7 removed `functions.config()`; deploys that still use Runtime Config fail after 2027-03. Migrate with `firebase functions:config:export` (as of 2026-10, per the firebase-functions releases and the Functions config docs).
- `firebase-admin` 14 (the Admin SDK for Node.js) requires Node 22+ and removes the namespaced API in favor of modular entry points (as of 2026-10, per the Admin release notes).
- Cloud Storage for Firebase needs the Blaze plan for every bucket from 2026-02-03; new default buckets are named `<project-id>.firebasestorage.app` (as of 2026-10, per the Storage changes FAQ).
- FCM is moving from registration tokens to Firebase Installation IDs; both work for now, and `firebase-admin` deprecates `TokenMessage` for `FidMessage` (as of 2026-10, per the FCM token docs and the Admin release notes).
- FCM device group messaging stops working after 2027-09-29 (as of 2026-10, per the device groups migration page).
- FlutterFire `firebase_core` 5 requires Flutter 3.27+ and Android API 24+, and FlutterFire supports iOS 15+ (as of 2026-10, per the FlutterFire release notes and setup page).
- Firebase Unity SDK 14 requires Unity 2022.3 LTS+ and Android API 24+, and resolves iOS dependencies with Swift Package Manager instead of CocoaPods; the setup page still describes CocoaPods (as of 2026-10, per the Unity release notes).
- Data Connect is renamed SQL Connect; the `dataconnect` key and `--only dataconnect` are unchanged (as of 2026-10, per the CLI release notes).
