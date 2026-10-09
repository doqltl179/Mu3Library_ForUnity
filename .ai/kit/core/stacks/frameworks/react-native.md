---
id: react-native
title: React Native
kind: framework
applies_to: []
related: [react, typescript, kotlin, swift]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://reactnative.dev/docs/getting-started", "https://reactnative.dev/blog", "https://reactnative.dev/architecture/landing-page", "https://reactnative.dev/blog/2026/08/11/react-native-0.87", "https://docs.expo.dev/", "https://expo.dev/changelog", "https://expo.dev/changelog/sdk-57", "https://expo.dev/changelog/sdk-58-beta"]
---

# React Native

## Detect
- Expo or bare: an `expo` dependency with `app.json`/`app.config.(js|ts)` means Expo. If `ios/` and `android/` are gitignored, the project uses Continuous Native Generation (CNG, `expo prebuild`); if they are committed, native code is edited in place.
- Versions: `react-native` in `package.json`; in Expo projects the `expo` SDK version decides compatible React Native and library versions.
- Navigation: Expo Router (`expo-router` with an `app/` directory) or React Navigation.
- New Architecture setting: `newArchEnabled` in `app.json` or `android/gradle.properties`, and `RCT_NEW_ARCH_ENABLED` for iOS.
- Metro config (`metro.config.js`), monorepo layout, and `babel.config.js` plugins (for example Reanimated).
- Tests: Jest with the `jest-expo` or `react-native` preset and `@testing-library/react-native`; E2E with Detox or Maestro.

## Conventions
- Build UI from React Native primitives (`View`, `Text`, `Pressable`, `Image`); render every string inside `<Text>`.
- Style with `StyleSheet.create` or the project's styling library; flexbox defaults to `flexDirection: "column"`, and styles do not cascade except text styles into nested `<Text>`.
- Small platform differences: `Platform.OS` or `Platform.select`. Larger ones: `Name.ios.tsx`/`Name.android.tsx` (or `.native.tsx`/`.web.tsx`), imported without the suffix.
- Render long or dynamic lists with `FlatList`/`SectionList` (or the project's list library), never `ScrollView` plus `map`; supply `keyExtractor`, memoized item components, and `getItemLayout` for fixed-height rows.
- Keep native calls behind one typed JS module per capability; write new native modules with the project's approach (Turbo Modules or the Expo Modules API).
- In Expo projects add libraries with `npx expo install <pkg>` so versions match the SDK; in a CNG project change native config through config plugins.
- Treat everything in the JS bundle as public: `EXPO_PUBLIC_*` and other inlined env values are not secrets.
- Respect safe areas (`react-native-safe-area-context`) and the keyboard (`KeyboardAvoidingView`); set `accessibilityLabel` and `accessibilityRole` on custom touchables.

## Verify
- `commands.test`, `commands.lint`, `commands.typecheck`; `commands.e2e` for Detox or Maestro flows.
- Expo: `npx expo-doctor` checks dependency compatibility.
- After native dependency or config changes, rebuild the binary (`npx expo run:ios`/`run:android`, or `pod install` in `ios/` then a native build); a Metro reload is not enough.

## Pitfalls
- Edits to `ios/` or `android/` in a CNG project are lost the next time prebuild regenerates them; use a config plugin.
- Adding a library with native code and only reloading JavaScript fails with a missing native module; rebuild the app.
- A stale bundler cache after config or dependency changes; restart Metro with `npx expo start -c` or `npx react-native start --reset-cache`.
- Inline object and arrow-function props in `renderItem` of large lists re-render every row; memoize the item component and callbacks.
- Web-only libraries that touch the DOM crash on device; pick libraries that support React Native.
- Upgrading `react-native` alone in an Expo project breaks native compatibility; upgrade the Expo SDK instead.

## Version Notes
- The New Architecture (Fabric, Turbo Modules, bridgeless) is enabled by default since React Native 0.76, and from 0.82 it is the only architecture: `newArchEnabled=false` and `RCT_NEW_ARCH_ENABLED=0` are ignored (as of 2026-10, per reactnative.dev/blog). Check native libraries for New Architecture support before adding them.
- React Native 0.87 (August 2026) is the latest stable release and 0.84 is unsupported. Its public JS API is the Strict TypeScript API: import only from the `react-native` root; deep imports (`react-native/Libraries/*`) are type errors, `*Properties` type aliases are gone (use `*Props`), and refs have types such as `ViewInstance`. The `react-native-legacy-deep-imports` tsconfig custom condition opts out through 0.88 only (as of 2026-10, per reactnative.dev/blog 0.87).
- React Native 0.87 requires Node.js 22.13+ and Kotlin 2.0+; removes `InteractionManager` (use `requestIdleCallback`), the `Modal` `animated` prop, and the `StatusBar` `backgroundColor`/`translucent` props; `useColorScheme()` returns `null` instead of `'unspecified'`; `ImageBackground` is deprecated. Swift Package Manager is experimental; CocoaPods stays the default (as of 2026-10, per reactnative.dev/blog 0.87).
- Each Expo SDK targets one React Native version: SDK 57 (June 2026) uses React Native 0.86 and React 19.2; SDK 58 is in beta on a React Native 0.88 release candidate, so no stable Expo SDK uses 0.87. Read the SDK changelog before upgrading (as of 2026-10, per expo.dev/changelog/sdk-57 and sdk-58-beta).
- From Expo SDK 57, `npx expo prebuild` clears and regenerates `android/` and `ios/` by default (`--no-clean` keeps them). Builds with the iOS 27 SDK need the UIKit scene life cycle: default in SDK 58, opt-in on SDK 57 (`expo@57.0.23`+) with `ios.enableSceneSupport` in `expo-build-properties` (as of 2026-10, per expo.dev/changelog/sdk-57).
