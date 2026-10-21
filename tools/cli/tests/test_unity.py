from __future__ import annotations

import platform
import shutil
import tempfile
import unittest
from pathlib import Path

from mu3_cli.repository import repo_root
from mu3_cli.unity import (
    UnityTargetSpec,
    compile_command_arguments,
    file_is_held_open,
    load_target_specs,
    project_editor_version,
    project_lock_diagnostic,
    project_lock_state,
)


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


if __name__ == "__main__":
    unittest.main()
