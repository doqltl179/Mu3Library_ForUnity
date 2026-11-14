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
    outside_checkout_refusal,
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

    def test_bytes_of_another_encoding_are_reported_as_a_file_to_save_as_utf8(self) -> None:
        korean = "".join(map(chr, (0xD55C, 0xAD6D, 0xC5B4)))
        cases = {
            "latin-1": "caf".encode("ascii") + bytes([0xE9]),
            "cp949": korean.encode("cp949"),
            "cut short": bytes([0xE2, 0x82]),
            # ED followed by a byte outside A0 to BF starts no surrogate.
            "ED without a surrogate": bytes([0xED, 0x41]),
        }
        for name, data in cases.items():
            with self.subTest(name):
                path = self.write(b"// first\n// " + data)

                [issue] = package_code_issues(self.root, [path])

                self.assertTrue(
                    issue.endswith(
                        "; line 2 is not UTF-8, as text saved in another encoding, such as Korean saved as CP949, "
                        "is not; save the file as UTF-8"
                    ),
                    issue,
                )
                self.assertNotIn("surrogate", issue)

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


class OutsideCheckoutRefusalTests(unittest.TestCase):
    """An installed mu3-cli acts on the checkout it was installed from wherever it runs, so the
    commands that change files there refuse a working directory outside that checkout (#416)."""

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.temporary = Path(directory.name)
        self.main = self.make_checkout(self.temporary / "main")
        # A worktree inside the main checkout's folder, as a session worktree under .claude/worktrees/ is.
        self.nested = self.make_checkout(self.main / ".claude" / "worktrees" / "session")
        self.other = self.make_checkout(self.temporary / "other")

    @staticmethod
    def make_checkout(root: Path) -> Path:
        (root / ".github").mkdir(parents=True)
        (root / "AGENTS.md").write_text("# Checkout\n", encoding="utf-8")
        (root / "UnityProject_BuiltIn").mkdir()
        return root

    @staticmethod
    def refusal(root: Path, working_directory: Path) -> str | None:
        with mock.patch.object(Path, "cwd", return_value=working_directory):
            return outside_checkout_refusal(root, "unity link-samples")

    def test_working_directory_inside_the_checkout_is_accepted(self) -> None:
        for working_directory in (self.main, self.main / "UnityProject_BuiltIn"):
            with self.subTest(working_directory=working_directory):
                self.assertIsNone(self.refusal(self.main, working_directory))

    def test_short_path_to_the_same_checkout_is_inside_it(self) -> None:
        # A link stands in for a subst drive: resolve() follows both back to the long path (#82).
        short = self.temporary / "short"
        create_directory_link(short, self.main.resolve())
        self.addCleanup(remove_directory_link, short)

        self.assertIsNone(self.refusal(self.main, short / "UnityProject_BuiltIn"))
        self.assertIsNone(self.refusal(short, self.main / "UnityProject_BuiltIn"))

    def test_another_checkout_is_refused_with_both_folders_named(self) -> None:
        working_directory = self.other / "UnityProject_BuiltIn"

        self.assertEqual(
            f"mu3-cli unity link-samples would change the checkout at {self.main}, but the working directory "
            f"{working_directory} is outside it, in the checkout at {self.other}, so nothing was changed. "
            f"Run it from inside {self.main}, or, to change the checkout at {self.other}, run "
            "PYTHONPATH=tools/cli/src python -m mu3_cli unity link-samples from that checkout's root.",
            self.refusal(self.main, working_directory),
        )

    def test_checkout_nested_in_the_folder_of_another_is_outside_it(self) -> None:
        working_directory = self.nested / "UnityProject_BuiltIn"

        refusal = self.refusal(self.main, working_directory)

        self.assertIsNotNone(refusal)
        self.assertIn(
            f"the working directory {working_directory} is outside it, in the checkout at {self.nested},", refusal
        )
        self.assertIsNone(self.refusal(self.nested, working_directory))

    def test_folder_in_no_checkout_is_refused_without_naming_another(self) -> None:
        self.assertEqual(
            f"mu3-cli unity link-samples would change the checkout at {self.main}, but the working directory "
            f"{self.temporary} is outside it and in no checkout, so nothing was changed. "
            "Run it from inside that checkout.",
            self.refusal(self.main, self.temporary),
        )

    def test_folder_a_failed_removal_left_in_a_worktree_folder_of_the_checkout_is_refused(self) -> None:
        # git worktree remove deletes AGENTS.md and .github before it stops at a junction, so nothing
        # marks the folder it leaves as a checkout of its own; run there, the command means its links (#436).
        for worktree_folder in (self.main / ".claude" / "worktrees", self.main / ".worktrees"):
            left_behind = worktree_folder / "removed"
            (left_behind / "UnityProject_BuiltIn").mkdir(parents=True)
            for working_directory in (left_behind, left_behind / "UnityProject_BuiltIn"):
                with self.subTest(working_directory=working_directory):
                    self.assertEqual(
                        f"mu3-cli unity link-samples would change the checkout at {self.main}, but the working "
                        f"directory {working_directory} is in {left_behind}, which is in that checkout's worktree "
                        f"folder {worktree_folder} but is no checkout of its own, such as a folder that a failed "
                        "git worktree remove left behind, so nothing was changed. To remove that folder's links, "
                        'see "Worktree Removal" in tools/README.md. To change that checkout, run it from inside '
                        "it, outside its worktree folders.",
                        self.refusal(self.main, working_directory),
                    )

    def test_left_folder_reached_by_a_short_path_to_the_checkout_is_named_by_that_path(self) -> None:
        # A link stands in for a subst drive, as in test_short_path_to_the_same_checkout_is_inside_it.
        (self.main / ".worktrees" / "removed" / "UnityProject_BuiltIn").mkdir(parents=True)
        short = self.temporary / "short"
        create_directory_link(short, self.main.resolve())
        self.addCleanup(remove_directory_link, short)

        refusal = self.refusal(self.main, short / ".worktrees" / "removed" / "UnityProject_BuiltIn")

        self.assertIsNotNone(refusal)
        self.assertIn(
            f" is in {short / '.worktrees' / 'removed'}, which is in that checkout's worktree folder ", refusal
        )

    def test_left_folder_in_a_worktree_folder_that_is_a_link_is_refused(self) -> None:
        # A worktree folder, or .claude above one, may be a link to a folder elsewhere, as on another drive.
        checkout = self.make_checkout(self.temporary / "linked")
        for link, target, worktree_folder in (
            (checkout / ".worktrees", self.temporary / "worktrees-elsewhere", checkout / ".worktrees"),
            (checkout / ".claude", self.temporary / "claude-elsewhere", checkout / ".claude" / "worktrees"),
        ):
            with self.subTest(link=link):
                target.mkdir()
                try:
                    create_directory_link(link, target.resolve())
                except OSError as error:
                    self.skipTest(f"a directory link cannot be made here: {error}")
                self.addCleanup(remove_directory_link, link)
                left_behind = worktree_folder / "removed"
                (left_behind / "UnityProject_BuiltIn").mkdir(parents=True)

                refusal = self.refusal(checkout, left_behind / "UnityProject_BuiltIn")

                self.assertIsNotNone(refusal)
                self.assertIn(
                    f" is in {left_behind}, which is in that checkout's worktree folder {worktree_folder} ", refusal
                )

    def test_worktree_folders_themselves_and_other_folders_beside_them_are_inside_the_checkout(self) -> None:
        (self.main / ".worktrees").mkdir()
        (self.main / ".claude" / "settings").mkdir()
        for working_directory in (
            self.main / ".worktrees",
            self.main / ".claude" / "worktrees",
            self.main / ".claude",
            self.main / ".claude" / "settings",
        ):
            with self.subTest(working_directory=working_directory):
                self.assertIsNone(self.refusal(self.main, working_directory))

    def test_checkout_in_a_worktree_folder_of_another_is_its_own_checkout(self) -> None:
        # A unit worktree under .worktrees/ keeps AGENTS.md and .github, so it is no left folder.
        for checkout in (self.nested, self.make_checkout(self.main / ".worktrees" / "unit")):
            with self.subTest(checkout=checkout):
                for working_directory in (checkout, checkout / "UnityProject_BuiltIn"):
                    self.assertIsNone(self.refusal(checkout, working_directory))
                self.assertIn(
                    f"is outside it, in the checkout at {checkout},",
                    self.refusal(self.main, checkout / "UnityProject_BuiltIn"),
                )
                # Its own worktree folders hold folders that it refuses in turn.
                left_behind = checkout / ".worktrees" / "removed"
                left_behind.mkdir(parents=True)
                self.assertIn(f" is in {left_behind}, ", self.refusal(checkout, left_behind))

    def test_this_checkout_is_inside_itself_wherever_it_lies(self) -> None:
        # The checkout the tests run from may itself be a worktree under another checkout's .worktrees/.
        root = repo_root()

        self.assertIsNone(self.refusal(root, root))
        self.assertIsNone(self.refusal(root, root / "tools" / "cli"))

    def test_unreadable_working_directory_is_refused(self) -> None:
        with mock.patch.object(Path, "cwd", side_effect=FileNotFoundError(2, "deleted")):
            refusal = outside_checkout_refusal(self.main, "unity unlink-samples")

        self.assertTrue(
            refusal.startswith(
                f"mu3-cli unity unlink-samples would change the checkout at {self.main}, "
                "but the working directory cannot be read ("
            ),
            refusal,
        )


if __name__ == "__main__":
    unittest.main()
