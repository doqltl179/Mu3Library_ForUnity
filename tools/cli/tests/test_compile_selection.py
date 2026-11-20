from __future__ import annotations

import platform
import shutil
import unittest
from pathlib import Path

from fixture_repository import CONFIGS, FixtureRepositoryTestCase

# The script checks who holds a lock file with PowerShell on Windows and lsof elsewhere.
CAN_CHECK_LOCK_HOLDERS = platform.system() == "Windows" or shutil.which("lsof") is not None


def section_entries(output: str, heading: str) -> list[tuple[str, str]]:
    """Return the `  - key: value` lines that follow a heading, up to the first other line."""
    entries: list[tuple[str, str]] = []
    in_section = False
    for line in output.splitlines():
        if line.startswith(heading):
            in_section = True
        elif in_section and line.startswith("  - "):
            key, _, value = line[4:].partition(": ")
            entries.append((key, value))
        elif in_section:
            break
    return entries


def selected_targets(output: str) -> list[str]:
    return [key for key, _ in section_entries(output, "Selected compile targets:")]


def run_plan(output: str) -> dict[str, str]:
    return dict(section_entries(output, "Run plan:"))


class ChangedSelectionTests(FixtureRepositoryTestCase):
    """Runs the real compile-unity.sh with --dry-run against a throwaway repository."""

    def dry_run_after_adding(self, relative_path: str) -> str:
        added = self.root / relative_path
        added.parent.mkdir(parents=True, exist_ok=True)
        added.write_text("// change under test\n", encoding="utf-8")
        self.addCleanup(added.unlink)

        return self.run_script("changed", "--dry-run")

    def unit_project(self, config: str) -> Path:
        """Where a run without unity_environment would generate a config's project."""
        return self.unit_folder(self.class_work_root) / config

    def write_lock_file(self, config: str) -> Path:
        lock_file = self.unit_project(config) / "Temp" / "UnityLockfile"
        lock_file.parent.mkdir(parents=True)
        lock_file.write_text("", encoding="utf-8")
        self.addCleanup(shutil.rmtree, self.class_work_root)
        return lock_file

    def run_script(self, *arguments: str) -> str:
        completed = self.run_entry_point("compile-unity.sh", *arguments)
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_shared_package_change_selects_every_config_that_names_it(self) -> None:
        output = self.dry_run_after_adding("PkgShared/Runtime/Added.cs")

        self.assertEqual(["layered", "leaf", "shared"], selected_targets(output))
        self.assertIn("  - PkgShared/Runtime/Added.cs -> layered, leaf, shared", output.splitlines())

    def test_layered_package_change_skips_configs_without_it(self) -> None:
        output = self.dry_run_after_adding("PkgLayered/Runtime/Added.cs")

        self.assertEqual(["layered", "leaf"], selected_targets(output))

    def test_leaf_package_change_selects_its_own_config(self) -> None:
        output = self.dry_run_after_adding("PkgLeaf/Runtime/Added.cs")

        self.assertEqual(["leaf"], selected_targets(output))

    def test_package_folder_must_match_the_whole_path(self) -> None:
        # extra names only PkgSharedExtra, whose folder merely starts with PkgShared.
        output = self.dry_run_after_adding("PkgShared/Runtime/Added.cs")

        self.assertNotIn("extra", selected_targets(output))

    def test_package_with_longer_name_selects_its_own_config(self) -> None:
        output = self.dry_run_after_adding("PkgSharedExtra/Runtime/Added.cs")

        self.assertEqual(["extra"], selected_targets(output))

    def test_source_outside_packages_and_configs_selects_every_target(self) -> None:
        output = self.dry_run_after_adding("Loose.cs")

        self.assertEqual(list(CONFIGS), selected_targets(output))
        self.assertIn("  - Loose.cs -> all", output.splitlines())

    def test_config_change_selects_only_that_config(self) -> None:
        output = self.dry_run_after_adding("tools/unity-projects/layered/ProjectSettings/Added.asset")

        self.assertEqual(["layered"], selected_targets(output))
        self.assertIn("  - tools/unity-projects/layered/ProjectSettings/Added.asset -> layered", output.splitlines())

    def test_package_documentation_and_other_tooling_select_nothing(self) -> None:
        for path in ("PkgShared/README.md", "tools/cli/src/Added.py"):
            with self.subTest(path=path):
                output = self.dry_run_after_adding(path)

                self.assertEqual([], selected_targets(output))
                self.assertIn("Selected compile targets: none", output.splitlines())

    def test_dry_run_names_the_project_folder_under_the_unit_and_creates_nothing(self) -> None:
        output = self.run_script("all", "--dry-run")

        self.assertEqual({config: f"generate {self.unit_project(config).as_posix()}" for config in CONFIGS}, run_plan(output))
        self.assertFalse(self.class_work_root.exists())

    def test_dry_run_names_a_project_an_earlier_run_generated_as_refreshed(self) -> None:
        self.unit_project("leaf").mkdir(parents=True)
        self.addCleanup(shutil.rmtree, self.class_work_root)

        output = self.run_script("leaf", "--dry-run")

        self.assertEqual({"leaf": f"refresh {self.unit_project('leaf').as_posix()}"}, run_plan(output))

    def test_work_dir_holds_the_projects_instead(self) -> None:
        work_dir = self.root.parent / "elsewhere"

        output = self.run_script("leaf", "--dry-run", "--work-dir", str(work_dir))

        self.assertEqual({"leaf": f"generate {(work_dir / 'leaf').as_posix()}"}, run_plan(output))
        self.assertFalse(work_dir.exists())

    def test_editor_version_gets_a_project_of_its_own(self) -> None:
        output = self.run_script("leaf", "--dry-run", "--editor-version", "6000.3.17f1")

        self.assertEqual({"leaf": f"generate {self.unit_project('leaf').as_posix()}-6000.3.17f1"}, run_plan(output))

    def test_without_a_work_root_a_run_says_what_to_set(self) -> None:
        environment = self._environment()
        del environment["MU3_WORK_ROOT"]
        for arguments in (("leaf", "--dry-run"), ("leaf",)):
            with self.subTest(arguments=arguments):
                completed = self.run_entry_point("compile-unity.sh", *arguments, environment=environment)

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn("set the user environment variable MU3_WORK_ROOT", completed.stderr)
                self.assertIn("or pass --work-dir <folder>", completed.stderr)

        # A selection of nothing needs no work folder.
        completed = self.run_entry_point("compile-unity.sh", "changed", "--dry-run", environment=environment)
        self.assertEqual(0, completed.returncode, completed.stderr)

    def test_on_a_detached_head_a_run_asks_for_a_branch_or_a_work_dir(self) -> None:
        self._git("checkout", "--quiet", "--detach")
        self.addCleanup(self._git, "checkout", "--quiet", "main")

        completed = self.run_entry_point("compile-unity.sh", "leaf", "--dry-run")

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("names no branch", completed.stderr)
        self.assertIn("check out a branch or pass --work-dir <folder>", completed.stderr)

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_stale_lock_file_is_ignored_with_a_note(self) -> None:
        self.write_lock_file("leaf")
        folder = self.unit_project("leaf").as_posix()

        output = self.run_script("leaf", "--dry-run")

        self.assertEqual(
            f"refresh {folder}, ignoring a stale {folder}/Temp/UnityLockfile that no running process holds",
            run_plan(output)["leaf"],
        )

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_project_whose_lock_is_held_cannot_run(self) -> None:
        lock_file = self.write_lock_file("leaf")

        with lock_file.open("rb"):
            output = self.run_script("leaf", "--dry-run")
            completed = self.run_entry_point(
                "compile-unity.sh", "leaf", environment=self.unity_environment(MU3_WORK_ROOT=str(self.class_work_root))
            )

        self.assertEqual(
            f"cannot run: {self.unit_project('leaf').as_posix()} is open in Unity; close it, since the run refreshes the project's files",
            run_plan(output)["leaf"],
        )
        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("preflight failed; no projects were compiled", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)

    def test_refusal_of_an_unchecked_lock_says_what_helps(self) -> None:
        # Closing an Editor does not help when nothing can tell whether one holds the lock.
        self.write_lock_file("leaf")
        phrases = {}
        for platform_name in ("windows", "linux"):
            completed = self.run_functions(
                "compile-unity.sh",
                "file_holder_state() { printf 'unknown\\n'; }\n"
                f"host_platform() {{ printf '{platform_name}\\n'; }}\n"
                "load_project_configs\nproject_plan_phrase leaf",
            )
            self.assertEqual(0, completed.returncode, completed.stderr)
            phrases[platform_name] = completed.stdout.strip()

        for phrase in phrases.values():
            self.assertTrue(phrase.startswith(f"cannot run: {self.unit_project('leaf').as_posix()}/Temp/UnityLockfile exists"), phrase)
            self.assertIn("delete the file if no Editor has the project open", phrase)
            self.assertNotIn("close it", phrase)
        self.assertIn("PowerShell could not open it", phrases["windows"])
        self.assertNotIn("lsof", phrases["windows"])
        self.assertIn("install lsof", phrases["linux"])


if __name__ == "__main__":
    unittest.main()
