from __future__ import annotations

import typer

from common.checkout import repo_root
from common.unity_projects import ConfigError, checkout_packages, load_configs
from repo.hygiene import agentkit_cli_path, repository_hygiene_issues, run_agentkit_check


app = typer.Typer(add_completion=False, no_args_is_help=True, help="Repository discovery commands.")


@app.command("info")
def repo_info() -> None:
    """Print key repository roots and agent document locations."""
    root = repo_root()

    typer.echo(f"Repository root: {root}")
    # The configs in tools/unity-projects name the packages each generated Unity project develops.
    try:
        packages = checkout_packages(root)
        configs = load_configs(root)
    except ConfigError as error:
        typer.echo(f"Packages: unreadable project configs ({error})")
    else:
        for name, folder in packages.items():
            developed = ", ".join(config.name for config in configs.values() if name in config.packages) or "no config"
            typer.echo(f"Package {name}: {root / folder}, developed in {developed}")
    typer.echo(f"Agent kit: {root / '.ai' / 'kit'}")
    typer.echo(f"Agent overlay: {root / '.ai' / 'project'}")
    typer.echo(f"CLI tooling: {root / 'tools' / 'cli'}")


@app.command("check")
def repo_check() -> None:
    """Validate repository hygiene, then run the agentkit check on the agent docs."""
    issues = repository_hygiene_issues()

    if issues:
        typer.echo("Invalid repository hygiene:")
        for issue in issues:
            typer.echo(f"- {issue}")
        raise typer.Exit(code=1)

    typer.echo(
        "Repository document layout, README links, routing references, package code characters, "
        "and tooling artifacts are valid."
    )

    root = repo_root()
    if not agentkit_cli_path(root).is_file():
        typer.echo(f"agentkit is not installed: {agentkit_cli_path(root).relative_to(root)} is missing")
        raise typer.Exit(code=1)

    exit_code = run_agentkit_check(root)
    if exit_code != 0:
        raise typer.Exit(code=exit_code)
