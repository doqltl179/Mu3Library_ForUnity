from __future__ import annotations

import os
import platform
import subprocess
from dataclasses import dataclass
from pathlib import Path

import typer

from mu3_common.checkout import echo_repository_root, outside_checkout_refusal, repo_root
from mu3_common.editor import UnityTargetSpec, load_target_specs
from mu3_common.sample_links import is_directory_link, link_destination, sample_link_state


app = typer.Typer(
    add_completion=False,
    no_args_is_help=True,
    help="Links from the development projects to their packages' Samples~ folders.",
)


def create_directory_link(link_path: Path, target: Path) -> None:
    """Link link_path to the target folder.

    Windows gets a junction, which needs neither administrator rights nor Developer Mode and
    takes an absolute target. Elsewhere a relative symbolic link keeps working after the
    checkout moves.
    """
    if platform.system() == "Windows":
        completed = subprocess.run(
            ["cmd", "/d", "/c", "mklink", "/J", str(link_path), str(target)],
            capture_output=True,
            text=True,
            errors="replace",
            check=False,
        )
        if completed.returncode != 0:
            raise OSError(f"mklink /J failed: {(completed.stderr or completed.stdout).strip()}")
        return

    os.symlink(os.path.relpath(target, link_path.parent), link_path, target_is_directory=True)


def remove_directory_link(link_path: Path) -> None:
    """Remove the link itself; neither call follows it into the folder it points at."""
    if platform.system() == "Windows":
        os.rmdir(link_path)
    else:
        os.unlink(link_path)


@dataclass(frozen=True)
class SampleLinkResult:
    target: str
    ok: bool
    message: str


def link_samples(specs: dict[str, UnityTargetSpec], root: Path) -> list[SampleLinkResult]:
    """Create each target's samples link, leave a correct one alone, and replace a link that
    points elsewhere. A real folder or file in the link's place is never touched."""
    results: list[SampleLinkResult] = []
    for spec in specs.values():
        current = sample_link_state(spec, root)
        if current is None:
            continue

        link_path = root / spec.project_path / spec.samples_link
        # A junction stores its target as given, so it must not keep a subst drive or a relative path.
        samples_path = (root / spec.samples_path).resolve()
        link_label = (spec.project_path / spec.samples_link).as_posix()
        samples_label = spec.samples_path.as_posix()

        if current.state == "linked":
            results.append(SampleLinkResult(spec.key, True, f"Already linked: {link_label} -> {samples_label}."))
            continue
        if current.state in ("blocked", "no-samples"):
            suggestion = (
                " Move it out of the way, then run this command again; it was left untouched."
                if current.state == "blocked"
                else ""
            )
            results.append(SampleLinkResult(spec.key, False, f"error: {current.detail}{suggestion}"))
            continue
        if not link_path.parent.is_dir():
            results.append(
                SampleLinkResult(spec.key, False, f"error: {link_path.parent.relative_to(root).as_posix()} is missing.")
            )
            continue

        try:
            if current.state == "misdirected":
                remove_directory_link(link_path)
            create_directory_link(link_path, samples_path)
        except OSError as error:
            results.append(SampleLinkResult(spec.key, False, f"error: could not link {link_label}: {error}"))
            continue

        verb = "Relinked" if current.state == "misdirected" else "Linked"
        results.append(SampleLinkResult(spec.key, True, f"{verb} {link_label} -> {samples_label}."))
    return results


def unlink_samples(specs: dict[str, UnityTargetSpec], root: Path) -> list[SampleLinkResult]:
    """Remove each target's samples link, whatever it points at, and nothing else.

    On Windows `git worktree remove` stops at the first junction it meets and leaves the
    worktree's folder behind, so the links go first. Only the link itself is removed, never the
    folder it points at, and a real folder or file in the link's place is reported and left
    untouched. A target without a link is reported as not linked, so the command can run again.
    """
    results: list[SampleLinkResult] = []
    for spec in specs.values():
        if spec.samples_link is None:
            continue

        link_path = root / spec.project_path / spec.samples_link
        link_label = (spec.project_path / spec.samples_link).as_posix()

        if is_directory_link(link_path):
            samples_path = root / spec.samples_path
            if link_path.exists() and samples_path.is_dir() and os.path.samefile(link_path, samples_path):
                destination = spec.samples_path.as_posix()
            else:
                destination = link_destination(link_path)
            try:
                remove_directory_link(link_path)
            except OSError as error:
                message = f"error: could not remove the link {link_label}: {error}"
                results.append(SampleLinkResult(spec.key, False, message))
                continue
            results.append(SampleLinkResult(spec.key, True, f"Unlinked {link_label}, which pointed at {destination}."))
            continue
        if os.path.lexists(link_path):
            # is_directory_link accepts only symbolic links and junctions, so this also covers
            # other reparse points, such as a cloud-synced folder.
            message = (
                f"error: {link_label} is not a sample link (a real folder, file, or other reparse point);"
                " it was left untouched."
            )
            results.append(SampleLinkResult(spec.key, False, message))
            continue
        results.append(SampleLinkResult(spec.key, True, f"Not linked: {link_label} does not exist."))
    return results


def refuse_outside_checkout(root: Path, command: str) -> None:
    """Stop with exit 2, before anything changes, when the working directory is outside the checkout at root."""
    refusal = outside_checkout_refusal(root, "mu3_samples", command)
    if refusal is not None:
        typer.echo(f"error: {refusal}", err=True)
        raise typer.Exit(code=2)


@app.command("link")
def samples_link(
    target: str | None = typer.Option(None, "--target", "-t", help="Optional configured target to link."),
) -> None:
    """Link each development project's Assets folder to its package's Samples~ folder.

    A correct link is left alone, a link that points elsewhere is replaced, and a real folder
    in the link's place stops the command without being touched.
    """
    root = repo_root()
    echo_repository_root(root)
    refuse_outside_checkout(root, "link")
    try:
        specs = load_target_specs(root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if target is not None:
        if target not in specs:
            raise typer.BadParameter(f"Unknown Unity target '{target}'. Choose one of: {', '.join(specs)}.")
        specs = {target: specs[target]}

    results = link_samples(specs, root)
    for result in results:
        typer.echo(f"[{result.target}] {result.message}", err=not result.ok)
    if not results:
        typer.echo("No configured target has a samples link.")
    if not all(result.ok for result in results):
        raise typer.Exit(code=1)


@app.command("unlink")
def samples_unlink(
    target: str | None = typer.Option(None, "--target", "-t", help="Optional configured target to unlink."),
) -> None:
    """Remove the samples links that link created, before git worktree remove deletes a worktree.

    Only each link is removed, never the Samples~ folder it points at. A real folder or file in
    the link's place is reported and left untouched, and the command then exits with 1.
    Run link to restore the links.
    """
    root = repo_root()
    echo_repository_root(root)
    refuse_outside_checkout(root, "unlink")
    try:
        specs = load_target_specs(root)
    except ValueError as error:
        raise typer.BadParameter(str(error)) from error

    if target is not None:
        if target not in specs:
            raise typer.BadParameter(f"Unknown Unity target '{target}'. Choose one of: {', '.join(specs)}.")
        specs = {target: specs[target]}

    results = unlink_samples(specs, root)
    for result in results:
        typer.echo(f"[{result.target}] {result.message}", err=not result.ok)
    if not results:
        typer.echo("No configured target has a samples link.")
    if not all(result.ok for result in results):
        raise typer.Exit(code=1)
