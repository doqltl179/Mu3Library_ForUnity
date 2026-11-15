from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path

from mu3_common.editor import UnityTargetSpec


_WINDOWS_REPARSE_TAG_MOUNT_POINT = 0xA0000003
# What creates the links, which compile-unity.sh names too.
LINK_SAMPLES_COMMAND = "PYTHONPATH=tools/cli/src python -m mu3_samples link"


def is_directory_link(path: Path) -> bool:
    """Whether path is a symbolic link or, on Windows, a directory junction, even a dangling one."""
    if path.is_symlink():
        return True
    try:
        status = os.lstat(path)
    except OSError:
        return False
    return getattr(status, "st_reparse_tag", 0) == _WINDOWS_REPARSE_TAG_MOUNT_POINT


def link_destination(path: Path) -> str:
    try:
        destination = os.readlink(path)
    except OSError:
        return "an unreadable destination"
    return destination.removeprefix("\\\\?\\")


@dataclass(frozen=True)
class SampleLinkState:
    """linked, missing, misdirected (a link elsewhere or to nothing), blocked (a real folder or file), or no-samples."""

    state: str
    detail: str


def sample_link_state(spec: UnityTargetSpec, root: Path) -> SampleLinkState | None:
    """Unity skips folders whose name ends in ~, so a project compiles a package's samples only
    through a link from its Assets folder to Samples~. Returns None for a target without one."""
    if spec.samples_link is None:
        return None

    link_label = (spec.project_path / spec.samples_link).as_posix()
    samples_label = spec.samples_path.as_posix()
    link_path = root / spec.project_path / spec.samples_link
    samples_path = root / spec.samples_path

    if not samples_path.is_dir():
        return SampleLinkState("no-samples", f"{samples_label} does not exist.")
    if is_directory_link(link_path):
        if link_path.exists() and os.path.samefile(link_path, samples_path):
            return SampleLinkState("linked", f"{link_label} links to {samples_label}.")
        return SampleLinkState(
            "misdirected", f"{link_label} links to {link_destination(link_path)} instead of {samples_label}."
        )
    if link_path.exists():
        kind = "folder" if link_path.is_dir() else "file"
        return SampleLinkState("blocked", f"{link_label} is a real {kind}, not a link to {samples_label}.")
    return SampleLinkState(
        "missing", f"{link_label} is missing, so the project does not compile the samples in {samples_label}."
    )
