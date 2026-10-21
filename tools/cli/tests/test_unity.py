from __future__ import annotations

import os
import platform
import shutil
import tempfile
import unittest
from pathlib import Path
from typing import Callable
from unittest import mock

from typer.testing import CliRunner

from mu3_cli.cli import app
from mu3_cli.repository import repo_root
from mu3_cli.unity import (
    BashNotFoundError,
    UnityTargetSpec,
    bash_diagnostic,
    compile_command_arguments,
    compile_script_path,
    default_editor_path,
    file_is_held_open,
    find_bash,
    find_windows_bash,
    load_target_specs,
    project_editor_version,
    project_lock_diagnostic,
    project_lock_state,
)
from shell_harness import run_sourced


def temporary_root(test: unittest.TestCase) -> Path:
    directory = tempfile.TemporaryDirectory()
    test.addCleanup(directory.cleanup)
    return Path(directory.name)


class UnityToolingTests(unittest.TestCase):
    def test_repository_mapping_loads_all_targets(self) -> None:
        specs = load_target_specs(repo_root())

        self.assertEqual(["built-in", "urp", "watermelon"], list(specs))
        self.assertEqual("Mu3Library_Base", specs["built-in"].package_path.as_posix())
        self.assertEqual("UnityProject_URP", specs["urp"].project_path.as_posix())

    def test_every_mapping_has_an_editor_version(self) -> None:
        # The versions themselves change with every editor upgrade; the lookup has to find one.
        root = repo_root()
        specs = load_target_specs(root)

        for key, spec in specs.items():
            with self.subTest(target=key):
                self.assertRegex(project_editor_version(spec, root) or "", r"^\d+\.\d+\.\d+[abfp]\d+$")

    def test_compile_arguments_preserve_change_selection(self) -> None:
        self.assertEqual(
            ["changed", "--base", "origin/develop", "--dry-run"],
            compile_command_arguments("changed", "origin/develop", True, False, False, False),
        )

    def test_compile_arguments_reject_conflicting_modes(self) -> None:
        with self.assertRaisesRegex(ValueError, "cannot be used together"):
            compile_command_arguments("built-in", None, False, True, True, False)

    def test_keep_staging_preserves_auto_isolation_behavior(self) -> None:
        self.assertEqual(
            ["built-in", "--keep-staging"],
            compile_command_arguments("built-in", None, False, False, False, True),
        )

    def test_base_requires_changed_target(self) -> None:
        with self.assertRaisesRegex(ValueError, "changed target"):
            compile_command_arguments("built-in", "origin/develop", False, False, False, False)


class ProjectLockTests(unittest.TestCase):
    """An Editor holds Temp/UnityLockfile open; a crashed one leaves the file behind unheld."""

    def setUp(self) -> None:
        self.root = temporary_root(self)
        self.spec = UnityTargetSpec(key="sample", package_path=Path("Package"), project_path=Path("Project"))
        self.lock_file = self.root / "Project" / "Temp" / "UnityLockfile"

    def write_lock_file(self) -> None:
        self.lock_file.parent.mkdir(parents=True)
        self.lock_file.write_text("", encoding="utf-8")

    def test_project_without_lock_file_is_not_locked(self) -> None:
        self.assertEqual("absent", project_lock_state(self.root / "Project", lambda _: True))

    def test_held_lock_file_means_the_project_is_open(self) -> None:
        self.write_lock_file()

        self.assertEqual("open", project_lock_state(self.root / "Project", lambda _: True))

    def test_unheld_lock_file_is_stale(self) -> None:
        self.write_lock_file()

        self.assertEqual("stale", project_lock_state(self.root / "Project", lambda _: False))

    def test_lock_file_is_unknown_when_the_holder_cannot_be_checked(self) -> None:
        self.write_lock_file()

        self.assertEqual("unknown", project_lock_state(self.root / "Project", lambda _: None))

    def test_doctor_reports_a_stale_lock_apart_from_an_open_project(self) -> None:
        self.write_lock_file()

        open_project = project_lock_diagnostic(self.spec, self.root, lambda _: True)
        stale_lock = project_lock_diagnostic(self.spec, self.root, lambda _: False)

        self.assertEqual("WARN", open_project.status)
        self.assertIn("is open in Unity", open_project.detail)
        self.assertIn("isolated mirror", open_project.detail)
        self.assertIn("Project/Temp/UnityLockfile is stale", stale_lock.detail)
        self.assertIn("compiles in place", stale_lock.detail)
        self.assertNotIn("is open", stale_lock.detail)
        self.assertIsNotNone(stale_lock.suggestion)

    def test_doctor_passes_a_project_without_lock_file(self) -> None:
        diagnostic = project_lock_diagnostic(self.spec, self.root, lambda _: True)

        self.assertEqual(("PASS", "Project is not locked."), (diagnostic.status, diagnostic.detail))

    @unittest.skipUnless(
        platform.system() == "Windows" or shutil.which("lsof"), "no way to check file holders on this host"
    )
    def test_probe_sees_a_handle_that_another_open_holds(self) -> None:
        self.write_lock_file()

        with self.lock_file.open("rb"):
            self.assertIs(True, file_is_held_open(self.lock_file))
        self.assertIs(False, file_is_held_open(self.lock_file))


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
    def test_changes_reports_a_missing_bash_the_way_compile_does(self) -> None:
        def missing(*_arguments: object, **_keywords: object) -> Path:
            raise BashNotFoundError("No bash here.", "Install it.")

        runner = CliRunner()
        with mock.patch("mu3_cli.unity.platform.system", return_value="Windows"), mock.patch(
            "mu3_cli.unity.find_windows_bash", missing
        ):
            changes = runner.invoke(app, ["unity", "changes"])
            compile_run = runner.invoke(app, ["unity", "compile", "--dry-run"])

        for result in (changes, compile_run):
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


if __name__ == "__main__":
    unittest.main()
