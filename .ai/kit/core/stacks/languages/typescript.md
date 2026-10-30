---
id: typescript
title: TypeScript / JavaScript
kind: language
applies_to: ["**/*.ts", "**/*.tsx", "**/*.mts", "**/*.cts", "**/*.js", "**/*.jsx", "**/*.mjs", "**/*.cjs"]
related: [react, nextjs, vue, node-server, react-native]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://www.typescriptlang.org/docs/", "https://devblogs.microsoft.com/typescript/", "https://nodejs.org/en/about/previous-releases", "https://nodejs.org/en/blog/announcements/evolving-the-nodejs-release-schedule"]
---

# TypeScript / JavaScript

## Detect
- Package manager from the lockfile: `pnpm-lock.yaml`, `yarn.lock`, `package-lock.json`, or `bun.lock*`. Use only that manager.
- Scripts in `package.json` (`build`, `test`, `lint`, `typecheck`); prefer them over invoking tools directly.
- Compiler settings in `tsconfig*.json`: `strict`, `module`, `moduleResolution`, `target`, path aliases, project references.
- Module system: `"type": "module"` in `package.json` and file extensions decide ESM versus CommonJS.
- Monorepo layout: workspaces in `package.json`, `pnpm-workspace.yaml`, or a build orchestrator config.

## Conventions
- Keep `strict` type checking; do not weaken compiler options to make an error disappear.
- Prefer precise types: unions and discriminated unions over optional-everything objects; `unknown` over `any` at boundaries, narrowed by validation.
- Validate external data (HTTP, storage, environment) at the boundary with the project's schema library before trusting its type.
- Use `async`/`await` with explicit error handling; never leave a promise unhandled.
- Follow the existing import style, path aliases, and file naming of the package being changed.
- Keep runtime dependencies out of type-only imports (`import type`).

## Verify
- Type check: `commands.typecheck`, or `tsc --noEmit -p <tsconfig>` when no script exists.
- Lint and format: `commands.lint`, `commands.format`.
- Tests: `commands.test`, narrowed to the changed package when the runner supports it.

## Pitfalls
- `as` casts and non-null `!` hide real errors; narrow with checks instead.
- Mixing ESM and CommonJS imports in one package breaks at runtime, not at type-check time.
- Editing generated files (`dist/`, generated clients, `*.d.ts` outputs); change the source and regenerate.
- Installing with a different package manager than the lockfile's rewrites the lockfile.
- Assuming a Node.js global or browser API exists in the other runtime.

## Version Notes
- TypeScript 7.0 ships a native compiler, turns 6.0 deprecations into errors (for example `moduleResolution: "node"`, `baseUrl`, `target: "es5"`), and changes defaults such as `strict`; tools that import the `typescript` JS API need the 6.x package. Check the project's major version before editing `tsconfig` (as of 2026-10, per devblogs.microsoft.com/typescript).
- Check the project's TypeScript and Node.js versions before using newer syntax or APIs. Through Node.js 26 only even-numbered releases become LTS; from Node.js 27 there is one major release per year and every release becomes LTS (as of 2026-10, per nodejs.org release schedule announcement).
