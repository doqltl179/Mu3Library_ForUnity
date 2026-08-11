from __future__ import annotations

import unittest

from mu3_cli.csdevkit import run_drift_checks
from mu3_cli.repository import repo_root
from mu3_cli.unity import compile_command_arguments, load_target_specs, project_editor_version


class UnityToolingTests(unittest.TestCase):
    def test_repository_mapping_loads_all_targets(self) -> None:
        specs = load_target_specs(repo_root())

        self.assertEqual(["built-in", "urp", "watermelon"], list(specs))
        self.assertEqual("Mu3Library_Base", specs["built-in"].package_path.as_posix())
        self.assertEqual("UnityProject_URP", specs["urp"].project_path.as_posix())

    def test_every_mapping_has_an_editor_version(self) -> None:
        root = repo_root()
        specs = load_target_specs(root)

        self.assertEqual(
            {"built-in": "6000.3.10f1", "urp": "6000.3.10f1", "watermelon": "6000.3.17f1"},
            {key: project_editor_version(spec, root) for key, spec in specs.items()},
        )

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

    def test_drift_checks_cover_canonical_package_identities(self) -> None:
        identity_results = [
            result for result in run_drift_checks(repo_root()) if result.title.startswith("Package identity (")
        ]

        self.assertEqual(2, len(identity_results))
        self.assertEqual(["PASS", "PASS"], [result.status for result in identity_results])


if __name__ == "__main__":
    unittest.main()
