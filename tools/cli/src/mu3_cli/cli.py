from __future__ import annotations

import typer

from mu3_cli.repository import (
    agentkit_cli_path,
    repo_root,
    repository_hygiene_issues,
    run_agentkit_check,
)
from mu3_cli.unity import unity_app


app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Auxiliary repository CLI for Mu3Library tooling and Unity automation workflows.",
)
repo_app = typer.Typer(no_args_is_help=True, help="Repository discovery commands.")
app.add_typer(repo_app, name="repo")
app.add_typer(unity_app, name="unity")


@repo_app.command("info")
def repo_info() -> None:
    """Print key repository roots and agent document locations."""
    root = repo_root()

    typer.echo(f"Repository root: {root}")
    typer.echo(f"Base package: {root / 'Mu3Library_Base'}")
    typer.echo(f"URP package: {root / 'Mu3Library_URP'}")
    typer.echo(f"Agent kit: {root / '.ai' / 'kit'}")
    typer.echo(f"Agent overlay: {root / '.ai' / 'project'}")
    typer.echo(f"CLI tooling: {root / 'tools' / 'cli'}")


@repo_app.command("check")
def repo_check() -> None:
    """Validate repository hygiene, then run the agentkit check on the agent docs."""
    issues = repository_hygiene_issues()

    if issues:
        typer.echo("Invalid repository hygiene:")
        for issue in issues:
            typer.echo(f"- {issue}")
        raise typer.Exit(code=1)

    typer.echo("Repository document layout, README links, routing references, and tooling artifacts are valid.")

    root = repo_root()
    if not agentkit_cli_path(root).is_file():
        typer.echo(f"agentkit is not installed: {agentkit_cli_path(root).relative_to(root)} is missing")
        raise typer.Exit(code=1)

    exit_code = run_agentkit_check(root)
    if exit_code != 0:
        raise typer.Exit(code=exit_code)
