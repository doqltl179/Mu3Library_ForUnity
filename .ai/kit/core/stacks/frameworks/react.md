---
id: react
title: React
kind: framework
applies_to: ["**/*.jsx", "**/*.tsx"]
related: [typescript, nextjs, react-native]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://react.dev/reference/rules", "https://react.dev/learn/you-might-not-need-an-effect", "https://react.dev/blog", "https://react.dev/blog/2026/09/09/react-19-3", "https://testing-library.com/docs/queries/about/"]
---

# React

## Detect
- `react` and `react-dom` versions in `package.json`. The host decides routing and data loading (Next.js, React Native, a router library, or a plain SPA); load that pack too when one exists.
- React Compiler: `babel-plugin-react-compiler` or the framework's compiler option. When enabled, it memoizes components and hooks automatically.
- State and data libraries already in use (for example: Redux Toolkit, Zustand, TanStack Query, context). Extend them; do not add a second library for the same job.
- Lint config: `eslint-plugin-react-hooks` rules and whether they are errors.
- Test stack: Jest or Vitest with `@testing-library/react`, `@testing-library/user-event`, and a DOM environment (`jsdom` or `happy-dom`).

## Conventions
- Keep components and hooks pure during render: no side effects, no mutation of props, state, or outer values; read and write `ref.current` only in handlers and effects.
- Call hooks only at the top level of components and `use*` custom hooks; never inside conditions, loops, nested functions, or after an early return.
- Colocate state in the lowest component that needs it; lift it to the closest common parent only when siblings share it. Do not copy props into state.
- Derive values during render from props and state instead of storing them and syncing with an effect.
- Use effects only to synchronize with systems outside React (subscriptions, timers, imperative DOM or widget APIs, network outside a data library), and return a cleanup. Put logic caused by a user action in its event handler.
- Treat state as immutable: replace objects and arrays; use the updater form `setX(prev => ...)` when the next value depends on the previous one.
- Give list items a stable, unique `key` from the data. Change a component's `key` to reset its state on purpose.
- Add `useMemo`, `useCallback`, or `memo` only when the Profiler shows a costly render or a dependency needs a stable identity; skip manual memoization when the React Compiler is enabled.
- Use semantic elements (`button`, `a`, `label`, `nav`, `ul`) instead of clickable `div`s; label every form control; add ARIA only when no native element expresses the role.
- Test as a user: query by role, then label, then text (`getByRole` first); drive input with `userEvent`; await async UI with `findBy*`; assert on rendered output, not on state or internals.

## Verify
- Lint with hooks rules: `commands.lint`; types: `commands.typecheck`.
- Tests: `commands.test`; build: `commands.build`.

## Pitfalls
- Silencing `react-hooks/exhaustive-deps` creates stale closures; move the logic into the effect, use the updater form, or restructure the dependency.
- Fetching in an effect without cleanup lets stale responses overwrite fresh ones; use the project's data library or ignore stale results in the cleanup.
- Defining a component inside another component remounts it every render and drops its state; define it at module level.
- Index or random keys on lists that reorder, insert, or delete attach state to the wrong row; use data ids.
- Strict Mode runs render and effects twice in development to expose missing cleanup; fix the effect, do not remove `StrictMode`.
- `{count && <X />}` renders `0` when `count` is 0; write `count > 0 && <X />`.
- Object or function literals passed as context values or effect dependencies change identity every render; memoize them or move them out of the component.

## Version Notes
- React 19: `ref` is a regular prop on function components (`forwardRef` not needed); `ReactDOM.render` and function-component `propTypes`/`defaultProps` are removed; Actions, `useActionState`, `useOptimistic`, and `use` are available (as of 2026-10, per react.dev/blog).
- React 19.2 added `<Activity>` and `useEffectEvent`; React Compiler 1.0 is stable (as of 2026-10, per react.dev/blog). Check the installed version before using them.
- React 19.3 makes `<ViewTransition>` and Fragment refs stable. Import `ViewTransition` and `addTransitionType` from `react`; `<ViewTransition>` animates only updates inside a Transition (`startTransition`, a `<Suspense>` reveal, `useDeferredValue`) and works only in the DOM. `<Fragment ref={ref}>` yields a `FragmentInstance` (`addEventListener`, `focus`, `observeUsing`, `getClientRects`) (as of 2026-10, per react.dev/blog/2026/09/09/react-19-3). Use them only when the installed `react` is 19.3 or later.
- React 19.3 also adds `browser()` from `react-dom` (`use(browser())` suspends only during server rendering) and makes Strict Mode double-invoke effects during hydration as well (as of 2026-10, per react.dev/blog/2026/09/09/react-19-3).
