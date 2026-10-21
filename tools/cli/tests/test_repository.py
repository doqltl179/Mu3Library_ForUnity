from __future__ import annotations

import unittest

from typer.testing import CliRunner

from mu3_cli.cli import app
from mu3_cli.repository import agentkit_cli_path, is_generated_tool_artifact, repo_root, repository_hygiene_issues


class RepositoryHygieneTests(unittest.TestCase):
    def test_current_repository_hygiene_is_valid(self) -> None:
        self.assertEqual([], repository_hygiene_issues(repo_root()))

    def test_generated_tool_artifacts_are_detected(self) -> None:
        self.assertTrue(is_generated_tool_artifact("tools/cli/build/obj/generated.json"))
        self.assertTrue(is_generated_tool_artifact("tools/cli/src/mu3_cli.egg-info/PKG-INFO"))
        self.assertFalse(is_generated_tool_artifact("tools/cli/src/mu3_cli/repository.py"))

    def test_agentkit_is_installed(self) -> None:
        self.assertTrue(agentkit_cli_path(repo_root()).is_file())

    def test_repo_info_lists_every_mapped_package(self) -> None:
        result = CliRunner().invoke(app, ["repo", "info"])

        self.assertEqual(0, result.exit_code, result.output)
        package_lines = [line for line in result.output.splitlines() if line.startswith("Package (")]
        self.assertEqual(3, len(package_lines), result.output)
        for package in ("Mu3Library_Base", "Mu3Library_URP", "Mu3Library_Game_WatermelonGame"):
            self.assertIn(str(repo_root() / package), result.output)


if __name__ == "__main__":
    unittest.main()
