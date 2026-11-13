from __future__ import annotations

import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from typer.testing import CliRunner

from fixture_repository import FixtureRepositoryTestCase
from mu3_cli import repository
from mu3_cli.cli import app
from mu3_cli.repository import (
    STALE_AGENT_REFERENCE_PARTS,
    agentkit_cli_path,
    is_generated_tool_artifact,
    package_code_issues,
    package_code_paths,
    repo_root,
    repository_hygiene_issues,
    repository_text_paths,
    tracked_tool_paths,
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

    # Built from code points and bytes, never typed: an editing tool can turn a typed escape into
    # the character, and a lossy one turns an unpaired surrogate into the replacement character.
    REPLACEMENT = chr(0xFFFD)
    UNPAIRED_SURROGATE = bytes([0xED, 0xA0, 0x80])  # U+D800 written out as if it were a character

    def test_replacement_character_is_reported_with_its_line(self) -> None:
        path = self.write(f"// first\r\nstring s = \"{self.REPLACEMENT}\";\r\n".encode("utf-8"))

        self.assertEqual(
            [
                "replacement character U+FFFD in package code, which a lossy conversion leaves where it "
                f"could not carry a character, write the character meant as a \\u escape: {Path(path)}:2"
            ],
            package_code_issues(self.root, [path]),
        )

    def test_replacement_character_written_as_an_escape_passes(self) -> None:
        escape = "\\u" + format(ord(self.REPLACEMENT), "04X")
        path = self.write(f"string s = \"{escape}\";\n".encode("utf-8"))

        self.assertEqual("\\uFFFD", escape)
        self.assertEqual([], package_code_issues(self.root, [path]))

    def test_unpaired_surrogate_bytes_are_reported_as_what_the_file_may_hold(self) -> None:
        path = self.write(b"// first\nstring s = \"" + self.UNPAIRED_SURROGATE + b"\";\n")

        [issue] = package_code_issues(self.root, [path])

        self.assertTrue(issue.startswith(f"could not inspect package code file: {Path(path)} ("), issue)
        self.assertIn("invalid continuation byte", issue)
        self.assertTrue(
            issue.endswith(
                "; line 2 is not UTF-8, and its bytes may be an unpaired surrogate (ED A0 80 to ED BF BF), "
                "which package code writes as a \\u escape"
            ),
            issue,
        )

    def test_line_of_bytes_that_are_not_utf8_counts_past_a_byte_order_mark(self) -> None:
        path = self.write(b"\xef\xbb\xbf// first\n// second\nchar c = '\xff';\n")

        [issue] = package_code_issues(self.root, [path])

        self.assertIn("; line 3 is not UTF-8", issue)

    def test_package_code_paths_lists_the_package_csharp_files(self) -> None:
        paths = package_code_paths(repo_root())

        self.assertIn("Mu3Library_Base/Runtime/Scripts/Multiplayer/Local/LobbyFilter.cs", paths)
        self.assertTrue(all(path.startswith("Mu3Library_") and path.endswith(".cs") for path in paths), paths)


# Caf\u00e9, a name with a character outside ASCII.
NON_ASCII_NAME = "Caf\u00e9"


class NonAsciiPathTests(FixtureRepositoryTestCase):
    """Git quotes a path that holds a character outside ASCII, and escapes that character's bytes,
    unless it is told not to. Read that way, the path ends in a quote, so its suffix no longer
    matches, and repo check would pass over the file without a word."""

    # A package file Git tracks and one it does not, which git ls-files lists in separate passes.
    PACKAGE_CODE = {
        f"Mu3Library_X/Runtime/{NON_ASCII_NAME}.cs": True,
        f"Mu3Library_X/Editor/{NON_ASCII_NAME}.cs": False,
    }
    REPOSITORY_TEXT = f"docs/{NON_ASCII_NAME}.md"
    TOOL_ARTIFACT = f"tools/obj/{NON_ASCII_NAME}.json"

    @classmethod
    def setUpClass(cls) -> None:
        super().setUpClass()
        # Git's default, set in the fixture itself so that no configuration of the user's turns it off.
        cls._git("config", "core.quotepath", "true")
        for relative_path in cls.PACKAGE_CODE:
            cls.write(relative_path, "// fixture\nchar c = '\u200b';\n")
        cls.write(cls.REPOSITORY_TEXT, f"See {STALE_AGENT_REFERENCE_PARTS[0]}.\n")
        cls.write(cls.TOOL_ARTIFACT, "{}\n")
        tracked = [path for path, is_tracked in cls.PACKAGE_CODE.items() if is_tracked]
        cls._git("add", "--", *tracked, cls.TOOL_ARTIFACT)

    @classmethod
    def write(cls, relative_path: str, text: str) -> None:
        path = cls.root / relative_path
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")

    def setUp(self) -> None:
        super().setUp()
        # The listings run git in this process's environment, for which the fixture's stands in.
        environment = mock.patch.dict(os.environ, self._environment(), clear=True)
        environment.start()
        self.addCleanup(environment.stop)

    def test_every_listing_names_the_file_as_it_is_spelled(self) -> None:
        package_code = package_code_paths(self.root)
        for relative_path in self.PACKAGE_CODE:
            self.assertIn(relative_path, package_code)
        self.assertIn(self.REPOSITORY_TEXT, repository_text_paths(self.root))
        self.assertIn(self.TOOL_ARTIFACT, tracked_tool_paths(self.root))

    def test_repo_check_inspects_each_listed_file(self) -> None:
        issues = repository_hygiene_issues(self.root)

        for relative_path in self.PACKAGE_CODE:
            self.assertIn(
                "invisible or private-use character U+200B in package code, "
                f"write it as a \\u escape: {Path(relative_path)}:2",
                issues,
            )
        self.assertIn(f"stale AI-agent routing reference: {Path(self.REPOSITORY_TEXT)}:1", issues)
        self.assertIn(f"generated tooling artifact is tracked: {self.TOOL_ARTIFACT}", issues)


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
