from __future__ import annotations

import unittest

from mu3_cli.repository import is_generated_tool_artifact, repo_root, repository_hygiene_issues


class RepositoryHygieneTests(unittest.TestCase):
    def test_current_repository_hygiene_is_valid(self) -> None:
        self.assertEqual([], repository_hygiene_issues(repo_root()))

    def test_generated_tool_artifacts_are_detected(self) -> None:
        self.assertTrue(is_generated_tool_artifact("tools/cli/build/obj/generated.json"))
        self.assertTrue(is_generated_tool_artifact("tools/cli/src/mu3_cli.egg-info/PKG-INFO"))
        self.assertFalse(is_generated_tool_artifact("tools/cli/src/mu3_cli/repository.py"))


if __name__ == "__main__":
    unittest.main()
