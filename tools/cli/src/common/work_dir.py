from __future__ import annotations

from pathlib import Path


class FolderNotClaimedError(ValueError):
    """A folder holds something, but not the marker file of the tool that would change it."""


def claim_folder(folder: Path, marker_name: str, marker_text: str) -> bool:
    """Make sure a tool may change and delete files in folder, which its marker file marks as its own.

    A folder that does not exist or is empty becomes the tool's own: it gets the marker file. A
    folder with the marker is one the tool made. Any other folder, or a file at that path, raises
    FolderNotClaimedError before anything is written, so a wrong path cannot cost anyone the files
    in it. Returns whether this call wrote the marker.
    """
    marker = folder / marker_name
    if marker.is_file():
        return False
    if folder.exists() and (not folder.is_dir() or any(folder.iterdir())):
        raise FolderNotClaimedError(f"{folder} is not an empty folder and has no {marker_name} marker")
    folder.mkdir(parents=True, exist_ok=True)
    marker.write_text(marker_text, encoding="utf-8")
    return True
