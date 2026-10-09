---
paths:
  - "**/*.cs"
---
<!-- agentkit:generated from .ai/kit/core/stacks/languages/csharp.md, .ai/project/profile.toml. Do not edit: change the source, then run `python .ai/kit/tools/agentkit.py sync`. Paths are relative to the project root. -->

# C#

## Detect
- Unity project (`Assets/`, `ProjectSettings/ProjectVersion.txt`, `*.asmdef`): Unity's own runtime and C# version apply, not the .NET SDK's; follow the `unity` pack.
- SDK version from `global.json` (`sdk.version`, `rollForward`); target frameworks from `<TargetFramework>`/`<TargetFrameworks>` in `*.csproj`.
- Solution file `*.sln` or `*.slnx`; shared settings in `Directory.Build.props`/`.targets`; central package versions in `Directory.Packages.props`.
- Project flags: `<Nullable>`, `<LangVersion>`, `<TreatWarningsAsErrors>`, `<ImplicitUsings>`; analyzer packages and `.editorconfig` severities.
- Test framework from package references: xUnit, NUnit, or MSTest.

## Conventions
- Keep nullable reference types enabled; model absence with `?` and check before use. Use the `!` operator only with a comment proving the value is non-null.
- Async all the way: return `Task`/`ValueTask`, accept and forward a `CancellationToken`, suffix method names with `Async`. Use `async void` only for event handlers.
- Dispose `IDisposable` with `using` declarations or statements; `await using` for `IAsyncDisposable`.
- Use `record` for immutable data; pattern matching and switch expressions for branching on shape.
- Rethrow with `throw;`, not `throw ex;`, to keep the stack trace; catch the narrowest exception type.
- Format and parse machine-readable text with `CultureInfo.InvariantCulture`; compare identifiers with `StringComparison.Ordinal`.
- Store timestamps as UTC `DateTimeOffset`; inject `TimeProvider` for testable time.
- With central package management, put versions in `Directory.Packages.props` and version-less `<PackageReference>` items in projects.
- Follow the existing namespace style (file-scoped or block) and `using` placement.

## Verify
- Build: `commands.build`, or `dotnet build`; analyzers run during build, so add no new warnings.
- Format: `commands.format`, or `dotnet format --verify-no-changes`.
- Tests: `commands.test`, or `dotnet test`; narrow with `--filter "FullyQualifiedName~Namespace.ClassTests"`.

## Pitfalls
- `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on a task deadlocks under a synchronization context and starves the thread pool; `await` instead.
- Enumerating a LINQ `IEnumerable` twice re-runs the query; materialize with `ToList()` when reused.
- Creating and disposing an `HttpClient` per request exhausts sockets; share one instance or use `IHttpClientFactory`.
- `DateTime.Now` in server code is local and ambiguous; use `DateTimeOffset.UtcNow` or the injected `TimeProvider`.
- Mutating a copy of a mutable struct silently drops the change; make structs `readonly`.
- Fire-and-forget tasks lose their exceptions; await them or hand them to an owner that observes failures.
- Editing generated files (`*.g.cs`, `*.Designer.cs`, anything under `obj/`); change the generator input.

## Version Notes
- The default C# version follows the target framework: .NET 11 → C# 15, .NET 10 → C# 14, .NET 9 → C# 13, .NET 8 → C# 12; do not set `<LangVersion>` above the framework's default. Use C# 15 features (for example union types, closed hierarchies, `[with(...)]` collection expression arguments) only in projects that target `net11.0` (as of 2026-10, per learn.microsoft.com language-versioning and What's new in .NET 11).
- Even-numbered .NET releases are LTS (three years) and odd-numbered are STS (two years). .NET 10 is the current LTS, supported until 2028-11-14; .NET 8 and .NET 9 both reach end of support on 2026-11-10 (as of 2026-10, per dotnet.microsoft.com support policy).
- .NET 11 is a go-live release candidate (RC1, September 2026) with general availability expected in November 2026; check `global.json` and `<TargetFramework>` before assuming it (as of 2026-10, per learn.microsoft.com What's new in .NET 11).
- `TimeProvider` is built in from .NET 8; .NET Standard 2.0 and .NET Framework 4.6.2+ targets get it from the `Microsoft.Bcl.TimeProvider` package. Without that package on older targets, inject a clock interface instead (as of 2026-10, per learn.microsoft.com TimeProvider API reference).
