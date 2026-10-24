from __future__ import annotations

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

from mu3_cli.cli import app
from mu3_cli.repository import repo_root
from mu3_cli.unity import (
    BashNotFoundError,
    SampleLinkResult,
    UnityTargetSpec,
    bash_diagnostic,
    entry_point_arguments,
    compile_script_path,
    create_directory_link,
    default_editor_path,
    file_is_held_open,
    find_bash,
    find_windows_bash,
    link_samples,
    load_target_specs,
    platform_arguments,
    project_editor_version,
    project_lock_diagnostic,
    project_lock_state,
    remove_directory_link,
    sample_link_diagnostic,
    sample_link_state,
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
            entry_point_arguments("changed", "origin/develop", True, False, False, False),
        )

    def test_compile_arguments_reject_conflicting_modes(self) -> None:
        with self.assertRaisesRegex(ValueError, "cannot be used together"):
            entry_point_arguments("built-in", None, False, True, True, False)

    def test_keep_staging_preserves_auto_isolation_behavior(self) -> None:
        self.assertEqual(
            ["built-in", "--keep-staging"],
            entry_point_arguments("built-in", None, False, False, False, True),
        )

    def test_base_requires_changed_target(self) -> None:
        with self.assertRaisesRegex(ValueError, "changed target"):
            entry_point_arguments("built-in", "origin/develop", False, False, False, False)

    def test_without_samples_reaches_the_script(self) -> None:
        self.assertEqual(
            ["built-in", "--without-samples"],
            entry_point_arguments("built-in", None, False, False, False, False, without_samples=True),
        )

    def test_editor_version_reaches_the_script(self) -> None:
        self.assertEqual(
            ["all", "--editor-version", "6000.3.17f1"],
            entry_point_arguments("all", None, False, False, False, False, editor_version="6000.3.17f1"),
        )

    def test_editor_version_rejects_in_place(self) -> None:
        with self.assertRaisesRegex(ValueError, "--editor-version runs in an isolated mirror"):
            entry_point_arguments("built-in", None, False, False, True, False, editor_version="6000.3.17f1")

    def test_platform_reaches_the_test_script(self) -> None:
        self.assertEqual(["--platform", "playmode"], platform_arguments("playmode"))

    def test_platform_rejects_a_name_the_test_script_does_not_take(self) -> None:
        with self.assertRaisesRegex(ValueError, "--platform expects one of: editmode, playmode"):
            platform_arguments("PlayMode")

    def test_repository_mapping_names_every_samples_link(self) -> None:
        specs = load_target_specs(repo_root())

        self.assertEqual(
            {
                "built-in": "Assets/Mu3LibrarySamples",
                "urp": "Assets/Mu3LibrarySamplesURP",
                "watermelon": "Assets/Mu3LibrarySamplesWatermelonGame",
            },
            {key: spec.samples_link.as_posix() for key, spec in specs.items() if spec.samples_link},
        )

    def test_mapping_loads_without_the_samples_column(self) -> None:
        root = temporary_root(self)
        (root / "unity-cli-packages.tsv").write_text("plain\tPackage\tProject\n", encoding="utf-8")

        self.assertIsNone(load_target_specs(root)["plain"].samples_link)

    def test_mapping_rejects_a_samples_link_outside_the_project(self) -> None:
        root = temporary_root(self)
        (root / "unity-cli-packages.tsv").write_text("plain\tPackage\tProject\t../Elsewhere\n", encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "inside the repository"):
            load_target_specs(root)


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

    def test_unknown_lock_advice_fits_the_host(self) -> None:
        self.write_lock_file()

        windows = project_lock_diagnostic(self.spec, self.root, lambda _: None, system="Windows")
        linux = project_lock_diagnostic(self.spec, self.root, lambda _: None, system="Linux")

        self.assertIn("could not be opened", windows.detail)
        self.assertNotIn("lsof", windows.detail + windows.suggestion)
        self.assertIn("delete Project/Temp/UnityLockfile", windows.suggestion)
        self.assertIn("lsof", linux.detail)
        self.assertIn("Install lsof", linux.suggestion)
        self.assertIn("delete Project/Temp/UnityLockfile", linux.suggestion)

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


class SampleLinkTests(unittest.TestCase):
    """The development projects see package samples only through a link to Samples~."""

    def setUp(self) -> None:
        self.root = temporary_root(self)
        self.spec = UnityTargetSpec("sample", Path("Package"), Path("Project"), Path("Assets/Samples"))
        samples = self.root / "Package" / "Samples~"
        samples.mkdir(parents=True)
        (samples / "Sample.cs").write_text("// sample\n", encoding="utf-8")
        (self.root / "Project" / "Assets").mkdir(parents=True)
        self.link = self.root / "Project" / "Assets" / "Samples"
        # Remove the link before the temporary folder goes, so cleanup never walks through it.
        self.addCleanup(self.remove_link)

    def remove_link(self) -> None:
        # A real folder goes with the temporary folder; only a link has to be removed first.
        if sample_link_state(self.spec, self.root).state in ("linked", "misdirected"):
            remove_directory_link(self.link)

    def link_all(self) -> list[SampleLinkResult]:
        return link_samples({"sample": self.spec}, self.root)

    def test_missing_link_is_created(self) -> None:
        [result] = self.link_all()

        self.assertTrue(result.ok, result.message)
        self.assertEqual("Linked Project/Assets/Samples -> Package/Samples~.", result.message)
        self.assertTrue((self.link / "Sample.cs").is_file())
        self.assertEqual("linked", sample_link_state(self.spec, self.root).state)

    def test_correct_link_is_left_alone(self) -> None:
        self.link_all()
        before = os.lstat(self.link)

        [result] = self.link_all()

        self.assertTrue(result.ok)
        self.assertEqual("Already linked: Project/Assets/Samples -> Package/Samples~.", result.message)
        after = os.lstat(self.link)
        self.assertEqual((before.st_ino, before.st_mtime_ns), (after.st_ino, after.st_mtime_ns))

    def test_real_folder_is_not_overwritten(self) -> None:
        self.link.mkdir()
        (self.link / "Local.cs").write_text("// local\n", encoding="utf-8")

        [result] = self.link_all()

        self.assertFalse(result.ok)
        self.assertIn("is a real folder, not a link to Package/Samples~", result.message)
        self.assertIn("left untouched", result.message)
        self.assertEqual(["Local.cs"], [path.name for path in self.link.iterdir()])

    def test_link_to_another_folder_is_replaced_without_touching_that_folder(self) -> None:
        other = self.root / "Other"
        other.mkdir()
        (other / "Keep.cs").write_text("// keep\n", encoding="utf-8")
        create_directory_link(self.link, other.resolve())

        [result] = self.link_all()

        self.assertTrue(result.ok, result.message)
        self.assertEqual("Relinked Project/Assets/Samples -> Package/Samples~.", result.message)
        self.assertTrue((self.link / "Sample.cs").is_file())
        self.assertTrue((other / "Keep.cs").is_file())

    def test_dangling_link_is_replaced(self) -> None:
        gone = self.root / "Gone"
        gone.mkdir()
        create_directory_link(self.link, gone.resolve())
        gone.rmdir()

        self.assertEqual("misdirected", sample_link_state(self.spec, self.root).state)
        [result] = self.link_all()
        self.assertTrue(result.ok, result.message)
        self.assertTrue((self.link / "Sample.cs").is_file())

    def test_missing_samples_folder_is_an_error(self) -> None:
        shutil.rmtree(self.root / "Package" / "Samples~")

        [result] = self.link_all()

        self.assertFalse(result.ok)
        self.assertIn("Package/Samples~ does not exist", result.message)

    def test_doctor_fails_until_the_link_exists(self) -> None:
        missing = sample_link_diagnostic(self.spec, self.root)
        self.link_all()
        linked = sample_link_diagnostic(self.spec, self.root)

        self.assertEqual("FAIL", missing.status)
        self.assertIn("Project/Assets/Samples is missing", missing.detail)
        self.assertIn("unity link-samples", missing.suggestion)
        self.assertEqual("PASS", linked.status)

    def test_target_without_a_samples_link_is_left_out(self) -> None:
        plain = UnityTargetSpec("plain", Path("Package"), Path("Project"))

        self.assertEqual([], link_samples({"plain": plain}, self.root))
        self.assertIsNone(sample_link_diagnostic(plain, self.root))


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
        with mock.patch("mu3_cli.unity.platform.system", return_value="Windows"), mock.patch(
            "mu3_cli.unity.find_windows_bash", missing
        ):
            changes = runner.invoke(app, ["unity", "changes"])
            compile_run = runner.invoke(app, ["unity", "compile", "--dry-run"])
            test_run = runner.invoke(app, ["unity", "test", "--dry-run"])

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
    """Unity must open a project on the subst drive a command ran from, not on the long path it maps.

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
            "mu3_cli.unity.find_windows_bash", return_value=Path("bash.exe")
        ), mock.patch("mu3_cli.unity.subprocess.run", run):
            result = CliRunner().invoke(app, ["unity", "compile", "built-in", "--dry-run"])

        self.assertEqual(0, result.exit_code, result.output)
        [(command, keywords)] = calls
        self.assertIn(str(self.short_root / "compile-unity.sh"), command)
        self.assertEqual(self.short_root, keywords["cwd"])

    def test_entry_point_keeps_the_short_path_it_was_started_from(self) -> None:
        # compile-unity.sh passes $REPO_ROOT/<project> to Unity as -projectPath.
        completed = run_sourced(self.short_root / "compile-unity.sh", 'to_windows_path "$REPO_ROOT/UnityProject_BuiltIn"')

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(self.short_root / "UnityProject_BuiltIn", Path(completed.stdout.strip()))


if __name__ == "__main__":
    unittest.main()
