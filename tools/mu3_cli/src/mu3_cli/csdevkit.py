from __future__ import annotations

import json
import shutil
import subprocess
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import typer

from mu3_cli.repository import repo_root


csdevkit_app = typer.Typer(no_args_is_help=True, help="C# Dev Kit workflow helpers.")
context_app = typer.Typer(no_args_is_help=True, help="Built-In and URP context helpers.")
doctor_app = typer.Typer(no_args_is_help=True, help="Project and solution health checks.")
build_app = typer.Typer(no_args_is_help=True, help="Context-aware compile-only target profiles.")
logs_app = typer.Typer(no_args_is_help=True, help="Diagnostic capture helpers for C# Dev Kit support.")
drift_app = typer.Typer(no_args_is_help=True, help="Detect context, solution, and dependency drift.")
csdevkit_app.add_typer(context_app, name="context")
csdevkit_app.add_typer(doctor_app, name="doctor")
csdevkit_app.add_typer(build_app, name="build-profile")
csdevkit_app.add_typer(logs_app, name="logs")
csdevkit_app.add_typer(drift_app, name="drift")


@dataclass(frozen=True)
class ContextSpec:
    key: str
    display_name: str
    role: str
    workspace_file: Path
    default_solution: Path
    project_root: Path
    package_name: str
    package_path: Path
    is_default: bool
    summary: str
    expected_projects: tuple[str, ...]
    known_solutions: tuple[str, ...]


@dataclass(frozen=True)
class DiagnosticResult:
    status: str
    title: str
    detail: str
    suggestion: str | None = None


@dataclass(frozen=True)
class BuildProfile:
    key: str
    context_key: str
    description: str
    targets: tuple[Path, ...]


def _relative_path(root: Path, relative: str) -> Path:
    return root / Path(relative)


def context_specs(root: Path | None = None) -> dict[str, ContextSpec]:
    root = root or repo_root()

    return {
        "built-in": ContextSpec(
            key="built-in",
            display_name="Built-In",
            role="default",
            workspace_file=_relative_path(root, "UnityProject_BuiltIn/Mu3Library_ForUnity.code-workspace"),
            default_solution=_relative_path(root, "UnityProject_BuiltIn/UnityProject_BuiltIn.slnx"),
            project_root=_relative_path(root, "UnityProject_BuiltIn"),
            package_name="com.github.doqltl179.mu3library.base",
            package_path=_relative_path(root, "Mu3Library_Base"),
            is_default=True,
            summary="Primary development context for the Base package and Built-In sample project.",
            expected_projects=(
                "Assembly-CSharp.csproj",
                "Assembly-CSharp-Editor.csproj",
                "Mu3Library.csproj",
                "Mu3Library.DI.csproj",
                "Mu3Library.Editor.csproj",
                "Mu3Library.Sample.Template.csproj",
            ),
            known_solutions=("UnityProject_BuiltIn.slnx",),
        ),
        "urp": ContextSpec(
            key="urp",
            display_name="URP",
            role="additional",
            workspace_file=_relative_path(root, "UnityProject_URP/Mu3Library_ForUnity.code-workspace"),
            default_solution=_relative_path(root, "UnityProject_URP/UnityProject_URP.slnx"),
            project_root=_relative_path(root, "UnityProject_URP"),
            package_name="com.github.doqltl179.mu3library.urp",
            package_path=_relative_path(root, "Mu3Library_URP"),
            is_default=False,
            summary="Additional development context for the URP package layered on top of the Base package.",
            expected_projects=(
                "Assembly-CSharp.csproj",
                "Assembly-CSharp-Editor.csproj",
                "Mu3Library.csproj",
                "Mu3Library.DI.csproj",
                "Mu3Library.Editor.csproj",
                "Mu3Library.Sample.Template.csproj",
                "Mu3Library.URP.csproj",
                "Mu3Library.URP.Sample.ScreenEffect.csproj",
            ),
            known_solutions=("UnityProject_URP.slnx", "Mu3Library_ForUnity_URP.slnx"),
        ),
    }


def _state_file(root: Path) -> Path:
    return root / "log" / "mu3_cli" / "csdevkit-state.json"


def _bundle_root(root: Path) -> Path:
    return root / "log" / "mu3_cli" / "csdevkit"


def _read_state(root: Path) -> dict[str, str]:
    state_file = _state_file(root)
    if not state_file.exists():
        return {}

    return json.loads(state_file.read_text(encoding="utf-8"))


def default_context_key(root: Path | None = None) -> str:
    root = root or repo_root()
    for key, spec in context_specs(root).items():
        if spec.is_default:
            return key

    raise RuntimeError("No default C# Dev Kit context is configured.")


def current_context_key(root: Path | None = None) -> str:
    root = root or repo_root()
    state = _read_state(root)
    candidate = state.get("active_context")
    specs = context_specs(root)

    if candidate in specs:
        return candidate

    return default_context_key(root)


def resolve_context(context_key: str | None, root: Path | None = None) -> ContextSpec:
    root = root or repo_root()
    specs = context_specs(root)
    selected_key = context_key or current_context_key(root)

    if selected_key not in specs:
        allowed = ", ".join(specs.keys())
        raise typer.BadParameter(f"Unknown context '{selected_key}'. Choose one of: {allowed}.")

    return specs[selected_key]


def save_context_key(context_key: str, root: Path | None = None) -> ContextSpec:
    root = root or repo_root()
    spec = resolve_context(context_key, root)
    state_file = _state_file(root)
    state_file.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "active_context": spec.key,
        "updated_at": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
    }
    state_file.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return spec


def _display_path(path: Path, root: Path) -> str:
    return str(path.relative_to(root))


def build_profiles(spec: ContextSpec) -> dict[str, BuildProfile]:
    package_surface = (
        spec.project_root / "Mu3Library.csproj",
        spec.project_root / "Mu3Library.DI.csproj",
        spec.project_root / "Mu3Library.Editor.csproj",
    )
    consumer_shell = (
        spec.project_root / "Assembly-CSharp.csproj",
        spec.project_root / "Assembly-CSharp-Editor.csproj",
    )

    if spec.key == "built-in":
        samples = (spec.project_root / "Mu3Library.Sample.Template.csproj",)
    else:
        samples = (
            spec.project_root / "Mu3Library.Sample.Template.csproj",
            spec.project_root / "Mu3Library.URP.Sample.ScreenEffect.csproj",
        )

    profiles = {
        "default-solution": BuildProfile(
            key="default-solution",
            context_key=spec.key,
            description="Build the default solution selected for this C# Dev Kit context.",
            targets=(spec.default_solution,),
        ),
        "package-surface": BuildProfile(
            key="package-surface",
            context_key=spec.key,
            description="Build the package-facing Mu3Library assemblies for the active context.",
            targets=package_surface,
        ),
        "samples": BuildProfile(
            key="samples",
            context_key=spec.key,
            description="Build sample-related assemblies for the active context.",
            targets=samples,
        ),
        "consumer-shell": BuildProfile(
            key="consumer-shell",
            context_key=spec.key,
            description="Build the generated consumer shell projects that load the package inside the Unity project.",
            targets=consumer_shell,
        ),
    }

    if spec.key == "urp":
        profiles["urp-additional"] = BuildProfile(
            key="urp-additional",
            context_key=spec.key,
            description="Build the URP-only package extension assembly.",
            targets=(spec.project_root / "Mu3Library.URP.csproj",),
        )

    return profiles


def resolve_build_profile(spec: ContextSpec, profile_key: str) -> BuildProfile:
    profiles = build_profiles(spec)
    if profile_key not in profiles:
        allowed = ", ".join(profiles.keys())
        raise typer.BadParameter(f"Unknown build profile '{profile_key}'. Choose one of: {allowed}.")

    return profiles[profile_key]


def _echo_build_profile(profile: BuildProfile, root: Path) -> None:
    typer.echo(f"Profile: {profile.key}")
    typer.echo(f"Context: {profile.context_key}")
    typer.echo(f"Description: {profile.description}")
    typer.echo("Targets:")
    for target in profile.targets:
        typer.echo(f"- {_display_path(target, root)}")


def run_build_profile(
    profile: BuildProfile,
    configuration: str,
    no_restore: bool,
    root: Path | None = None,
) -> None:
    root = root or repo_root()
    dotnet_path = shutil.which("dotnet")

    if not dotnet_path:
        raise typer.BadParameter("dotnet was not found on PATH. Install the .NET SDK before running build profiles.")

    for target in profile.targets:
        command = [dotnet_path, "build", str(target), "--configuration", configuration, "--nologo", "--verbosity", "minimal"]
        if no_restore:
            command.append("--no-restore")

        typer.echo(f"Running: {' '.join(command)}")
        completed = subprocess.run(command, cwd=root, check=False)
        if completed.returncode != 0:
            raise typer.Exit(code=completed.returncode)


def _echo_context(spec: ContextSpec, root: Path, active_key: str) -> None:
    typer.echo(f"Context: {spec.key}")
    typer.echo(f"Display name: {spec.display_name}")
    typer.echo(f"Role: {spec.role}")
    typer.echo(f"Active: {'yes' if spec.key == active_key else 'no'}")
    typer.echo(f"Workspace file: {_display_path(spec.workspace_file, root)}")
    typer.echo(f"Default solution: {_display_path(spec.default_solution, root)}")
    typer.echo(f"Project root: {_display_path(spec.project_root, root)}")
    typer.echo(f"Package path: {_display_path(spec.package_path, root)}")
    typer.echo(f"Package name: {spec.package_name}")
    typer.echo(f"Summary: {spec.summary}")


def _workspace_default_solution(spec: ContextSpec) -> str | None:
    workspace = json.loads(spec.workspace_file.read_text(encoding="utf-8-sig"))
    settings = workspace.get("settings", {})
    value = settings.get("dotnet.defaultSolution")
    return value if isinstance(value, str) else None


def _package_manifest(path: Path) -> dict[str, object]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def _diagnostic(status: str, title: str, detail: str, suggestion: str | None = None) -> DiagnosticResult:
    return DiagnosticResult(status=status, title=title, detail=detail, suggestion=suggestion)


def _check_workspace_file(spec: ContextSpec, root: Path) -> DiagnosticResult:
    if spec.workspace_file.exists():
        return _diagnostic("PASS", "Workspace file", f"Found {_display_path(spec.workspace_file, root)}.")

    return _diagnostic(
        "FAIL",
        "Workspace file",
        f"Missing {_display_path(spec.workspace_file, root)}.",
        "Restore or recreate the tracked workspace file before relying on C# Dev Kit context switching.",
    )


def _check_workspace_default_solution(spec: ContextSpec, root: Path) -> DiagnosticResult:
    if not spec.workspace_file.exists():
        return _diagnostic("FAIL", "Workspace default solution", "Cannot inspect the workspace file because it is missing.")

    configured = _workspace_default_solution(spec)
    expected = _display_path(spec.default_solution, root).replace("\\", "/")

    if configured == expected:
        return _diagnostic("PASS", "Workspace default solution", f"dotnet.defaultSolution points to {configured}.")

    if configured is None:
        return _diagnostic(
            "FAIL",
            "Workspace default solution",
            "dotnet.defaultSolution is not configured.",
            "Set the workspace default solution so C# Dev Kit does not have to guess in a multi-solution workspace.",
        )

    return _diagnostic(
        "FAIL",
        "Workspace default solution",
        f"Configured value '{configured}' does not match expected '{expected}'.",
        "Align the workspace file with the intended Built-In or URP default solution.",
    )


def _check_default_solution(spec: ContextSpec, root: Path) -> DiagnosticResult:
    if spec.default_solution.exists():
        return _diagnostic("PASS", "Default solution", f"Found {_display_path(spec.default_solution, root)}.")

    return _diagnostic(
        "FAIL",
        "Default solution",
        f"Missing {_display_path(spec.default_solution, root)}.",
        "Regenerate Unity project files or reopen the project in Unity to restore the generated solution.",
    )


def _check_solution_ambiguity(spec: ContextSpec, root: Path) -> DiagnosticResult:
    known_paths = [spec.project_root / relative for relative in spec.known_solutions]
    existing = [path for path in known_paths if path.exists()]

    if len(existing) <= 1:
        return _diagnostic("PASS", "Solution ambiguity", f"Detected {len(existing)} known solution file(s) for this context.")

    return _diagnostic(
        "PASS",
        "Solution ambiguity",
        "Multiple solution files are present, but the workspace default solution is configured.",
    )


def _check_generated_projects(spec: ContextSpec, root: Path) -> DiagnosticResult:
    missing = [relative for relative in spec.expected_projects if not (spec.project_root / relative).exists()]

    if not missing:
        return _diagnostic(
            "PASS",
            "Generated projects",
            f"Found all {len(spec.expected_projects)} expected generated project files.",
        )

    missing_text = ", ".join(missing)
    return _diagnostic(
        "FAIL",
        "Generated projects",
        f"Missing generated project files: {missing_text}.",
        "Regenerate Unity project files before expecting C# Dev Kit to load or build this context reliably.",
    )


def _check_package_manifest(spec: ContextSpec, root: Path) -> DiagnosticResult:
    manifest_path = spec.package_path / "package.json"
    if manifest_path.exists():
        return _diagnostic("PASS", "Package manifest", f"Found {_display_path(manifest_path, root)}.")

    return _diagnostic(
        "FAIL",
        "Package manifest",
        f"Missing {_display_path(manifest_path, root)}.",
        "Restore the package root before relying on this context.",
    )


def _check_dotnet_sdk() -> DiagnosticResult:
    dotnet_path = shutil.which("dotnet")
    if not dotnet_path:
        return _diagnostic(
            "WARN",
            ".NET SDK",
            "dotnet was not found on PATH.",
            "Install a supported .NET SDK or open the workspace in an environment where C# Dev Kit can resolve dotnet.",
        )

    version = subprocess.run(
        [dotnet_path, "--version"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if version.returncode == 0:
        return _diagnostic("PASS", ".NET SDK", f"dotnet is available ({version.stdout.strip()}).")

    return _diagnostic(
        "WARN",
        ".NET SDK",
        "dotnet is present on PATH but its version could not be resolved cleanly.",
        "Run 'dotnet --info' manually to inspect the local SDK installation.",
    )


def run_load_diagnostics(spec: ContextSpec, root: Path | None = None) -> list[DiagnosticResult]:
    root = root or repo_root()
    return [
        _check_workspace_file(spec, root),
        _check_workspace_default_solution(spec, root),
        _check_default_solution(spec, root),
        _check_solution_ambiguity(spec, root),
        _check_generated_projects(spec, root),
        _check_package_manifest(spec, root),
        _check_dotnet_sdk(),
    ]


def _render_diagnostics(results: list[DiagnosticResult]) -> tuple[int, int, int]:
    pass_count = 0
    warn_count = 0
    fail_count = 0

    for result in results:
        typer.echo(f"[{result.status}] {result.title}: {result.detail}")
        if result.suggestion:
            typer.echo(f"  Suggestion: {result.suggestion}")

        if result.status == "PASS":
            pass_count += 1
        elif result.status == "WARN":
            warn_count += 1
        else:
            fail_count += 1

    return pass_count, warn_count, fail_count


def _diagnostic_payload(result: DiagnosticResult) -> dict[str, str | None]:
    return {
        "status": result.status,
        "title": result.title,
        "detail": result.detail,
        "suggestion": result.suggestion,
    }


def _build_profile_payload(profile: BuildProfile, root: Path) -> dict[str, object]:
    return {
        "key": profile.key,
        "context": profile.context_key,
        "description": profile.description,
        "targets": [_display_path(target, root) for target in profile.targets],
    }


def _bundle_timestamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def create_logs_bundle(
    spec: ContextSpec,
    include_dotnet_info: bool,
    root: Path | None = None,
) -> Path:
    root = root or repo_root()
    bundle_dir = _bundle_root(root) / f"{_bundle_timestamp()}-{spec.key}"
    bundle_dir.mkdir(parents=True, exist_ok=True)

    diagnostics = run_load_diagnostics(spec, root)
    pass_count, warn_count, fail_count = _render_diagnostics(diagnostics)

    summary_lines = [
        "# C# Dev Kit Support Bundle",
        "",
        f"- Generated at: {datetime.now(timezone.utc).replace(microsecond=0).isoformat()}",
        f"- Context: {spec.key}",
        f"- Workspace file: {_display_path(spec.workspace_file, root)}",
        f"- Default solution: {_display_path(spec.default_solution, root)}",
        f"- Package manifest: {_display_path(spec.package_path / 'package.json', root)}",
        f"- Diagnostic summary: {pass_count} pass, {warn_count} warn, {fail_count} fail",
        "",
        "## Pair With VS Code",
        "",
        "1. Open the Command Palette in VS Code.",
        "2. Run `Collect C# Dev Kit Logs`.",
        "3. Attach the VS Code-generated ZIP together with this bundle directory when reporting the issue.",
        "",
        "## Included Files",
        "",
        "- `diagnostics.json`: Structured load diagnostics for the selected context.",
        "- `build-profiles.json`: Compile-only build profiles for the selected context.",
        "- `workspace.code-workspace`: Snapshot of the selected tracked workspace file.",
        "- `package.json`: Snapshot of the selected package manifest.",
    ]

    if _state_file(root).exists():
        summary_lines.append("- `csdevkit-state.json`: Local active-context state used by `mu3-cli`.")

    if include_dotnet_info:
        summary_lines.append("- `dotnet-info.txt`: Output from `dotnet --info` when available.")

    (bundle_dir / "summary.md").write_text("\n".join(summary_lines) + "\n", encoding="utf-8")
    (bundle_dir / "diagnostics.json").write_text(
        json.dumps([_diagnostic_payload(result) for result in diagnostics], indent=2),
        encoding="utf-8",
    )
    (bundle_dir / "build-profiles.json").write_text(
        json.dumps(
            [_build_profile_payload(profile, root) for profile in build_profiles(spec).values()],
            indent=2,
        ),
        encoding="utf-8",
    )

    shutil.copy2(spec.workspace_file, bundle_dir / "workspace.code-workspace")
    shutil.copy2(spec.package_path / "package.json", bundle_dir / "package.json")

    if _state_file(root).exists():
        shutil.copy2(_state_file(root), bundle_dir / "csdevkit-state.json")

    if include_dotnet_info:
        dotnet_path = shutil.which("dotnet")
        if dotnet_path:
            info = subprocess.run(
                [dotnet_path, "--info"],
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                check=False,
            )
            (bundle_dir / "dotnet-info.txt").write_text(info.stdout or info.stderr, encoding="utf-8")

    return bundle_dir


def run_drift_checks(root: Path | None = None) -> list[DiagnosticResult]:
    root = root or repo_root()
    specs = context_specs(root)
    results: list[DiagnosticResult] = []

    default_key = default_context_key(root)
    active_key = current_context_key(root)
    if active_key == default_key:
        results.append(
            _diagnostic(
                "PASS",
                "Active context override",
                f"Local active context matches the repository default ({default_key}).",
            )
        )
    else:
        results.append(
            _diagnostic(
                "WARN",
                "Active context override",
                f"Local active context is '{active_key}' while the repository default is '{default_key}'.",
                "This is valid for URP-focused work, but remember that Built-In remains the default baseline for shared maintenance.",
            )
        )

    base_manifest_path = root / "Mu3Library_Base" / "package.json"
    urp_manifest_path = root / "Mu3Library_URP" / "package.json"
    base_manifest = _package_manifest(base_manifest_path)
    urp_manifest = _package_manifest(urp_manifest_path)
    base_version = str(base_manifest.get("version", ""))
    urp_dependencies = urp_manifest.get("dependencies", {})

    if isinstance(urp_dependencies, dict) and urp_dependencies.get("com.github.doqltl179.mu3library.base") == base_version:
        results.append(
            _diagnostic(
                "PASS",
                "Base/URP package alignment",
                f"URP package depends on Base version {base_version}.",
            )
        )
    else:
        results.append(
            _diagnostic(
                "FAIL",
                "Base/URP package alignment",
                "URP package dependency on the Base package does not match the current Base package version.",
                "Align Mu3Library_URP/package.json with Mu3Library_Base/package.json before release or compile verification.",
            )
        )

    for spec in specs.values():
        results.append(_check_workspace_default_solution(spec, root))

        missing_targets: list[str] = []
        for profile in build_profiles(spec).values():
            for target in profile.targets:
                if not target.exists():
                    missing_targets.append(_display_path(target, root))

        if missing_targets:
            results.append(
                _diagnostic(
                    "FAIL",
                    f"Build profile targets ({spec.key})",
                    f"Missing targets referenced by build profiles: {', '.join(missing_targets)}.",
                    "Regenerate Unity project files or update the curated build profiles to match the current generated project set.",
                )
            )
        else:
            results.append(
                _diagnostic(
                    "PASS",
                    f"Build profile targets ({spec.key})",
                    f"All curated build profile targets exist for the {spec.key} context.",
                )
            )

    return results


@context_app.command("list")
def context_list() -> None:
    """List the available C# Dev Kit contexts for this repository."""
    root = repo_root()
    active_key = current_context_key(root)

    for key, spec in context_specs(root).items():
        marker = "*" if key == active_key else "-"
        default_label = "default" if spec.is_default else spec.role
        typer.echo(f"{marker} {key}: {spec.display_name} ({default_label})")


@context_app.command("show")
def context_show(context: str | None = typer.Argument(None, help="Optional context key to inspect.")) -> None:
    """Show the current or requested C# Dev Kit context."""
    root = repo_root()
    active_key = current_context_key(root)
    spec = resolve_context(context, root)
    _echo_context(spec, root, active_key)


@context_app.command("use")
def context_use(context: str = typer.Argument(..., help="Context key to persist locally.")) -> None:
    """Persist the active C# Dev Kit context under log/mu3_cli/."""
    root = repo_root()
    spec = save_context_key(context, root)
    typer.echo(
        f"Active context set to {spec.key}. Local state written to {_display_path(_state_file(root), root)}."
    )
    _echo_context(spec, root, spec.key)


@doctor_app.command("load")
def doctor_load(
    context: str | None = typer.Option(None, "--context", "-c", help="Optional context key to inspect."),
) -> None:
    """Run C# Dev Kit-oriented workspace and project-load diagnostics."""
    root = repo_root()
    spec = resolve_context(context, root)
    typer.echo(f"C# Dev Kit load diagnostics for context: {spec.key}")
    pass_count, warn_count, fail_count = _render_diagnostics(run_load_diagnostics(spec, root))
    typer.echo(f"Summary: {pass_count} pass, {warn_count} warn, {fail_count} fail")

    if fail_count:
        raise typer.Exit(code=1)


@build_app.command("list")
def build_list(
    context: str | None = typer.Option(None, "--context", "-c", help="Optional context key to inspect."),
) -> None:
    """List the available compile-only build profiles for a context."""
    root = repo_root()
    spec = resolve_context(context, root)
    typer.echo(f"Build profiles for context: {spec.key}")
    for profile in build_profiles(spec).values():
        typer.echo(f"- {profile.key}: {profile.description}")


@build_app.command("show")
def build_show(
    profile: str = typer.Argument(..., help="Build profile key to inspect."),
    context: str | None = typer.Option(None, "--context", "-c", help="Optional context key to inspect."),
) -> None:
    """Show the targets behind a compile-only build profile."""
    root = repo_root()
    spec = resolve_context(context, root)
    _echo_build_profile(resolve_build_profile(spec, profile), root)


@build_app.command("run")
def build_run(
    profile: str = typer.Argument(..., help="Build profile key to execute."),
    context: str | None = typer.Option(None, "--context", "-c", help="Optional context key to inspect."),
    configuration: str = typer.Option("Debug", "--configuration", help="dotnet build configuration to use."),
    no_restore: bool = typer.Option(False, "--no-restore", help="Pass --no-restore to dotnet build."),
) -> None:
    """Run dotnet build for a compile-only build profile."""
    root = repo_root()
    spec = resolve_context(context, root)
    selected_profile = resolve_build_profile(spec, profile)
    _echo_build_profile(selected_profile, root)
    run_build_profile(selected_profile, configuration, no_restore, root)


@logs_app.command("guide")
def logs_guide() -> None:
    """Print the recommended C# Dev Kit log collection flow for this repository."""
    typer.echo("C# Dev Kit log collection flow:")
    typer.echo("1. Open the Command Palette in VS Code.")
    typer.echo("2. Run 'Collect C# Dev Kit Logs'.")
    typer.echo("3. Choose either 'Collect existing logs' or 'Record a new trace' as needed.")
    typer.echo("4. Run 'mu3-cli csdevkit logs bundle' for the relevant Built-In or URP context.")
    typer.echo("5. Attach both the VS Code-generated ZIP and the local bundle under log/mu3_cli/csdevkit/.")


@logs_app.command("bundle")
def logs_bundle(
    context: str | None = typer.Option(None, "--context", "-c", help="Optional context key to inspect."),
    include_dotnet_info: bool = typer.Option(True, "--include-dotnet-info/--no-dotnet-info", help="Include dotnet --info output when dotnet is available."),
) -> None:
    """Create a repo-local support bundle to pair with C# Dev Kit logs."""
    root = repo_root()
    spec = resolve_context(context, root)
    bundle_dir = create_logs_bundle(spec, include_dotnet_info, root)
    typer.echo(f"Created C# Dev Kit support bundle: {_display_path(bundle_dir, root)}")


@drift_app.command("check")
def drift_check() -> None:
    """Check for context, solution, and dependency drift across the repository."""
    root = repo_root()
    pass_count, warn_count, fail_count = _render_diagnostics(run_drift_checks(root))
    typer.echo(f"Summary: {pass_count} pass, {warn_count} warn, {fail_count} fail")

    if fail_count:
        raise typer.Exit(code=1)