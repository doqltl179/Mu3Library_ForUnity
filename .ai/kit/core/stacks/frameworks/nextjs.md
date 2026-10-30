---
id: nextjs
title: Next.js
kind: framework
applies_to: ["**/app/**/*.tsx", "**/app/**/route.ts", "**/pages/**/*.tsx", "**/next.config.*"]
related: [react, typescript, node-server]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://nextjs.org/docs", "https://nextjs.org/blog", "https://nextjs.org/blog/next-15", "https://nextjs.org/blog/next-16", "https://nextjs.org/blog/next-16-4"]
---

# Next.js

## Detect
- `next` version in `package.json` and `next.config.(js|mjs|ts)`; read the config for `output`, `images`, caching options, and experimental flags.
- Router: `app/` or `src/app/` means App Router; `pages/` or `src/pages/` means Pages Router. Both can coexist during a migration; identify which router owns the route being changed.
- Request interception file at the project root or `src/`: `middleware.ts` or `proxy.ts`.
- Per-route runtime: `export const runtime = "edge"` limits which Node.js APIs are available.
- Deployment target from `output` (`standalone`, `export`); static export disables server-only features.

## Conventions
- App Router files are Server Components by default. Add `"use client"` only to a module that needs state, effects, event handlers, or browser APIs, and keep it on the smallest leaf; every module it imports becomes client code.
- Pass only serializable props from Server to Client Components; pass server-rendered UI as `children` instead of importing server modules into a client file.
- Keep secrets and server-only modules (database clients, `fs`, private keys) out of client code; add `import "server-only"` to modules that must never reach the client bundle.
- Only `NEXT_PUBLIC_*` variables reach the browser, inlined at build time; never give a secret that prefix, and rebuild after changing one.
- Fetch data in async Server Components or a server data-access layer; mutate with Server Actions (`"use server"`) or Route Handlers, then revalidate affected data (`revalidatePath`, `revalidateTag`).
- Treat every Server Action and Route Handler as a public endpoint: authenticate, authorize, and validate input inside it.
- Route Handlers live in `app/**/route.ts` and export HTTP-method functions; a segment cannot hold both `route.ts` and `page.tsx`. Pages Router APIs live in `pages/api/`.
- Use segment files for their purpose: `layout`, `page`, `loading`, `error` (must be a Client Component), `not-found`.
- Use `next/image` with `width`/`height` or `fill` plus `sizes`, allowing remote hosts in `images.remotePatterns`; use `next/font` for fonts and `next/link` for internal navigation.
- Set metadata with `metadata`/`generateMetadata` in the App Router. `next/head`, `getServerSideProps`, `getStaticProps`, and `getStaticPaths` are Pages Router only.

## Verify
- `commands.build` (`next build`) catches server/client boundary, type, and prerender errors; run it after boundary, routing, or config changes.
- `commands.lint`, `commands.test`, and `commands.e2e` for routing and rendering flows.

## Pitfalls
- Adding `"use client"` to a page or layout to fix one hook error moves the whole subtree to the client; extract a small Client Component instead.
- Client Components still render on the server: reading `window`, `localStorage`, `Date.now()`, or random values during render causes hydration mismatches; read them in an effect.
- Fetching the app's own Route Handler from a Server Component adds a network hop and fails during static builds; call the server function directly.
- `redirect()` and `notFound()` work by throwing; do not call them inside a `try` block that swallows the error.
- Reading `cookies()`, `headers()`, or `searchParams` makes a route dynamic; keep them out of layouts meant to be static.
- Copying caching examples across major versions; defaults changed between majors, so check «Version Notes» against the installed version.

## Version Notes
- Next.js 15: `cookies()`, `headers()`, `draftMode()`, `params`, and `searchParams` are async and must be awaited; `fetch` and `GET` Route Handlers are not cached by default; the App Router requires React 19 (as of 2026-10, per nextjs.org/blog/next-15).
- Next.js 16: Turbopack is the default bundler; `middleware.ts` is renamed `proxy.ts`, which runs on the Node.js runtime (`middleware.ts` is deprecated and kept only for Edge use); `next lint` is removed in favor of running the linter directly; explicit caching uses the `"use cache"` directive when `cacheComponents` is enabled (as of 2026-10, per nextjs.org/blog/next-16).
- From Next.js 16.4, `create-next-app` enables `cacheComponents` in new apps; existing apps keep their config value (as of 2026-10, per nextjs.org/blog/next-16-4).
