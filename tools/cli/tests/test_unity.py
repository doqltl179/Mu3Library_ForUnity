from __future__ import annotations

import json
import os
import platform
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path
from typing import Callable
from unittest import mock

from typer.testing import CliRunner

from common.bash import BashNotFoundError, find_bash, find_windows_bash
from common.checkout import repo_root
from common.editor import default_editor_path
from common.project_lock import file_is_held_open, project_lock_state
from common.unity_projects import create_directory_link, remove_directory_link
from unity.cli import (
    bash_diagnostic,
    entry_point_arguments,
    compile_script_path,
    filter_arguments,
    load_arguments,
    platform_arguments,
    project_lock_diagnostic,
    stall_timeout_arguments,
    work_dir_diagnostic,
)
from unity.cli import app as unity_app
from shell_harness import run_sourced


def temporary_root(test: unittest.TestCase) -> Path:
    directory = tempfile.TemporaryDirectory()
    test.addCleanup(directory.cleanup)
    return Path(directory.name)


class UnityToolingTests(unittest.TestCase):
    def test_compile_arguments_preserve_change_selection(self) -> None:
        self.assertEqual(
            ["changed", "--base", "origin/develop", "--dry-run"],
            entry_point_arguments("changed", "origin/develop", True),
        )

    def test_base_requires_changed_target(self) -> None:
        with self.assertRaisesRegex(ValueError, "changed target"):
            entry_point_arguments("full", "origin/develop", False)

    def test_work_dir_and_editor_version_reach_the_script(self) -> None:
        self.assertEqual(
            ["all", "--work-dir", str(Path("D:/w/unit")), "--editor-version", "6000.3.17f1"],
            entry_point_arguments("all", None, False, Path("D:/w/unit"), "6000.3.17f1"),
        )

    def test_platform_reaches_the_test_script(self) -> None:
        self.assertEqual(["--platform", "playmode"], platform_arguments("playmode"))

    def test_platform_rejects_a_name_the_test_script_does_not_take(self) -> None:
        with self.assertRaisesRegex(ValueError, "--platform expects one of: editmode, playmode"):
            platform_arguments("PlayMode")

    def test_filter_reaches_the_test_script_only_when_given(self) -> None:
        self.assertEqual([], filter_arguments(None))
        self.assertEqual(["--filter", "A.Tests;B.Tests"], filter_arguments("A.Tests;B.Tests"))

    def test_filter_rejects_what_the_test_script_rejects(self) -> None:
        for pattern, message in (
            ("", "--filter requires a test name or pattern"),
            ("  ", "--filter requires a test name or pattern"),
            ("--dry-run", "--filter expects a test name or pattern, not an option: --dry-run"),
            ("/Mu3/Regex", "--filter cannot start with /, which Git Bash rewrites as a file path"),
        ):
            with self.subTest(pattern=pattern), self.assertRaisesRegex(ValueError, message):
                filter_arguments(pattern)

    def test_test_command_refuses_an_option_as_its_filter(self) -> None:
        calls: list[list[str]] = []

        with mock.patch("unity.cli.run_entry_point", lambda _script, arguments, _root: calls.append(arguments) or 0):
            result = CliRunner().invoke(unity_app, ["test", "--filter", "--dry-run", "full"])

        self.assertEqual(2, result.exit_code, result.output)
        # The error box wraps the message, so the option it names can land on the next line.
        self.assertIn("not an option:", result.output)
        self.assertEqual([], calls)

    def test_stall_timeout_reaches_the_test_script_only_when_given(self) -> None:
        self.assertEqual([], stall_timeout_arguments(None))
        self.assertEqual(["--stall-timeout", "0"], stall_timeout_arguments(0))
        self.assertEqual(["--stall-timeout", "900"], stall_timeout_arguments(900))
        with self.assertRaisesRegex(ValueError, "--stall-timeout expects a whole number of seconds"):
            stall_timeout_arguments(-1)

    def test_test_command_hands_its_options_to_the_test_script(self) -> None:
        calls: list[tuple[Path, list[str]]] = []

        def run(script_path: Path, arguments: list[str], root: Path) -> int:
            calls.append((script_path, arguments))
            return 0

        with mock.patch("unity.cli.run_entry_point", run):
            result = CliRunner().invoke(
                unity_app,
                [
                    "test",
                    "full",
                    "--dry-run",
                    "--filter",
                    "Mu3Library.Tests.MultiplayerPerformanceTests",
                    "--stall-timeout",
                    "120",
                ],
            )

        self.assertEqual(0, result.exit_code, result.output)
        [(script_path, arguments)] = calls
        self.assertEqual("test-unity.sh", script_path.name)
        self.assertEqual(
            [
                "full",
                "--dry-run",
                "--platform",
                "editmode",
                "--filter",
                "Mu3Library.Tests.MultiplayerPerformanceTests",
                "--stall-timeout",
                "120",
            ],
            arguments,
        )

    def test_load_and_repeat_reach_the_test_script_only_when_given(self) -> None:
        self.assertEqual([], load_arguments(None, None))
        self.assertEqual(["--load-spinners", "0"], load_arguments(0, None))
        self.assertEqual(["--load-spinners", "16", "--repeat", "3"], load_arguments(16, 3))
        with self.assertRaisesRegex(ValueError, "--load-spinners expects a whole number of spinners"):
            load_arguments(-1, None)
        with self.assertRaisesRegex(ValueError, "--repeat expects a whole number of runs, 1 or more"):
            load_arguments(None, 0)

    def test_test_command_hands_the_load_and_the_repeats_to_the_test_script(self) -> None:
        calls: list[list[str]] = []

        with mock.patch("unity.cli.run_entry_point", lambda _script, arguments, _root: calls.append(arguments) or 0):
            result = CliRunner().invoke(
                unity_app, ["test", "full", "--dry-run", "--load-spinners", "16", "--repeat", "3"]
            )
            refused = CliRunner().invoke(unity_app, ["test", "full", "--repeat", "0"])

        self.assertEqual(0, result.exit_code, result.output)
        self.assertEqual(
            [["full", "--dry-run", "--platform", "editmode", "--load-spinners", "16", "--repeat", "3"]], calls
        )
        self.assertEqual(2, refused.exit_code, refused.output)

    def test_compile_command_hands_the_stall_timeout_to_the_compile_script(self) -> None:
        calls: list[tuple[Path, list[str]]] = []

        def run(script_path: Path, arguments: list[str], root: Path) -> int:
            calls.append((script_path, arguments))
            return 0

        with mock.patch("unity.cli.run_entry_point", run):
            result = CliRunner().invoke(unity_app, ["compile", "full", "--dry-run", "--stall-timeout", "120"])
            without = CliRunner().invoke(unity_app, ["compile", "full", "--dry-run"])

        self.assertEqual(0, result.exit_code, result.output)
        self.assertEqual(0, without.exit_code, without.output)
        self.assertEqual(
            [
                ("compile-unity.sh", ["full", "--dry-run", "--stall-timeout", "120"]),
                ("compile-unity.sh", ["full", "--dry-run"]),
            ],
            [(script_path.name, arguments) for script_path, arguments in calls],
        )


class ProjectLockTests(unittest.TestCase):
    """An Editor holds Temp/UnityLockfile open; a crashed one leaves the file behind unheld."""

    def setUp(self) -> None:
        self.root = temporary_root(self)
        self.project = self.root / "Project"
        self.lock_file = self.project / "Temp" / "UnityLockfile"

    def write_lock_file(self) -> None:
        self.lock_file.parent.mkdir(parents=True)
        self.lock_file.write_text("", encoding="utf-8")

    def test_project_without_lock_file_is_not_locked(self) -> None:
        self.assertEqual("absent", project_lock_state(self.project, lambda _: True))

    def test_held_lock_file_means_the_project_is_open(self) -> None:
        self.write_lock_file()

        self.assertEqual("open", project_lock_state(self.project, lambda _: True))

    def test_unheld_lock_file_is_stale(self) -> None:
        self.write_lock_file()

        self.assertEqual("stale", project_lock_state(self.project, lambda _: False))

    def test_lock_file_is_unknown_when_the_holder_cannot_be_checked(self) -> None:
        self.write_lock_file()

        self.assertEqual("unknown", project_lock_state(self.project, lambda _: None))

    def test_doctor_reports_a_stale_lock_apart_from_an_open_project(self) -> None:
        self.write_lock_file()

        open_project = project_lock_diagnostic("sample", self.project, lambda _: True)
        stale_lock = project_lock_diagnostic("sample", self.project, lambda _: False)

        self.assertEqual("WARN", open_project.status)
        self.assertIn("is open in Unity", open_project.detail)
        self.assertIn("refuse to refresh it", open_project.detail)
        self.assertIn(f"{self.lock_file.as_posix()} is stale", stale_lock.detail)
        self.assertIn("runs the project anyway", stale_lock.detail)
        self.assertNotIn("is open", stale_lock.detail)
        self.assertIsNotNone(stale_lock.suggestion)

    def test_unknown_lock_advice_fits_the_host(self) -> None:
        self.write_lock_file()

        windows = project_lock_diagnostic("sample", self.project, lambda _: None, system="Windows")
        linux = project_lock_diagnostic("sample", self.project, lambda _: None, system="Linux")

        self.assertIn("could not be opened", windows.detail)
        self.assertNotIn("lsof", windows.detail + windows.suggestion)
        self.assertIn(f"delete {self.lock_file.as_posix()}", windows.suggestion)
        self.assertIn("lsof", linux.detail)
        self.assertIn("Install lsof", linux.suggestion)
        self.assertIn(f"delete {self.lock_file.as_posix()}", linux.suggestion)

    def test_doctor_passes_a_project_without_lock_file_or_not_generated_yet(self) -> None:
        self.assertEqual("PASS", project_lock_diagnostic("sample", self.project, lambda _: True).status)
        self.project.mkdir()
        diagnostic = project_lock_diagnostic("sample", self.project, lambda _: True)

        self.assertEqual(("PASS", "Project is not locked."), (diagnostic.status, diagnostic.detail))

    def test_doctor_fails_without_a_work_folder_and_names_what_to_set(self) -> None:
        with mock.patch.dict(os.environ, {"MU3_WORK_ROOT": ""}):
            diagnostic, folder = work_dir_diagnostic(repo_root())

        self.assertEqual("FAIL", diagnostic.status)
        self.assertIsNone(folder)
        self.assertIn("MU3_WORK_ROOT", diagnostic.detail)

    @unittest.skipUnless(
        platform.system() == "Windows" or shutil.which("lsof"), "no way to check file holders on this host"
    )
    def test_probe_sees_a_handle_that_another_open_holds(self) -> None:
        self.write_lock_file()

        with self.lock_file.open("rb"):
            self.assertIs(True, file_is_held_open(self.lock_file))
        self.assertIs(False, file_is_held_open(self.lock_file))


class RepositoryRootLineTests(unittest.TestCase):
    """Every unity command names the checkout it acts on, which for an installed mu3-unity is the one
    it was installed from, wherever it runs (#416)."""

    def test_selection_commands_name_the_root_before_running_the_entry_point(self) -> None:
        root = repo_root()
        for arguments in (["changes"], ["compile", "--dry-run"], ["test", "--dry-run"]):
            with self.subTest(command=arguments[0]):
                with mock.patch("unity.cli.run_entry_point", return_value=0) as run:
                    result = CliRunner().invoke(unity_app, arguments)

                self.assertEqual(0, result.exit_code, result.output)
                self.assertEqual([f"Repository root: {root}"], result.stdout.splitlines())
                self.assertEqual(root, run.call_args.args[2])

    def test_doctor_names_the_root_in_text_and_in_json(self) -> None:
        root = repo_root()
        with mock.patch("unity.cli.run_unity_diagnostics", return_value=[]):
            text = CliRunner().invoke(unity_app, ["doctor"])
            as_json = CliRunner().invoke(unity_app, ["doctor", "--format", "json"])

        self.assertEqual(0, text.exit_code, text.output)
        self.assertEqual([f"Repository root: {root}", "Summary: 0 pass, 0 warn, 0 fail"], text.stdout.splitlines())
        self.assertEqual(0, as_json.exit_code, as_json.output)
        self.assertEqual(
            {"repository_root": str(root), "diagnostics": [], "summary": {"pass": 0, "warn": 0, "fail": 0}},
            json.loads(as_json.stdout),
        )

    def test_doctor_names_the_root_before_it_refuses_a_target(self) -> None:
        root = repo_root()

        text = CliRunner().invoke(unity_app, ["doctor", "--target", "missing"])
        as_json = CliRunner().invoke(unity_app, ["doctor", "--format", "json", "--target", "missing"])

        self.assertEqual(2, text.exit_code, text.output)
        self.assertEqual([f"Repository root: {root}"], text.stdout.splitlines())
        self.assertIn("Unknown Unity target 'missing'", text.stderr)
        # The JSON report names the root only as a field of the report, which a refused target never reaches.
        self.assertEqual(2, as_json.exit_code, as_json.output)
        self.assertEqual("", as_json.stdout)


class BashDiscoveryTests(unittest.TestCase):
    """Git for Windows puts git on PATH but not bash, and System32 holds the WSL launcher."""

    def setUp(self) -> None:
        self.root = temporary_root(self)
        # No Program Files entries, so the Git installed on the test machine stays out of reach.
        self.environ = {"SystemRoot": str(self.root / "Windows")}

    def make_file(self, *parts: str) -> Path:
        path = self.root.joinpath(*parts)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("", encoding="utf-8")
        return path

    @staticmethod
    def on_path(**commands: Path) -> Callable[[str], str | None]:
        return lambda name: str(commands[name]) if name in commands else None

    def test_bash_on_path_is_used(self) -> None:
        bash = self.make_file("Git", "usr", "bin", "bash.exe")

        self.assertEqual(bash, find_windows_bash(self.on_path(bash=bash), self.environ))

    def test_bash_is_found_from_git_in_the_cmd_folder(self) -> None:
        bash = self.make_file("Git", "bin", "bash.exe")
        git = self.make_file("Git", "cmd", "git.exe")

        self.assertEqual(bash, find_windows_bash(self.on_path(git=git), self.environ))

    def test_bash_is_found_from_git_in_the_mingw64_folder(self) -> None:
        bash = self.make_file("Git", "bin", "bash.exe")
        git = self.make_file("Git", "mingw64", "bin", "git.exe")

        self.assertEqual(bash, find_windows_bash(self.on_path(git=git), self.environ))

    def test_wsl_launcher_on_path_gives_way_to_git_bash(self) -> None:
        launcher = self.make_file("Windows", "System32", "bash.exe")
        bash = self.make_file("Git", "bin", "bash.exe")
        git = self.make_file("Git", "cmd", "git.exe")

        self.assertEqual(bash, find_windows_bash(self.on_path(bash=launcher, git=git), self.environ))

    def test_store_alias_for_wsl_is_skipped(self) -> None:
        alias = self.make_file("Users", "me", "AppData", "Local", "Microsoft", "WindowsApps", "bash.exe")
        bash = self.make_file("Git", "bin", "bash.exe")
        git = self.make_file("Git", "cmd", "git.exe")

        self.assertEqual(bash, find_windows_bash(self.on_path(bash=alias, git=git), self.environ))

    def test_default_install_location_is_used_without_git_on_path(self) -> None:
        bash = self.make_file("Program Files", "Git", "bin", "bash.exe")
        environ = {**self.environ, "ProgramFiles": str(self.root / "Program Files")}

        self.assertEqual(bash, find_windows_bash(self.on_path(), environ))

    def test_missing_bash_names_what_to_do(self) -> None:
        launcher = self.make_file("Windows", "System32", "bash.exe")
        git = self.make_file("Git", "cmd", "git.exe")

        with self.assertRaises(BashNotFoundError) as caught:
            find_windows_bash(self.on_path(bash=launcher, git=git), self.environ)

        self.assertIn("starts WSL, not Git Bash", caught.exception.detail)
        self.assertIn(f"next to {git}", caught.exception.detail)
        self.assertIn("Install Git for Windows", caught.exception.suggestion)
        self.assertIn("C:\\Program Files\\Git\\bin, to PATH", caught.exception.suggestion)

    def test_other_hosts_use_the_bash_on_path(self) -> None:
        bash = self.make_file("usr", "bin", "bash")

        self.assertEqual(bash, find_bash(self.on_path(bash=bash), self.environ, system="Linux"))
        with self.assertRaises(BashNotFoundError):
            find_bash(self.on_path(), self.environ, system="Darwin")

    def test_doctor_fails_without_bash(self) -> None:
        def missing() -> Path:
            raise BashNotFoundError("No bash.", "Install one.")

        diagnostic = bash_diagnostic(missing)

        self.assertEqual(("FAIL", "No bash.", "Install one."), (diagnostic.status, diagnostic.detail, diagnostic.suggestion))

    def test_doctor_passes_with_bash(self) -> None:
        bash = self.make_file("Git", "bin", "bash.exe")

        self.assertEqual("PASS", bash_diagnostic(lambda: bash).status)


class CommandErrorTests(unittest.TestCase):
    def test_changes_and_test_report_a_missing_bash_the_way_compile_does(self) -> None:
        def missing(*_arguments: object, **_keywords: object) -> Path:
            raise BashNotFoundError("No bash here.", "Install it.")

        runner = CliRunner()
        with mock.patch("common.bash.platform.system", return_value="Windows"), mock.patch(
            "common.bash.find_windows_bash", missing
        ):
            changes = runner.invoke(unity_app, ["changes"])
            compile_run = runner.invoke(unity_app, ["compile", "--dry-run"])
            test_run = runner.invoke(unity_app, ["test", "--dry-run"])

        for result in (changes, compile_run, test_run):
            with self.subTest(command=result):
                self.assertEqual(2, result.exit_code, result.output)
                self.assertNotIsInstance(result.exception, ValueError)
                self.assertIn("No bash here. Install it.", result.output)


class EditorRootTests(unittest.TestCase):
    def test_doctor_and_compile_script_read_the_same_hub_editor_root(self) -> None:
        editor_root = temporary_root(self)
        environment = {key: value for key, value in os.environ.items() if key not in {"UNITY_EDITOR", "UNITY"}}
        environment["UNITY_HUB_EDITOR_ROOT"] = str(editor_root)

        with mock.patch.dict(os.environ, environment, clear=True):
            editor = default_editor_path("6000.6.7f1")
        self.assertTrue(editor.is_relative_to(editor_root), editor)
        editor.parent.mkdir(parents=True)
        editor.write_text("", encoding="utf-8")

        completed = run_sourced(
            compile_script_path(repo_root()),
            'candidate="$(hub_editor_executable 6000.6.7f1)"\n[[ -f "$candidate" ]] && printf found',
            environment,
        )

        self.assertEqual("found", completed.stdout, completed.stderr)


class ShortPathTests(unittest.TestCase):
    """Unity must open the packages on the subst drive a command ran from, not on the long path it maps.

    A link to the checkout stands in for the drive: Path.resolve() follows both back to the long path.
    """

    def setUp(self) -> None:
        self.short_root = temporary_root(self) / "short"
        create_directory_link(self.short_root, repo_root().resolve())
        # Remove the link before the temporary folder goes, so cleanup never walks through it.
        self.addCleanup(remove_directory_link, self.short_root)

    def test_compile_starts_the_entry_point_on_the_short_path(self) -> None:
        calls: list[tuple[list[str], dict[str, object]]] = []

        def run(command: list[str], **keywords: object) -> subprocess.CompletedProcess[str]:
            calls.append((command, keywords))
            return subprocess.CompletedProcess(command, 0)

        with mock.patch.object(Path, "cwd", return_value=self.short_root), mock.patch(
            "common.bash.find_windows_bash", return_value=Path("bash.exe")
        ), mock.patch("unity.cli.subprocess.run", run):
            result = CliRunner().invoke(unity_app, ["compile", "full", "--dry-run"])

        self.assertEqual(0, result.exit_code, result.output)
        [(command, keywords)] = calls
        self.assertIn(str(self.short_root / "compile-unity.sh"), command)
        self.assertEqual(self.short_root, keywords["cwd"])
        self.assertEqual(f"Repository root: {self.short_root}", result.stdout.splitlines()[0])

    def test_entry_point_keeps_the_short_path_it_was_started_from(self) -> None:
        # compile-unity.sh passes $REPO_ROOT to the project generator, which writes each checkout
        # package's folder below it into the generated project's manifest, for Unity to open.
        completed = run_sourced(self.short_root / "compile-unity.sh", 'to_windows_path "$REPO_ROOT/packages/base"')

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(self.short_root / "packages" / "base", Path(completed.stdout.strip()))


if __name__ == "__main__":
    unittest.main()
