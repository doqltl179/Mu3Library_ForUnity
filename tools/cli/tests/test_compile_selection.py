from __future__ import annotations

import platform
import shutil
import unittest
from pathlib import Path

from fixture_repository import MAPPING, FixtureRepositoryTestCase

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

    def write_lock_file(self, project: str) -> Path:
        lock_file = self.root / project / "Temp" / "UnityLockfile"
        lock_file.parent.mkdir(parents=True)
        lock_file.write_text("", encoding="utf-8")
        self.addCleanup(shutil.rmtree, lock_file.parent)
        return lock_file

    def run_script(self, *arguments: str) -> str:
        completed = self.run_entry_point("compile-unity.sh", *arguments)
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_shared_package_change_selects_every_consuming_project(self) -> None:
        output = self.dry_run_after_adding("PkgShared/Runtime/Added.cs")

        self.assertEqual(["shared", "layered", "leaf"], selected_targets(output))
        self.assertIn("  - PkgShared/Runtime/Added.cs -> shared, layered, leaf", output.splitlines())

    def test_layered_package_change_skips_projects_without_it(self) -> None:
        output = self.dry_run_after_adding("PkgLayered/Runtime/Added.cs")

        self.assertEqual(["layered", "leaf"], selected_targets(output))

    def test_leaf_package_change_selects_its_own_project(self) -> None:
        output = self.dry_run_after_adding("PkgLeaf/Runtime/Added.cs")

        self.assertEqual(["leaf"], selected_targets(output))

    def test_package_reference_must_match_the_whole_path(self) -> None:
        # ProjExtra references only PkgSharedExtra, whose path merely starts with PkgShared.
        output = self.dry_run_after_adding("PkgShared/Runtime/Added.cs")

        self.assertNotIn("extra", selected_targets(output))

    def test_package_with_longer_name_selects_its_own_project(self) -> None:
        output = self.dry_run_after_adding("PkgSharedExtra/Runtime/Added.cs")

        self.assertEqual(["extra"], selected_targets(output))

    def test_mapped_project_is_selected_without_a_manifest_reference(self) -> None:
        output = self.dry_run_after_adding("PkgOrphan/Runtime/Added.cs")

        self.assertEqual(["orphan"], selected_targets(output))

    def test_source_outside_packages_and_projects_selects_every_target(self) -> None:
        output = self.dry_run_after_adding("Loose.cs")

        self.assertEqual([key for key, _, _ in MAPPING], selected_targets(output))
        self.assertIn("  - Loose.cs -> all", output.splitlines())

    def test_project_change_selects_only_that_project(self) -> None:
        output = self.dry_run_after_adding("ProjLayered/Assets/Added.cs")

        self.assertEqual(["layered"], selected_targets(output))

    def test_package_documentation_change_selects_nothing(self) -> None:
        output = self.dry_run_after_adding("PkgShared/README.md")

        self.assertEqual([], selected_targets(output))
        self.assertIn("Selected compile targets: none", output.splitlines())

    def test_closed_project_runs_in_place(self) -> None:
        output = self.run_script("leaf", "--dry-run")

        self.assertEqual({"leaf": "in place"}, run_plan(output))

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_stale_lock_file_runs_in_place(self) -> None:
        self.write_lock_file("ProjLeaf")

        output = self.run_script("leaf", "--dry-run")

        self.assertEqual(
            "in place, ignoring a stale ProjLeaf/Temp/UnityLockfile that no running process holds",
            run_plan(output)["leaf"],
        )

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_held_lock_file_runs_in_an_isolated_mirror(self) -> None:
        lock_file = self.write_lock_file("ProjLeaf")

        with lock_file.open("rb"):
            output = self.run_script("leaf", "--dry-run")

        self.assertEqual("isolated mirror, because ProjLeaf is open in Unity", run_plan(output)["leaf"])

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_in_place_refuses_a_project_whose_lock_is_held(self) -> None:
        lock_file = self.write_lock_file("ProjLeaf")

        with lock_file.open("rb"):
            output = self.run_script("leaf", "--in-place", "--dry-run")

        self.assertTrue(run_plan(output)["leaf"].startswith("cannot run: ProjLeaf is open in Unity;"), output)


if __name__ == "__main__":
    unittest.main()
