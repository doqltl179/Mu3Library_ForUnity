from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from unittest import mock

from typer.testing import CliRunner

from mu3_cli import repository
from mu3_cli.cli import app
from mu3_cli.repository import (
    agentkit_cli_path,
    is_generated_tool_artifact,
    package_code_issues,
    package_code_paths,
    repo_root,
    repository_hygiene_issues,
)
from mu3_cli.unity import create_directory_link, remove_directory_link

MODULE_PATH = Path("tools", "cli", "src", "mu3_cli", "repository.py")


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


class PackageCodeCharacterTests(unittest.TestCase):
    """Package code writes a character a reader cannot see as a \\u escape, never as the character."""

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)

    def write(self, data: bytes) -> str:
        relative_path = "Mu3Library_Base/Runtime/Scripts/Code.cs"
        path = self.root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return relative_path

    def test_hidden_character_is_reported_with_its_line(self) -> None:
        hidden = {
            "\ue000": "U+E000",  # private use, the sort key LobbyFilter once held as the character
            "\u200b": "U+200B",  # zero-width space
            "\u202e": "U+202E",  # right-to-left override
            "\ufeff": "U+FEFF",  # byte order mark past the start of the file
            "\u0007": "U+0007",  # bell
            "\u2028": "U+2028",  # line separator, which str.splitlines() would split at
        }
        for character, code_point in hidden.items():
            with self.subTest(code_point=code_point):
                path = self.write(f"// first\r\nchar c = '{character}';\r\n".encode("utf-8"))

                self.assertEqual(
                    [
                        f"invisible or private-use character {code_point} in package code, "
                        f"write it as a \\u escape: {Path(path)}:2"
                    ],
                    package_code_issues(self.root, [path]),
                )

    def test_text_a_reader_can_see_passes(self) -> None:
        # A leading byte order mark, tabs, CRLF, Korean, Japanese, an accented and a full-width
        # letter, and the escape that stands for a private-use character.
        text = "\ufeff// \ud55c\uad6d\uc5b4 \u65e5\u672c\u8a9e \u00e9 \uff21\r\n\tchar c = '\\uE000';\r\n"
        path = self.write(text.encode("utf-8"))

        self.assertEqual([], package_code_issues(self.root, [path]))

    def test_file_that_is_not_utf8_is_reported(self) -> None:
        path = self.write(b"char c = '\xff';\n")

        issues = package_code_issues(self.root, [path])

        self.assertEqual(1, len(issues), issues)
        self.assertTrue(issues[0].startswith(f"could not inspect package code file: {Path(path)} ("), issues)

    def test_package_code_paths_lists_the_package_csharp_files(self) -> None:
        paths = package_code_paths(repo_root())

        self.assertIn("Mu3Library_Base/Runtime/Scripts/Multiplayer/Local/LobbyFilter.cs", paths)
        self.assertTrue(all(path.startswith("Mu3Library_") and path.endswith(".cs") for path in paths), paths)


class RepositoryRootSpellingTests(unittest.TestCase):
    """A subst drive reaches the checkout through a short path that Path.resolve() undoes on Windows.

    A link to the checkout stands in for the drive: resolve() follows it back to the long path too.
    """

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.temporary = Path(directory.name)
        self.real_root = Path(repository.__file__).resolve().parents[len(MODULE_PATH.parts) - 1]
        self.short_root = self.temporary / "short"
        create_directory_link(self.short_root, self.real_root)
        # Remove the link before the temporary folder goes, so cleanup never walks through it.
        self.addCleanup(remove_directory_link, self.short_root)

    def root_for(self, module_root: Path, working_directory: Path) -> Path:
        with mock.patch.object(repository, "__file__", str(module_root / MODULE_PATH)), mock.patch.object(
            Path, "cwd", return_value=working_directory
        ):
            return repo_root()

    def test_root_keeps_the_short_path_the_module_was_imported_from(self) -> None:
        # PYTHONPATH=tools/cli/src python -m mu3_cli, run from the short path.
        self.assertEqual(self.short_root, self.root_for(self.short_root, self.temporary))

    def test_working_directory_in_the_same_checkout_supplies_its_short_path(self) -> None:
        # An installed mu3-cli keeps the long path it was installed from, but runs from the short path.
        working_directory = self.short_root / "UnityProject_BuiltIn"

        self.assertEqual(self.short_root, self.root_for(self.real_root, working_directory))

    def test_working_directory_in_another_checkout_leaves_the_root_alone(self) -> None:
        other = self.temporary / "other"
        (other / ".github").mkdir(parents=True)
        (other / "AGENTS.md").write_text("# Other checkout\n", encoding="utf-8")

        self.assertEqual(self.short_root, self.root_for(self.short_root, other))

    def test_deleted_working_directory_leaves_the_module_spelling(self) -> None:
        with mock.patch.object(repository, "__file__", str(self.short_root / MODULE_PATH)), mock.patch.object(
            Path, "cwd", side_effect=FileNotFoundError("deleted")
        ):
            self.assertEqual(self.short_root, repo_root())


if __name__ == "__main__":
    unittest.main()
