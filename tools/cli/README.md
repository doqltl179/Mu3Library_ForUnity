# Mu3 CLI

`mu3-cli` is an auxiliary Python command-line tool for repository automation.

It is intentionally scoped to tooling-safe roots and should not modify Unity runtime or editor package surfaces unless that is explicitly requested.

## Initial Command Surface

- `repo info`: print key repository roots and framework document locations.
- `repo check`: validate repository document layout, README links, stale routing references, tracked tooling artifacts, and the agent-framework shape.
- `agents list`: list registered agent documents under `.github/agents`.
- `agents check`: validate primary agent discovery entrypoints, context budgets, instruction scope, prompt/skill frontmatter, obsolete docs, and compact role-card shape.
- `agents handoff-template`: print the current handoff packet template from `docs/ai-agents/contracts/handoff-contract.md`.
- `csdevkit context {list,show,use}`: inspect or locally switch between the Built-In default context and the URP additional context.
- `csdevkit doctor load`: run C# Dev Kit-oriented workspace, solution, generated project, and `.NET SDK` health checks.
- `csdevkit build-profile {list,show,run}`: inspect or execute compile-only build profiles mapped to the generated Unity `.slnx` and `.csproj` files.
- `csdevkit logs {guide,bundle}`: print the recommended `Collect C# Dev Kit Logs` flow and create a repo-local support bundle under `log/mu3_cli/csdevkit/`.
- `csdevkit drift check`: detect workspace-default, package-identity, package-version, build-target, and local-context drift across Base, Built-In, and URP surfaces.
- `unity doctor`: diagnose package mappings, Unity projects, required Editors, installed modules, project locks, and log readiness.
- `unity changes`: explain which package projects are selected by the current Git changes or a `--base` ref.
- `unity compile`: invoke the repository's selective Unity batchmode compiler through the shared shell entrypoint.

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

## Environment Bootstrap

Preferred flow with `uv`:

```powershell
cd tools/cli
uv venv .venv
.\.venv\Scripts\Activate.ps1
uv pip install -e .
mu3-cli --help
```

## Verification

Run the dependency-light CLI unit tests from the repository root:

```powershell
$env:PYTHONPATH = "tools/cli/src"
python -m unittest discover -s tools/cli/tests -v
python -m mu3_cli repo check
```

Portable fallback with standard `venv`:

```powershell
cd tools/cli
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

## Planned Unity Automation Surface

Keep `compile-unity.sh` as the dependency-light repository entrypoint while the workflow stabilizes. The `mu3-cli unity` group now orchestrates it, while the shell script remains a compatibility wrapper for CI and dependency-light use.

Recommended command groups:

- `unity doctor`: Editor versions, modules, license/auth state, project locks, package mappings, and writable log paths.
- `unity changes`: changed-file classification, owning packages, selected projects, and optional dependent-package expansion.
- `unity compile`: current change-aware batchmode compilation with configured target keys, `all`, and `--base` controls.
- `unity test`: EditMode/PlayMode execution, XML results, filters, retries, and timeout policy.
- `unity build`: named Build Profiles, platform modules, output directories, and artifact manifests.
- `unity logs`: per-run log directories, warning/error summaries, durations, and reproducible invocation metadata.
- `unity cache`: cache size/status inspection and opt-in cleanup; never purge a Unity `Library` implicitly.

The first three commands are now available:

```powershell
mu3-cli unity doctor --target built-in
mu3-cli unity changes --base origin/develop
mu3-cli unity compile --dry-run
```

The current `unity-cli-packages.tsv` is intentionally minimal: target key, package root, and representative Unity project. Replace it with a richer versioned TOML model only when dependencies, test matrices, or player-build profiles need structured fields. At that point, keep package ownership, dependency edges, Unity version source, test suites, and build profiles in one configuration rather than spreading them across shell conditionals and CI YAML.
