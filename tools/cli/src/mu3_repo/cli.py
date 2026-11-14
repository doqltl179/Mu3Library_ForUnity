from __future__ import annotations

import typer

from mu3_common.checkout import repo_root
from mu3_common.editor import load_target_specs
from mu3_repo.hygiene import agentkit_cli_path, repository_hygiene_issues, run_agentkit_check


app = typer.Typer(add_completion=False, no_args_is_help=True, help="Repository discovery commands.")


@app.command("info")
def repo_info() -> None:
    """Print key repository roots and agent document locations."""
    root = repo_root()

    typer.echo(f"Repository root: {root}")
    # unity-cli-packages.tsv is the one list of packages and the projects that develop them.
    try:
        specs = load_target_specs(root)
    except ValueError as error:
        typer.echo(f"Packages: unreadable mapping ({error})")
    else:
        for spec in specs.values():
            typer.echo(f"Package ({spec.key}): {root / spec.package_path}, developed in {spec.project_path}")
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
