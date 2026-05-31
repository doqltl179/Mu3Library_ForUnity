# Mu3 CLI

`mu3-cli` is an auxiliary Python command-line tool for repository automation.

It is intentionally scoped to tooling-safe roots and should not modify Unity runtime or editor package surfaces unless that is explicitly requested.

## Initial Command Surface

- `repo info`: print key repository roots and framework document locations.
- `agents list`: list registered agent documents under `.github/agents`.
- `agents check`: validate that the primary agent discovery entrypoints exist.
- `agents handoff-template`: print the current handoff packet template from `docs/ai-agents/contracts/handoff-contract.md`.
- `csdevkit context {list,show,use}`: inspect or locally switch between the Built-In default context and the URP additional context.
- `csdevkit doctor load`: run C# Dev Kit-oriented workspace, solution, generated project, and `.NET SDK` health checks.
- `csdevkit build-profile {list,show,run}`: inspect or execute compile-only build profiles mapped to the generated Unity `.slnx` and `.csproj` files.
- `csdevkit logs {guide,bundle}`: print the recommended `Collect C# Dev Kit Logs` flow and create a repo-local support bundle under `log/mu3_cli/csdevkit/`.
- `csdevkit drift check`: detect workspace-default, package-version, build-target, and local-context drift across Base, Built-In, and URP surfaces.

## C# Dev Kit Workflow

- Open `UnityProject_BuiltIn/Mu3Library_ForUnity.code-workspace` first for the default Base and Built-In workflow.
- Open `UnityProject_URP/Mu3Library_ForUnity.code-workspace` only when URP is the primary context you need to inspect or verify.
- Built-In is the default development context for this repository.
- URP is an additional context layered on top of the Base package and should not replace the Built-In baseline for shared maintenance.
- Shared Base files can still have more than one valid project context because both Built-In and URP include them with different define sets.
- Local context state is stored under `log/mu3_cli/csdevkit-state.json`, so switching contexts does not dirty tracked workspace files.
- The tracked workspace files carry the C# Dev Kit extension recommendations and `dotnet.defaultSolution` values that this flow expects.

Recommended command flow:

```powershell
mu3-cli csdevkit context show
mu3-cli csdevkit doctor load
mu3-cli csdevkit build-profile list
mu3-cli csdevkit drift check
```

When you need repo-local support artifacts for a C# Dev Kit issue:

```powershell
mu3-cli csdevkit logs guide
mu3-cli csdevkit logs bundle --context built-in
```

## Pure C# Test Surface

`tools/csdevkit_tests/Mu3Library.CsDevKit.Tests.csproj` is a standalone xUnit project that validates tracked repository metadata without depending on Unity assemblies. It exists so C# Dev Kit can light up Test Explorer and code coverage against a narrow, non-Unity surface.

This test surface targets `net10.0`, so use a `.NET 10 SDK` or newer when running it locally or through C# Dev Kit.

Run it directly with:

```powershell
dotnet test tools/csdevkit_tests/Mu3Library.CsDevKit.Tests.csproj --nologo --verbosity minimal
```

## Environment Bootstrap

Preferred flow with `uv`:

```powershell
cd tools/mu3_cli
uv venv .venv
.\.venv\Scripts\Activate.ps1
uv pip install -e .
mu3-cli --help
```

Portable fallback with standard `venv`:

```powershell
cd tools/mu3_cli
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -e .
mu3-cli --help
```

## Notes

- This package lives under `tools/` so it stays outside Unity package delivery surfaces.
- The command tree is intentionally small. Add new groups only when a workflow becomes stable and reusable.
- The CLI remains tooling-safe: it can write local support artifacts under `log/`, but it does not edit shipped Unity runtime or editor package surfaces.
- Governance policy still lives in the framework docs and instructions.