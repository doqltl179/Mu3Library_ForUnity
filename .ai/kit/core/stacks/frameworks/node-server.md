---
id: node-server
title: Node.js Server Frameworks
kind: framework
applies_to: []
related: [typescript, sql, docker]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://nodejs.org/docs/latest/api/", "https://expressjs.com/en/guide/migrating-5.html", "https://fastify.dev/docs/latest/", "https://docs.nestjs.com/", "https://hono.dev/docs/"]
---

# Node.js Server Frameworks

## Detect
- Framework from `package.json` dependencies: `express`, `fastify`, `@nestjs/core`, `hono`, or `koa`. Write in that framework's idiom; do not mix frameworks in one service.
- Runtime and version: `engines`, `.nvmrc`, `.node-version`, or the Dockerfile base image. Bun, Deno, or edge targets (common with Hono) change which Node.js APIs exist.
- How TypeScript runs: `tsc` build output, `tsx`, `ts-node`, or Node.js type stripping.
- Validation library: for example zod, valibot, joi, class-validator (Nest), or TypeBox/JSON Schema (Fastify).
- Logger (for example pino, winston, Nest `Logger`), database client or ORM, and how config is loaded.
- HTTP test style: supertest, `fastify.inject()`, Nest `Test.createTestingModule()`, or Hono `app.request()`.

## Conventions
- Validate params, query, headers, and body at the route boundary with the project's schema library; derive request types from the schema; reject invalid input with 400 without echoing internals.
- Route every error to one handler: Express error middleware (four arguments), Fastify `setErrorHandler`, Nest exception filters, or Hono `app.onError`. Map domain errors to status codes there.
- Load configuration from environment variables once at startup into a validated, typed object; exit with a clear message when a required value is missing. Elsewhere read that object, not `process.env`.
- Log structured entries through the project logger with a request id; never log secrets, tokens, or raw bodies containing personal data.
- Keep the event loop free: no synchronous fs, crypto, or compression calls (`*Sync`) in request paths and no CPU-heavy loops; move such work to `worker_threads` or a job queue, and stream large payloads.
- Create database pools and HTTP clients once per process and reuse them; set timeouts on outbound calls and a size limit on request bodies.
- Shut down gracefully on `SIGTERM` and `SIGINT`: stop accepting connections (`server.close()`, `fastify.close()`, Nest `app.enableShutdownHooks()`), finish in-flight requests, close pools, then exit, under a hard timeout.
- Keep per-request data on the request object or in `AsyncLocalStorage`, never in module-level variables.
- Nest: inject dependencies through constructors; validate DTOs with a global `ValidationPipe` using `whitelist: true`.
- Fastify: register routes as plugins and declare request and response JSON schemas; response schemas also drop unlisted fields.

## Verify
- `commands.test` with HTTP-level tests through the framework's inject helper or supertest; `commands.typecheck`, `commands.lint`.
- `commands.run`, then call the health endpoint and one changed route.

## Pitfalls
- Unhandled promise rejections terminate the process; await every promise or attach a handler.
- Express 4 ignores rejected promises from `async` handlers, so the request hangs; wrap the handler or call `next(err)`.
- Responding twice, or calling `next()` after `res.send()`, throws "headers already sent"; `return` after sending.
- Behind a reverse proxy, `req.ip` and protocol report the proxy unless trusted proxies are configured; set the framework's trust-proxy option to the real topology.
- Catching an error and replying 200 with an error body hides failures from clients and monitoring; return the correct status.
- Calling `process.exit()` in a handler drops in-flight requests; throw, or trigger the shutdown path.

## Version Notes
- Express 5 is the current major: rejected promises from handlers reach error middleware, and route path syntax changed (wildcards must be named, for example `/*splat`) (as of 2026-10, per the expressjs.com migration guide).
- Fastify 5 requires Node.js 20 or later; NestJS 11 uses Express 5 by default; NestJS 12 ships its core packages as ESM and needs Node.js 20.19+ or 22.12+ (as of 2026-10, per fastify.dev and docs.nestjs.com).
- Node.js 20.6+ loads `.env` files with `--env-file` without extra packages (as of 2026-10, per nodejs.org).
