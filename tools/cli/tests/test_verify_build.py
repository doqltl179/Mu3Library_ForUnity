from __future__ import annotations

import difflib
import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from typer.testing import CliRunner

from common.checkout import repo_root
from common.unity_projects import is_directory_link
from verify_build.cli import (
    BUILD_SCRIPT,
    EXIT_BUILD_FAILED,
    PLAYER_CONTENT,
    PROJECT_BUILD_SCRIPT,
    SETTINGS_ASSET,
    WORK_DIR_MARKER,
    SettingsFormatError,
    VerifyOptions,
    VerifySetupError,
    app,
    content_mode_problem,
    override_player_build_option,
    override_project_names,
    run_verify_build,
    scene_problem,
)


FULL = Path("tools", "unity-projects", "full")
# The lines around the one the tool changes in the full config's AddressableAssetSettings.asset.
SAMPLE_SETTINGS = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_Name: AddressableAssetSettings
  m_BuildRemoteCatalog: 0
  m_ContentStateBuildPath:
  m_BuildAddressablesWithPlayerBuild: 0
  m_overridePlayerVersion: '[UnityEditor.PlayerSettings.bundleVersion]'
  m_ActiveProfileId: 41d6783c029feb84db0438f9bc4051e7
"""

SAMPLE_PROJECT_SETTINGS = """%YAML 1.1
--- !u!129 &1
PlayerSettings:
  productGUID: 0123456789abcdef0123456789abcdef
  companyName: DefaultCompany
  productName: Mu3Library
  applicationIdentifier:
    Standalone: com.DefaultCompany.Mu3Library
"""


def changed_lines(before: str, after: str) -> list[tuple[str, str]]:
    """Each line that changed, as (before, after), for edits that keep the line count."""
    before_lines, after_lines = before.splitlines(), after.splitlines()
    matcher = difflib.SequenceMatcher(a=before_lines, b=after_lines, autojunk=False)
    pairs: list[tuple[str, str]] = []
    for tag, a_start, a_end, b_start, b_end in matcher.get_opcodes():
        if tag == "equal":
            continue
        if tag != "replace" or a_end - a_start != b_end - b_start:
            raise AssertionError(f"lines were added or removed: {tag} {before_lines[a_start:a_end]} -> {after_lines[b_start:b_end]}")
        pairs += list(zip(before_lines[a_start:a_end], after_lines[b_start:b_end]))
    return pairs


class AddressablesSettingsOverrideTests(unittest.TestCase):
    def test_each_mode_sets_the_player_build_option_only(self) -> None:
        for content, value in (("build", "1"), ("none", "2")):
            with self.subTest(content=content):
                self.assertEqual(
                    [("  m_BuildAddressablesWithPlayerBuild: 0", f"  m_BuildAddressablesWithPlayerBuild: {value}")],
                    changed_lines(SAMPLE_SETTINGS, override_player_build_option(SAMPLE_SETTINGS, content)),
                )

    def test_keeps_crlf_line_endings(self) -> None:
        crlf = SAMPLE_SETTINGS.replace("\n", "\r\n")

        self.assertIn("  m_BuildAddressablesWithPlayerBuild: 1\r\n", override_player_build_option(crlf, "build"))

    def test_refuses_a_file_with_two_player_build_options(self) -> None:
        doubled = SAMPLE_SETTINGS + "  m_BuildAddressablesWithPlayerBuild: 0\n"

        with self.assertRaisesRegex(SettingsFormatError, "2 m_BuildAddressablesWithPlayerBuild lines, not one"):
            override_player_build_option(doubled, "build")

    def test_the_full_config_s_asset_changes_in_one_line(self) -> None:
        tracked = (repo_root() / FULL / SETTINGS_ASSET).read_text(encoding="utf-8")

        pairs = changed_lines(tracked, override_player_build_option(tracked, "none"))

        self.assertEqual(["  m_BuildAddressablesWithPlayerBuild: 2"], [after for _, after in pairs])


class ProjectNameTests(unittest.TestCase):
    def test_sets_the_verification_company_and_product(self) -> None:
        after = override_project_names(SAMPLE_PROJECT_SETTINGS)

        self.assertEqual(
            [("  companyName: DefaultCompany", "  companyName: Mu3LibraryVerify"), ("  productName: Mu3Library", "  productName: VerifyBuild")],
            changed_lines(SAMPLE_PROJECT_SETTINGS, after),
        )

    def test_refuses_a_file_without_a_product_name(self) -> None:
        with self.assertRaisesRegex(SettingsFormatError, "0 productName lines"):
            override_project_names(SAMPLE_PROJECT_SETTINGS.replace("  productName: Mu3Library\n", ""))


class SceneCheckTests(unittest.TestCase):
    def test_a_scene_is_an_asset_path_of_a_unity_scene(self) -> None:
        self.assertIsNone(scene_problem("Assets/Mu3LibrarySamples/Multiplayer/Sample_Multiplayer.unity"))
        self.assertIsNone(scene_problem("Packages/com.example/Scenes/A.unity"))
        for scene in ("C:/x/A.unity", "/Assets/A.unity", "Assets\\A.unity", "Assets/../A.unity", "Library/A.unity", "Assets/A.prefab"):
            with self.subTest(scene=scene):
                self.assertIsNotNone(scene_problem(scene))


class ContentModeTests(unittest.TestCase):
    def test_none_is_refused_on_a_project_that_built_content(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            project = Path(folder)
            self.assertIsNone(content_mode_problem(project, "none"))

            (project / "Library" / "com.unity.addressables" / "aa").mkdir(parents=True)

            self.assertIn("pass --fresh", content_mode_problem(project, "none"))
            self.assertIsNone(content_mode_problem(project, "build"))


def git(root: Path, *arguments: str) -> str:
    return subprocess.run(["git", "-C", str(root), *arguments], check=True, capture_output=True, text=True).stdout


def isolate_git(test: unittest.TestCase, folder: Path) -> None:
    """Give this test's git commands, the tool's among them, a configuration of their own.

    The user's configuration, such as commit signing, and the variables that tie a command to one
    repository, which Git exports to hooks, would otherwise reach the fixture.
    """
    config = folder / "gitconfig"
    config.write_text("[safe]\n\tdirectory = *\n", encoding="utf-8")
    repository_variables = subprocess.run(
        ["git", "rev-parse", "--local-env-vars"], capture_output=True, text=True, check=True
    ).stdout.split()
    environment = {key: value for key, value in os.environ.items() if key not in repository_variables}
    environment.update(
        {
            "GIT_CONFIG_GLOBAL": str(config),
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_AUTHOR_NAME": "fixture",
            "GIT_AUTHOR_EMAIL": "fixture@example.invalid",
            "GIT_COMMITTER_NAME": "fixture",
            "GIT_COMMITTER_EMAIL": "fixture@example.invalid",
        }
    )
    patcher = mock.patch.dict(os.environ, environment, clear=True)
    patcher.start()
    test.addCleanup(patcher.stop)


def write(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="")


def fixture_repository(test: unittest.TestCase) -> Path:
    """A committed repository with the files the tool reads, shaped like this one."""
    directory = tempfile.TemporaryDirectory()
    test.addCleanup(directory.cleanup)
    root = Path(directory.name) / "repo"
    manifest = {"dependencies": {"com.github.doqltl179.mu3library.base": "checkout"}, "testables": ["com.github.doqltl179.mu3library.base"]}
    write(root / FULL / "Packages" / "manifest.json", json.dumps(manifest, indent=2) + "\n")
    write(root / FULL / SETTINGS_ASSET, SAMPLE_SETTINGS)
    write(root / FULL / "ProjectSettings" / "ProjectSettings.asset", SAMPLE_PROJECT_SETTINGS)
    write(root / FULL / "ProjectSettings" / "ProjectVersion.txt", "m_EditorVersion: 6000.6.7f1\n")
    write(root / FULL / "Assets" / "Mu3LibrarySamples.meta", "meta\n")
    write(root / FULL / "sample-links.json", '{"Assets/Mu3LibrarySamples": "com.github.doqltl179.mu3library.base"}\n')
    write(root / "packages" / "base" / "package.json", '{"name": "com.github.doqltl179.mu3library.base"}\n')
    write(root / "packages" / "base" / "Samples~" / "Demo" / "Demo.cs", "class Demo {}\n")
    write(root / BUILD_SCRIPT, "// build script\n")
    isolate_git(test, root.parent)
    git(root.parent, "init", "-q", str(root))
    git(root, "add", "-A")
    git(root, "commit", "-q", "-m", "fixture")
    return root


class FakeUnity:
    """Writes the files a build leaves: its log, the player, and, when told, the content."""

    def __init__(self, status: int | None = 0, builds_content: bool = False, builds_player: bool = True) -> None:
        # None stands for a Unity that the runner stopped at the timeout.
        self.status = status
        self.builds_content = builds_content
        self.builds_player = builds_player
        self.commands: list[list[str]] = []
        self.timeouts: list[float] = []

    def __call__(self, command: list[str], timeout: float) -> int | None:
        self.commands.append(command)
        self.timeouts.append(timeout)

        def argument(name: str) -> Path:
            return Path(command[command.index(name) + 1])

        project, player, log = argument("-projectPath"), argument("-mu3-out"), argument("-logFile")
        lines = ["MU3VERIFY settings profile=Default"]
        if self.builds_content:
            write(project / "Library" / "com.unity.addressables" / "aa" / "Windows" / "settings.json", "{}")
            lines.append("Addressable content successfully built (duration : 0:00:04.157)")
        if self.builds_player:
            write(player, "exe")
            if self.builds_content:
                write(player.parent / PLAYER_CONTENT, "{}")
        write(log, "\n".join(lines + [f"MU3VERIFY exit {self.status}"]) + "\n")
        return self.status


class RunTests(unittest.TestCase):
    def setUp(self) -> None:
        self.root = fixture_repository(self)
        self.work = self.root.parent / "work"
        self.project = self.work / "full"
        editor = self.root.parent / "Unity.exe"
        write(editor, "")
        patcher = mock.patch("verify_build.cli.resolve_editor_path", return_value=(editor, False))
        patcher.start()
        self.addCleanup(patcher.stop)
        self.output: list[str] = []

    def run_tool(self, content: str, runner: FakeUnity | None = None, **options: object) -> int:
        return run_verify_build(
            self.root,
            VerifyOptions(content=content, work_dir=self.work, **options),
            runner=runner or FakeUnity(),
            echo=self.output.append,
        )

    def test_no_build_generates_and_changes_the_project_and_leaves_the_checkout(self) -> None:
        self.assertEqual(0, self.run_tool("build", build=False))

        settings = (self.project / SETTINGS_ASSET).read_text(encoding="utf-8")
        self.assertIn("  m_BuildAddressablesWithPlayerBuild: 1\n", settings)
        self.assertIn("  productName: VerifyBuild\n", (self.project / "ProjectSettings" / "ProjectSettings.asset").read_text(encoding="utf-8"))
        self.assertTrue((self.project / PROJECT_BUILD_SCRIPT).is_file())
        self.assertTrue(is_directory_link(self.project / "Assets" / "Mu3LibrarySamples"))
        self.assertTrue((self.work / WORK_DIR_MARKER).is_file())
        self.assertEqual("", git(self.root, "status", "--porcelain"))
        self.assertIn("  m_BuildAddressablesWithPlayerBuild: 0\n", (self.root / FULL / SETTINGS_ASSET).read_text(encoding="utf-8"))

    def test_build_mode_passes_when_the_player_carries_the_content(self) -> None:
        unity = FakeUnity(builds_content=True)

        self.assertEqual(0, self.run_tool("build", unity))

        command = unity.commands[0]
        self.assertEqual("build", command[command.index("-mu3-content") + 1])
        self.assertEqual(str(self.project), command[command.index("-projectPath") + 1])
        self.assertIn("Content: built, carried in the player's StreamingAssets/aa", self.output)

    def test_build_mode_fails_when_the_player_carries_no_content(self) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, self.run_tool("build", FakeUnity()))
        self.assertTrue(any(line.startswith("FAIL: the player carries no Addressables content") for line in self.output))

    def test_unity_gets_the_build_timeout(self) -> None:
        unity = FakeUnity()

        self.assertEqual(0, self.run_tool("none", unity))
        self.assertEqual(0, self.run_tool("none", unity, build_timeout=120))

        self.assertEqual([3600, 120], unity.timeouts)

    def test_a_build_stopped_at_the_timeout_fails(self) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, self.run_tool("none", FakeUnity(status=None, builds_player=False), build_timeout=5))

        self.assertTrue(any(line.startswith("Unity was stopped after ") and line.endswith("at the build timeout of 5 s") for line in self.output))
        self.assertTrue(any(line.startswith("FAIL: Unity did not finish within --build-timeout") for line in self.output))

    def test_a_build_that_unity_failed_fails(self) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, self.run_tool("none", FakeUnity(status=1, builds_player=False)))
        self.assertIn("FAIL: no player was built", self.output)

    def test_none_mode_passes_without_content_and_is_refused_once_content_was_built(self) -> None:
        self.assertEqual(0, self.run_tool("none", FakeUnity()))
        self.assertEqual(0, self.run_tool("build", FakeUnity(builds_content=True)))

        with self.assertRaisesRegex(VerifySetupError, "holds the content of an earlier build"):
            self.run_tool("none", FakeUnity())
        self.assertEqual(0, self.run_tool("none", FakeUnity(), fresh=True))
        # --fresh deleted the project through its link without touching the samples.
        self.assertTrue((self.root / "packages" / "base" / "Samples~" / "Demo" / "Demo.cs").is_file())

    def test_none_mode_fails_when_content_was_built_anyway(self) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, self.run_tool("none", FakeUnity(builds_content=True)))
        self.assertIn("FAIL: content was built although --content none turned it off", self.output)

    def test_a_folder_it_did_not_make_is_refused_before_anything_is_written(self) -> None:
        write(self.work / "keep.txt", "keep")

        with self.assertRaisesRegex(VerifySetupError, f"has no {WORK_DIR_MARKER} marker"):
            self.run_tool("build", build=False)
        self.assertEqual(["keep.txt"], [path.name for path in self.work.iterdir()])


class CommandTests(unittest.TestCase):
    def test_content_is_required_and_must_be_a_mode(self) -> None:
        for arguments in ([], ["--content", "all"]):
            with self.subTest(arguments=arguments):
                self.assertEqual(2, CliRunner().invoke(app, arguments).exit_code)

    def test_a_scene_must_be_an_asset_path_and_needs_a_build(self) -> None:
        bad = CliRunner().invoke(app, ["--content", "none", "--scene", "C:/x/A.unity"])
        self.assertEqual(2, bad.exit_code)
        self.assertIn("--scene expects", bad.output)

        no_build = CliRunner().invoke(app, ["--content", "none", "--no-build", "--scene", "Assets/A.unity"])
        self.assertEqual(2, no_build.exit_code)

    def test_the_build_timeout_must_be_positive(self) -> None:
        result = CliRunner().invoke(app, ["--content", "none", "--build-timeout", "0"])

        self.assertEqual(2, result.exit_code)
        self.assertIn("--build-timeout expects a positive number", result.output)


if __name__ == "__main__":
    unittest.main()
