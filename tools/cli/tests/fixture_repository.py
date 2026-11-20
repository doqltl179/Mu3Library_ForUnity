"""A throwaway repository shaped like this one, for running the real shell entry points."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

from common.bash import bash_script_command
from common.checkout import repo_root
from common.unity_projects import PROJECT_MARKER, WORK_FOLDER, remove_generated_project
from shell_harness import bash_or_skip, run_sourced

# Mirrors the shape of this repository: one package every config names, one layered on it, one
# leaf, a package whose folder name only extends another one's, and a config that names none.
PACKAGES = ("PkgShared", "PkgLayered", "PkgLeaf", "PkgSharedExtra")
CONFIG_PACKAGES = {
    "shared": ("PkgShared",),
    "layered": ("PkgShared", "PkgLayered"),
    "leaf": ("PkgShared", "PkgLayered", "PkgLeaf"),
    "extra": ("PkgSharedExtra",),
    "orphan": (),
}
# The scripts take the configs in the order of their names.
CONFIGS = tuple(sorted(CONFIG_PACKAGES))
ENTRY_POINTS = ("compile-unity.sh", "test-unity.sh")
CONFIGS_FOLDER = Path("tools", "unity-projects")
# The helper test-unity.sh runs beside each run, and the package whose unity_projects.py reads the
# configs and generates the projects, which the fixture keeps at their paths.
LOAD_HELPER = Path("tools") / "cli" / "src" / "fixed_load" / "fixed_load.py"
COMMON_PACKAGE = Path("tools") / "cli" / "src" / "common"
# The fixture's branch, which names the unit's folder under the work root.
BRANCH = "main"


def package_name(package: str) -> str:
    return f"fixture.{package.lower()}"


# Stands in for the Unity Editor: names the project, the arguments it ran with, and the
# -testFilter value as one argument, and the MSYS2_ARG_CONV_EXCL it got, prints the log line
# it is given, prints as many ticks as it is given, FAKE_UNITY_TICK_SECONDS (1 unless given)
# apart, and the log line it is given for after them, then stays silent as long as it is told, in a child process whose PID it can write to
# a file, writes the test results it is given to the path after -testResults, and exits with
# the status it is given. Given a file to count its runs in, run <n> prints FAKE_UNITY_LOG_<n>
# and writes FAKE_UNITY_RESULTS_<n> instead when they are set, so that each run of --repeat can
# differ. Given FAKE_UNITY_SERVICE_LOG, it writes that text as the log Unity's Data Store service
# writes in the project, Logs/UDS-Service.log.
FAKE_UNITY = """#!/usr/bin/env bash
arguments="$*"
results=""
project=""
filter=""
while [[ "$#" -gt 0 ]]; do
    case "$1" in
        -testResults) results="$2"; shift ;;
        -projectPath) project="$2"; shift ;;
        -testFilter) filter="$2"; shift ;;
    esac
    shift
done
printf 'fake unity ran %s\\n' "$project"
printf 'fake unity arguments: %s\\n' "$arguments"
if [[ -n "$filter" ]]; then
    printf 'fake unity test filter: [%s]\\n' "$filter"
fi
if [[ -n "${MSYS2_ARG_CONV_EXCL:-}" ]]; then
    printf 'fake unity path conversion exclusions: [%s]\\n' "$MSYS2_ARG_CONV_EXCL"
fi
if [[ -n "${FAKE_UNITY_RUN_FILE:-}" ]]; then
    run=$(( $(cat "$FAKE_UNITY_RUN_FILE" 2>/dev/null || printf 0) + 1 ))
    printf '%s\\n' "$run" > "$FAKE_UNITY_RUN_FILE"
    per_run="FAKE_UNITY_LOG_$run"
    FAKE_UNITY_LOG="${!per_run-${FAKE_UNITY_LOG:-}}"
    per_run="FAKE_UNITY_RESULTS_$run"
    FAKE_UNITY_RESULTS="${!per_run-${FAKE_UNITY_RESULTS:-}}"
fi
if [[ -n "${FAKE_UNITY_LOG:-}" ]]; then
    printf '%s\\n' "$FAKE_UNITY_LOG"
fi
if [[ -n "${FAKE_UNITY_SERVICE_LOG:-}" ]]; then
    mkdir -p "$project/Logs"
    printf '%s\\n' "$FAKE_UNITY_SERVICE_LOG" > "$project/Logs/UDS-Service.log"
fi
for ((tick = 1; tick <= ${FAKE_UNITY_TICKS:-0}; tick++)); do
    sleep "${FAKE_UNITY_TICK_SECONDS:-1}"
    printf 'fake unity tick %s\\n' "$tick"
done
if [[ -n "${FAKE_UNITY_LOG_AFTER_TICKS:-}" ]]; then
    printf '%s\\n' "$FAKE_UNITY_LOG_AFTER_TICKS"
fi
if [[ -n "${FAKE_UNITY_SLEEP:-}" ]]; then
    sleep "$FAKE_UNITY_SLEEP" &
    sleeper=$!
    if [[ -n "${FAKE_UNITY_SLEEPER_FILE:-}" ]]; then
        printf '%s\\n' "$sleeper" > "$FAKE_UNITY_SLEEPER_FILE"
    fi
    wait "$sleeper"
fi
if [[ -n "$results" && -n "${FAKE_UNITY_RESULTS:-}" ]]; then
    printf '%s\\n' "$FAKE_UNITY_RESULTS" > "$results"
fi
exit "${FAKE_UNITY_STATUS:-0}"
"""


class FixtureRepositoryTestCase(unittest.TestCase):
    """Builds the fixture once per class; tests remove whatever they add to it."""

    # Configs whose manifest lists their packages under "testables", as a config must for Unity
    # to run a package's tests.
    TESTABLE_CONFIGS: frozenset[str] = frozenset()
    # Lines of the fixture's .gitignore, committed with the baseline.
    GITIGNORE_LINES: tuple[str, ...] = ()
    # The name of the fixture's root folder inside its temporary directory, which a longer name
    # pushes deeper, as a nested worktree is.
    ROOT_NAME = "repository"

    root: Path
    fake_unity: Path
    git_config: Path
    # The work root a run without unity_environment sees; nothing creates it.
    class_work_root: Path
    repository_environment: frozenset[str]

    @classmethod
    def setUpClass(cls) -> None:
        super().setUpClass()
        bash_or_skip()

        temporary_directory = tempfile.TemporaryDirectory()
        cls.addClassCleanup(temporary_directory.cleanup)
        cls.root = Path(temporary_directory.name) / cls.ROOT_NAME
        cls.root.mkdir()
        cls.class_work_root = Path(temporary_directory.name) / "unused-work-root"
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
        (cls.root / LOAD_HELPER).parent.mkdir(parents=True)
        shutil.copy2(repo_root() / LOAD_HELPER, cls.root / LOAD_HELPER)
        shutil.copytree(repo_root() / COMMON_PACKAGE, cls.root / COMMON_PACKAGE, ignore=shutil.ignore_patterns("__pycache__"))

        for package in PACKAGES:
            runtime = cls.root / package / "Runtime"
            runtime.mkdir(parents=True)
            (runtime / "Code.cs").write_text("// fixture\n", encoding="utf-8")
            (cls.root / package / "package.json").write_text(
                json.dumps({"name": package_name(package), "version": "1.0.0"}, indent=2) + "\n", encoding="utf-8"
            )

        for config, packages in CONFIG_PACKAGES.items():
            folder = cls.root / CONFIGS_FOLDER / config
            manifest: dict[str, object] = {"dependencies": {package_name(package): "checkout" for package in packages}}
            if config in cls.TESTABLE_CONFIGS:
                manifest["testables"] = [package_name(package) for package in packages]
            (folder / "Packages").mkdir(parents=True)
            (folder / "Packages" / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
            (folder / "ProjectSettings").mkdir()
            (folder / "ProjectSettings" / "ProjectVersion.txt").write_text("m_EditorVersion: 6000.6.7f1\n", encoding="utf-8")

        if cls.GITIGNORE_LINES:
            (cls.root / ".gitignore").write_text("".join(f"{line}\n" for line in cls.GITIGNORE_LINES), encoding="utf-8")

        cls._git("init", "--quiet", f"--initial-branch={BRANCH}")
        cls._git("add", "--all")
        cls._git("-c", "commit.gpgsign=false", "commit", "--quiet", "--message", "fixture baseline")

    @classmethod
    def _environment(cls) -> dict[str, str]:
        # The user's and the machine's Git configuration stay out as well, so their hooks,
        # file-system monitors, and ignore rules do not apply to the fixture.
        environment = {key: value for key, value in os.environ.items() if key not in cls.repository_environment}
        for name in (
            "UNITY_EDITOR",
            "UNITY",
            "UNITY_HUB_EDITOR_ROOT",
            "FAKE_UNITY_RESULTS",
            "FAKE_UNITY_STATUS",
            "FAKE_UNITY_LOG",
            "FAKE_UNITY_TICKS",
            "FAKE_UNITY_LOG_AFTER_TICKS",
            "FAKE_UNITY_TICK_SECONDS",
            "FAKE_UNITY_SLEEP",
            "FAKE_UNITY_SLEEPER_FILE",
            "FAKE_UNITY_RUN_FILE",
            "FAKE_UNITY_SERVICE_LOG",
            "MSYS2_ARG_CONV_EXCL",
            "UNITY_LOG_POLL_SECONDS",
            "UNITY_LOAD_SPINNERS_IDLE",
        ):
            environment.pop(name, None)
        environment.update(
            {
                "GIT_CONFIG_GLOBAL": str(cls.git_config),
                "GIT_CONFIG_NOSYSTEM": "1",
                "GIT_AUTHOR_NAME": "fixture",
                "GIT_AUTHOR_EMAIL": "fixture@example.invalid",
                "GIT_COMMITTER_NAME": "fixture",
                "GIT_COMMITTER_EMAIL": "fixture@example.invalid",
                "MU3_WORK_ROOT": str(cls.class_work_root),
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

    @classmethod
    def unit_folder(cls, work_root: Path) -> Path:
        """The work folder of the fixture's branch under a work root."""
        return work_root / WORK_FOLDER / BRANCH

    def own_work_root(self) -> Path:
        """This test's own work root, made the first time the test asks for it; it goes when the test ends."""
        # unittest runs each test on an instance of its own.
        if "work_root" not in self.__dict__:
            work_root = tempfile.TemporaryDirectory()
            self.addCleanup(work_root.cleanup)
            self.work_root = Path(work_root.name)
            # Cleanups run last first: the projects lose their links before the folder goes.
            self.addCleanup(self.remove_projects)
        return self.work_root

    def project_folder(self, config: str) -> Path:
        """The folder of a config's project under this test's work root."""
        return self.unit_folder(self.own_work_root()) / config

    def unity_environment(self, **variables: str) -> dict[str, str]:
        """The fixture environment with the fake Unity and this test's own work root.

        The logs and results that a run with it writes under log/, and the projects it generates,
        go when the test ends. The scripts look at the fake's log every 0.02 s instead of every
        second, since the fake exits at once and each run would otherwise wait out a whole second,
        and the spinners that --load-spinners starts wait instead of spinning a core each.
        """
        self.addCleanup(shutil.rmtree, self.root / "log", ignore_errors=True)
        return {
            **self._environment(),
            "UNITY_EDITOR": str(self.fake_unity),
            "MU3_WORK_ROOT": str(self.own_work_root()),
            "UNITY_LOG_POLL_SECONDS": "0.02",
            "UNITY_LOAD_SPINNERS_IDLE": "1",
            **variables,
        }

    def remove_projects(self) -> None:
        """Delete the projects generated under this test's work root, each link first, without following it."""
        unit = self.unit_folder(self.work_root)
        if unit.is_dir():
            for project in unit.iterdir():
                if (project / PROJECT_MARKER).is_file():
                    remove_generated_project(project)

    def run_entry_point(
        self,
        entry_point: str,
        *arguments: str,
        environment: dict[str, str] | None = None,
        cwd: Path | None = None,
        script: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        """Run an entry point from cwd, the fixture's root unless given.

        script spells the entry point's path for bash, such as in another form of the same path;
        without it the entry point runs by its path as the unity commands run it.
        """
        return subprocess.run(
            bash_script_command(self.root / entry_point, list(arguments))
            if script is None
            else [str(bash_or_skip()), script, *arguments],
            cwd=cwd or self.root,
            env=environment if environment is not None else self._environment(),
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )

    def run_functions(
        self,
        entry_point: str,
        body: str,
        *arguments: str,
        environment: dict[str, str] | None = None,
        cwd: Path | None = None,
        script: str | None = None,
    ) -> subprocess.CompletedProcess[str]:
        """Source an entry point and run body with the arguments as its positional parameters.

        It runs from cwd, the fixture's root unless given, and sources script, a spelling of the
        entry point's path, or the path itself.
        """
        return run_sourced(
            script or self.root / entry_point,
            body,
            environment if environment is not None else self._environment(),
            cwd=cwd or self.root,
            arguments=arguments,
        )
