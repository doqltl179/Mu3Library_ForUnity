"""A throwaway repository shaped like this one, for running the real shell entry points."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

from mu3_cli.repository import repo_root
from mu3_cli.unity import bash_script_command
from shell_harness import bash_or_skip, run_sourced

# Mirrors the shape of this repository: one package every project consumes, one
# layered on it, one leaf, a package whose name only extends another one's, and a
# project whose manifest does not reference the package the mapping pairs it with.
MAPPING = (
    ("shared", "PkgShared", "ProjShared"),
    ("layered", "PkgLayered", "ProjLayered"),
    ("leaf", "PkgLeaf", "ProjLeaf"),
    ("extra", "PkgSharedExtra", "ProjExtra"),
    ("orphan", "PkgOrphan", "ProjOrphan"),
)
PROJECT_PACKAGES = {
    "ProjShared": ("PkgShared",),
    "ProjLayered": ("PkgShared", "PkgLayered"),
    "ProjLeaf": ("PkgShared", "PkgLayered", "PkgLeaf"),
    "ProjExtra": ("PkgSharedExtra",),
    "ProjOrphan": (),
}
ENTRY_POINTS = ("compile-unity.sh", "test-unity.sh")

# Stands in for the Unity Editor: names the project it ran, writes the test results it
# is given to the path after -testResults, and exits with the status it is given.
FAKE_UNITY = """#!/usr/bin/env bash
results=""
project=""
while [[ "$#" -gt 0 ]]; do
    case "$1" in
        -testResults) results="$2"; shift ;;
        -projectPath) project="$2"; shift ;;
    esac
    shift
done
printf 'fake unity ran %s\\n' "$project"
if [[ -n "$results" && -n "${FAKE_UNITY_RESULTS:-}" ]]; then
    printf '%s\\n' "$FAKE_UNITY_RESULTS" > "$results"
fi
exit "${FAKE_UNITY_STATUS:-0}"
"""


class FixtureRepositoryTestCase(unittest.TestCase):
    """Builds the fixture once per class; tests remove whatever they add to it."""

    root: Path
    fake_unity: Path
    git_config: Path
    repository_environment: frozenset[str]

    @classmethod
    def setUpClass(cls) -> None:
        super().setUpClass()
        bash_or_skip()

        temporary_directory = tempfile.TemporaryDirectory()
        cls.addClassCleanup(temporary_directory.cleanup)
        cls.root = Path(temporary_directory.name) / "repository"
        cls.root.mkdir()
        # Git may treat the system temp directory as owned by someone else.
        cls.git_config = Path(temporary_directory.name) / "gitconfig"
        cls.git_config.write_text("[safe]\n\tdirectory = *\n", encoding="utf-8")
        # Git's own list of the variables that tie a command to one repository. Git exports
        # them to hooks, where they would point the fixture's commands at the repository
        # the tests were started from.
        cls.repository_environment = frozenset(
            subprocess.run(
                ["git", "rev-parse", "--local-env-vars"], capture_output=True, text=True, check=True
            ).stdout.split()
        )

        cls.fake_unity = Path(temporary_directory.name) / "fake-unity"
        cls.fake_unity.write_text(FAKE_UNITY, encoding="utf-8", newline="\n")
        cls.fake_unity.chmod(0o755)

        for entry_point in ENTRY_POINTS:
            if (repo_root() / entry_point).is_file():
                shutil.copy2(repo_root() / entry_point, cls.root / entry_point)
        (cls.root / "unity-cli-packages.tsv").write_text(
            "".join(f"{key}\t{package}\t{project}\n" for key, package, project in MAPPING),
            encoding="utf-8",
            newline="\n",
        )
        for _, package, project in MAPPING:
            runtime = cls.root / package / "Runtime"
            runtime.mkdir(parents=True)
            (runtime / "Code.cs").write_text("// fixture\n", encoding="utf-8")

            manifest = {
                "dependencies": {
                    f"fixture.{consumed.lower()}": f"file:../../{consumed}" for consumed in PROJECT_PACKAGES[project]
                }
            }
            packages = cls.root / project / "Packages"
            packages.mkdir(parents=True)
            (packages / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
            settings = cls.root / project / "ProjectSettings"
            settings.mkdir()
            (settings / "ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.7f1\n", encoding="utf-8")
            (cls.root / project / "Assets").mkdir()

        cls._git("init", "--quiet")
        cls._git("add", "--all")
        cls._git("-c", "commit.gpgsign=false", "commit", "--quiet", "--message", "fixture baseline")

    @classmethod
    def _environment(cls) -> dict[str, str]:
        # The user's and the machine's Git configuration stay out as well, so their hooks,
        # file-system monitors, and ignore rules do not apply to the fixture.
        environment = {key: value for key, value in os.environ.items() if key not in cls.repository_environment}
        for name in ("UNITY_EDITOR", "UNITY", "UNITY_HUB_EDITOR_ROOT", "FAKE_UNITY_RESULTS", "FAKE_UNITY_STATUS"):
            environment.pop(name, None)
        environment.update(
            {
                "GIT_CONFIG_GLOBAL": str(cls.git_config),
                "GIT_CONFIG_NOSYSTEM": "1",
                "GIT_AUTHOR_NAME": "fixture",
                "GIT_AUTHOR_EMAIL": "fixture@example.invalid",
                "GIT_COMMITTER_NAME": "fixture",
                "GIT_COMMITTER_EMAIL": "fixture@example.invalid",
            }
        )
        return environment

    @classmethod
    def _git(cls, *arguments: str) -> None:
        completed = subprocess.run(
            ["git", *arguments],
            cwd=cls.root,
            env=cls._environment(),
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        if completed.returncode != 0:
            raise RuntimeError(f"git {' '.join(arguments)} failed in the fixture: {completed.stderr.strip()}")

    def unity_environment(self, **variables: str) -> dict[str, str]:
        """The fixture environment with the fake Unity and a mirror directory of this test's own."""
        mirror_directory = tempfile.TemporaryDirectory()
        self.addCleanup(mirror_directory.cleanup)
        self.mirror_directory = Path(mirror_directory.name)
        return {
            **self._environment(),
            "UNITY_EDITOR": str(self.fake_unity),
            "TMPDIR": str(self.mirror_directory),
            **variables,
        }

    def run_entry_point(
        self, entry_point: str, *arguments: str, environment: dict[str, str] | None = None
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            bash_script_command(self.root / entry_point, list(arguments)),
            cwd=self.root,
            env=environment if environment is not None else self._environment(),
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )

    def run_functions(
        self, entry_point: str, body: str, *arguments: str, environment: dict[str, str] | None = None
    ) -> subprocess.CompletedProcess[str]:
        """Source an entry point and run body with the arguments as its positional parameters."""
        return run_sourced(
            self.root / entry_point,
            body,
            environment if environment is not None else self._environment(),
            arguments=arguments,
        )

    def mirrors_left(self) -> list[Path]:
        return sorted(self.mirror_directory.glob("mu3library-unity-compile.*"))
