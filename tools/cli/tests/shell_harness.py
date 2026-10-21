"""Runs functions of the repository's shell entry points from the tests."""

from __future__ import annotations

import os
import subprocess
import unittest
from pathlib import Path
from typing import Mapping

from mu3_cli.unity import BashNotFoundError, find_bash


def bash_or_skip() -> Path:
    try:
        return find_bash()
    except BashNotFoundError as error:
        raise unittest.SkipTest(str(error)) from error


def run_sourced(
    script: Path,
    body: str,
    environment: Mapping[str, str] | None = None,
    cwd: Path | None = None,
) -> subprocess.CompletedProcess[str]:
    """Source an entry point, which defines its functions without running it, then run body."""
    return subprocess.run(
        [str(bash_or_skip()), "-c", f'source "$1"\n{body}', "harness", str(script)],
        cwd=cwd or script.parent,
        env=dict(environment if environment is not None else os.environ),
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
    )
