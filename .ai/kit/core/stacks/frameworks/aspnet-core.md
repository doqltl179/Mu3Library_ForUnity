---
id: aspnet-core
title: ASP.NET Core
kind: framework
applies_to: []
related: [csharp, sql, docker]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://learn.microsoft.com/aspnet/core/", "https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0", "https://learn.microsoft.com/ef/core/", "https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/overview", "https://dotnet.microsoft.com/platform/support/policy/dotnet-core"]
---

# ASP.NET Core

## Detect
- `<TargetFramework>` in `.csproj` files, the SDK pin in `global.json`, and shared settings in `Directory.Build.props` (`Nullable`, `TreatWarningsAsErrors`).
- Hosting: `Program.cs` with `WebApplication.CreateBuilder` (minimal hosting) or `Startup.cs` (older generic host).
- Endpoint style: minimal APIs (`app.MapGet`, `MapGroup`), controllers (`[ApiController]`, `ControllerBase`), Razor Pages, or Blazor. Follow the style of the area being changed.
- Data access: EF Core `DbContext` classes, provider package, and `Migrations/` folder; or a micro-ORM such as Dapper.
- Tests: xUnit, NUnit, or MSTest projects; integration tests with `WebApplicationFactory<Program>`.

## Conventions
- Register services in `Program.cs` or `IServiceCollection` extension methods; inject through constructors (or primary constructors) and minimal API parameters.
- Choose lifetimes deliberately: `DbContext` and per-request state are scoped, stateless helpers transient, thread-safe shared state singleton. A singleton must never capture a scoped service; create a scope with `IServiceScopeFactory` inside singletons and hosted services.
- Get `HttpClient` from `IHttpClientFactory` or typed clients, not `new HttpClient()` per call.
- Bind config sections with the options pattern: `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`; inject `IOptions<T>`, `IOptionsSnapshot<T>` (scoped, per request), or `IOptionsMonitor<T>` (change notifications).
- Keep secrets in user-secrets locally and in environment variables or a vault when deployed, never in `appsettings*.json`.
- Keep middleware in the documented order: exception handling, HSTS, HTTPS redirection, static files, routing, CORS, authentication, authorization, then endpoints.
- Go async all the way: `async Task` endpoints, EF async methods (`ToListAsync`, `SaveChangesAsync`), and a `CancellationToken` passed through.
- EF Core: use `AsNoTracking()` for read-only queries; project to DTOs with `Select`; load relations with `Include` or projections, not lazy loading in loops; call `SaveChangesAsync` once per unit of work.
- Add migrations with `dotnet ef migrations add <Name>` and review the generated code; apply them in deployment with `dotnet ef database update`, SQL scripts, or migration bundles.
- Return errors as `ProblemDetails` (`AddProblemDetails`, `Results.Problem`); use `TypedResults` in minimal APIs.
- Log with `ILogger<T>` message templates (`"Order {OrderId}"`), not interpolated strings.

## Verify
- `commands.build` (`dotnet build`) and `commands.test` (`dotnet test`).
- `commands.format`, or `dotnet format --verify-no-changes` when the project uses it.

## Pitfalls
- `.Result`, `.Wait()`, or `async void` (outside event handlers) cause deadlocks, thread-pool starvation, or lost exceptions; `await` instead.
- Running queries in parallel on one `DbContext` throws because it is not thread-safe; await sequentially or use separate contexts.
- `ToList()` before `Where` or `Select` loads whole tables into memory; keep filtering on the `IQueryable`.
- Editing a migration already applied elsewhere desynchronizes databases; add a new migration.
- `Database.Migrate()` at startup races when several instances start; apply migrations as a deployment step.
- A singleton capturing a scoped service fails only where scope validation runs (Development by default); fix the lifetime, do not disable validation.

## Version Notes
- Even-numbered .NET releases are LTS; .NET 10 (LTS) shipped in November 2025 and is supported until 2028-11-14; .NET 8 (LTS) and .NET 9 (STS) reach end of support on 2026-11-10 (as of 2026-10, per the .NET support policy).
- ASP.NET Core 11 ships with .NET 11, a go-live release candidate (RC1, September 2026) with general availability expected in November 2026. Use ASP.NET Core 11 APIs only when `<TargetFramework>` is `net11.0` (as of 2026-10, per learn.microsoft.com What's new in .NET 11).
- ASP.NET Core 10 adds built-in validation for minimal APIs through `builder.Services.AddValidation()`; earlier versions need endpoint filters or a library (as of 2026-10, per learn.microsoft.com "What's new in ASP.NET Core 10"). Check `<TargetFramework>` before relying on it.
