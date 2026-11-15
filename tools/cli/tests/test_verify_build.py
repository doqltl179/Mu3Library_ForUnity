from __future__ import annotations

import difflib
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from typer.testing import CliRunner

from mu3_common.checkout import repo_root
from mu3_verify_build.cli import (
    BUILD_SCRIPT,
    COPY_BUILD_SCRIPT,
    EXIT_BUILD_FAILED,
    PROJECT_PATH_LIMIT,
    WORK_DIR_MARKER,
    SettingsFormatError,
    VerifyOptions,
    VerifySetupError,
    app,
    bundles_dir,
    content_mode_problem,
    copy_from_archive,
    override_addressables_settings,
    override_project_names,
    profile_path_problem,
    project_path_problem,
    run_verify_build,
    scene_problem,
    work_dir_problem,
)
from mu3_common.editor import load_target_specs


SETTINGS = "UnityProject_BuiltIn/Assets/AddressableAssetsData/AddressableAssetSettings.asset"
OUTSIDE = r"C:\Users\someone\Desktop\UnityProject\Mu3Web\Bundles\Windows"
BUNDLES = r"D:\checkout\log\verify-build-bundles"
REMOTE_LOAD = "https://doqltl179.github.io/Bundles/Windows"
LOCAL_BUILD = "'[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]'"
LOCAL_LOAD = "'{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]'"

# The lines of the tracked AddressableAssetSettings.asset that the tool reads, as of 2026-11-15 after
# #498, with the user's folder shortened.
SAMPLE_SETTINGS = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_Name: AddressableAssetSettings
  m_RemoteCatalogBuildPath:
    m_Id: 1f361e44290e9a348b9f533a553128d9
  m_RemoteCatalogLoadPath:
    m_Id: 983773c60fdc2ac4696dfcb6fa77688e
  m_ContentStateBuildPath:
  m_BuildAddressablesWithPlayerBuild: 0
  m_overridePlayerVersion: '[UnityEditor.PlayerSettings.bundleVersion]'
  m_ProfileSettings:
    m_Profiles:
    - m_InheritedParent:
      m_Id: 41d6783c029feb84db0438f9bc4051e7
      m_ProfileName: Default
      m_Values:
      - m_Id: 01bb88842aeda8f49831412080323f14
        m_Value: '[UnityEditor.EditorUserBuildSettings.activeBuildTarget]'
      - m_Id: 1f361e44290e9a348b9f533a553128d9
        m_Value: 'ServerData/[BuildTarget]'
      - m_Id: 7c8eaa9c01f11754cb898a6857ef902a
        m_Value: '[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]'
      - m_Id: 983773c60fdc2ac4696dfcb6fa77688e
        m_Value: <undefined>
      - m_Id: fa6c663e03e54c844ab7f5918f47c281
        m_Value: '{{UnityEngine.AddressableAssets.Addressables.RuntimePath}}/[BuildTarget]'
    - m_InheritedParent:
      m_Id: 1630fff1998e9e244a84795e159c3c73
      m_ProfileName: GitHub_Windows
      m_Values:
      - m_Id: 01bb88842aeda8f49831412080323f14
        m_Value: '[UnityEditor.EditorUserBuildSettings.activeBuildTarget]'
      - m_Id: 1f361e44290e9a348b9f533a553128d9
        m_Value: {OUTSIDE}
      - m_Id: 7c8eaa9c01f11754cb898a6857ef902a
        m_Value: {LOCAL_BUILD}
      - m_Id: 983773c60fdc2ac4696dfcb6fa77688e
        m_Value: {REMOTE_LOAD}
      - m_Id: fa6c663e03e54c844ab7f5918f47c281
        m_Value: {LOCAL_LOAD}
    m_ProfileEntryNames:
    - m_Id: 01bb88842aeda8f49831412080323f14
      m_Name: BuildTarget
      m_InlineUsage: 0
    - m_Id: 1f361e44290e9a348b9f533a553128d9
      m_Name: Remote.BuildPath
      m_InlineUsage: 0
    - m_Id: 7c8eaa9c01f11754cb898a6857ef902a
      m_Name: Local.BuildPath
      m_InlineUsage: 0
    - m_Id: 983773c60fdc2ac4696dfcb6fa77688e
      m_Name: Remote.LoadPath
      m_InlineUsage: 0
    - m_Id: fa6c663e03e54c844ab7f5918f47c281
      m_Name: Local.LoadPath
      m_InlineUsage: 0
    m_ProfileVersion: 1
  m_ActivePlayerDataBuilderIndex: 2
  m_ActiveProfileId: 1630fff1998e9e244a84795e159c3c73
"""

# GitHub_Windows's Local.BuildPath item with the Remote.LoadPath item after it, which only that profile has.
GITHUB_REMOTE_LOAD = f"      - m_Id: 983773c60fdc2ac4696dfcb6fa77688e\n        m_Value: {REMOTE_LOAD}\n"
GITHUB_LOCAL_BUILD = f"      - m_Id: 7c8eaa9c01f11754cb898a6857ef902a\n        m_Value: {LOCAL_BUILD}\n" + GITHUB_REMOTE_LOAD

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
    def test_build_mode_changes_the_remote_build_path_and_the_player_build_option_only(self) -> None:
        after = override_addressables_settings(SAMPLE_SETTINGS, BUNDLES, "build")

        self.assertEqual(
            [
                ("  m_BuildAddressablesWithPlayerBuild: 0", "  m_BuildAddressablesWithPlayerBuild: 1"),
                (f"        m_Value: {OUTSIDE}", f"        m_Value: '{BUNDLES}'"),
            ],
            changed_lines(SAMPLE_SETTINGS, after),
        )
        # Local.BuildPath and the two load paths of the profile, and the Default profile, stay.
        self.assertIn(f"        m_Value: {REMOTE_LOAD}\n", after)
        self.assertEqual(2, after.count(f"        m_Value: {LOCAL_BUILD}\n"))
        self.assertEqual(2, after.count(f"        m_Value: {LOCAL_LOAD}\n"))
        self.assertNotIn(OUTSIDE, after)
        self.assertIn("        m_Value: 'ServerData/[BuildTarget]'\n", after)

    def test_none_mode_turns_the_content_build_off(self) -> None:
        after = override_addressables_settings(SAMPLE_SETTINGS, BUNDLES, "none")

        self.assertIn("  m_BuildAddressablesWithPlayerBuild: 2\n", after)
        self.assertEqual(1, after.count(f"        m_Value: '{BUNDLES}'\n"))

    def test_applying_it_again_gives_the_same_file(self) -> None:
        once = override_addressables_settings(SAMPLE_SETTINGS, BUNDLES, "build")

        self.assertEqual(once, override_addressables_settings(once, BUNDLES, "build"))
        self.assertIn("  m_BuildAddressablesWithPlayerBuild: 2\n", override_addressables_settings(once, BUNDLES, "none"))

    def test_quotes_an_apostrophe_in_the_folder(self) -> None:
        after = override_addressables_settings(SAMPLE_SETTINGS, r"D:\O'Neil\log\b", "build")

        self.assertEqual(1, after.count(r"        m_Value: 'D:\O''Neil\log\b'" + "\n"))

    def test_keeps_crlf_line_endings(self) -> None:
        crlf = SAMPLE_SETTINGS.replace("\n", "\r\n")

        after = override_addressables_settings(crlf, BUNDLES, "build")

        self.assertNotIn("\n", after.replace("\r\n", ""))
        self.assertIn(f"        m_Value: '{BUNDLES}'\r\n", after)

    def test_replaces_a_value_that_continues_on_the_next_line(self) -> None:
        wrapped = SAMPLE_SETTINGS.replace(
            f"      - m_Id: 1f361e44290e9a348b9f533a553128d9\n        m_Value: {OUTSIDE}\n",
            "      - m_Id: 1f361e44290e9a348b9f533a553128d9\n        m_Value: C:\\a very long folder\n          that Unity wrapped\n",
        )

        after = override_addressables_settings(wrapped, BUNDLES, "build")

        self.assertNotIn("that Unity wrapped", after)
        self.assertEqual(1, after.count(f"        m_Value: '{BUNDLES}'\n"))
        self.assertEqual(2, after.count(f"        m_Value: {LOCAL_BUILD}\n"))

    def test_refuses_a_local_build_path_other_than_the_default(self) -> None:
        # The folder the profile named before #498, a value one variable short, and the default wrapped onto a second line.
        for value in (OUTSIDE, "'[UnityEngine.AddressableAssets.Addressables.BuildPath]'", f"{LOCAL_BUILD}\n          wrapped"):
            with self.subTest(value=value):
                other = SAMPLE_SETTINGS.replace(
                    GITHUB_LOCAL_BUILD, f"      - m_Id: 7c8eaa9c01f11754cb898a6857ef902a\n        m_Value: {value}\n" + GITHUB_REMOTE_LOAD
                )
                self.assertNotEqual(SAMPLE_SETTINGS, other)

                with self.assertRaisesRegex(SettingsFormatError, "GitHub_Windows .* has Local.BuildPath .*, not the default"):
                    override_addressables_settings(other, BUNDLES, "build")

    def test_refuses_another_active_profile(self) -> None:
        default_active = SAMPLE_SETTINGS.replace(
            "m_ActiveProfileId: 1630fff1998e9e244a84795e159c3c73", "m_ActiveProfileId: 41d6783c029feb84db0438f9bc4051e7"
        )

        with self.assertRaisesRegex(SettingsFormatError, "active profile .* is Default, not GitHub_Windows"):
            override_addressables_settings(default_active, BUNDLES, "build")

    def test_refuses_an_unknown_active_profile(self) -> None:
        unknown = SAMPLE_SETTINGS.replace("m_ActiveProfileId: 1630fff1998e9e244a84795e159c3c73", "m_ActiveProfileId: 0")

        with self.assertRaisesRegex(SettingsFormatError, "no profile with the active profile's id 0"):
            override_addressables_settings(unknown, BUNDLES, "build")

    def test_refuses_a_file_without_a_build_path_variable(self) -> None:
        without_local = SAMPLE_SETTINGS.replace("      m_Name: Local.BuildPath\n", "      m_Name: Other\n")

        with self.assertRaisesRegex(SettingsFormatError, "no profile variable Local.BuildPath"):
            override_addressables_settings(without_local, BUNDLES, "build")

    def test_refuses_a_profile_without_a_value_for_a_build_path(self) -> None:
        without_value = SAMPLE_SETTINGS.replace(GITHUB_LOCAL_BUILD, GITHUB_REMOTE_LOAD)

        with self.assertRaisesRegex(SettingsFormatError, "GitHub_Windows .* has no value for Local.BuildPath"):
            override_addressables_settings(without_value, BUNDLES, "build")

    def test_refuses_a_file_with_two_player_build_options(self) -> None:
        doubled = SAMPLE_SETTINGS.replace(
            "  m_BuildAddressablesWithPlayerBuild: 0\n", "  m_BuildAddressablesWithPlayerBuild: 0\n  m_BuildAddressablesWithPlayerBuild: 0\n"
        )

        with self.assertRaisesRegex(SettingsFormatError, "2 m_BuildAddressablesWithPlayerBuild lines, not one"):
            override_addressables_settings(doubled, BUNDLES, "build")

    def test_the_tracked_asset_changes_in_two_lines(self) -> None:
        tracked = (repo_root() / SETTINGS).read_text(encoding="utf-8")

        pairs = changed_lines(tracked, override_addressables_settings(tracked, BUNDLES, "build"))

        self.assertEqual(["  m_BuildAddressablesWithPlayerBuild: 1", f"        m_Value: '{BUNDLES}'"], [after for _, after in pairs])


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


class PathCheckTests(unittest.TestCase):
    def test_a_profile_value_must_be_absolute_and_free_of_variable_brackets(self) -> None:
        self.assertIsNone(profile_path_problem(Path.cwd() / "log" / "b"))
        self.assertIn("not an absolute path", profile_path_problem(Path("log") / "b"))
        for folder in ("[x]", "{x}"):
            with self.subTest(folder=folder):
                self.assertIn("profile variable", profile_path_problem(Path.cwd() / folder / "log"))

    def test_refuses_a_deep_project_folder_on_windows_only(self) -> None:
        short = Path("D:/" + "a" * (PROJECT_PATH_LIMIT - 4))
        deep = Path("D:/" + "a" * (PROJECT_PATH_LIMIT - 3))
        self.assertEqual(PROJECT_PATH_LIMIT - 1, len(str(short)))

        self.assertIsNone(project_path_problem(short, "Windows"))
        self.assertIn(f"has {PROJECT_PATH_LIMIT} characters", project_path_problem(deep, "Windows"))
        self.assertIsNone(project_path_problem(deep, "Linux"))

    def test_a_scene_is_an_asset_path_of_a_unity_scene(self) -> None:
        self.assertIsNone(scene_problem("Assets/Mu3LibrarySamples/Multiplayer/Sample_Multiplayer.unity"))
        self.assertIsNone(scene_problem("Packages/com.example/Scenes/A.unity"))
        for scene in ("C:/x/A.unity", "/Assets/A.unity", "Assets\\A.unity", "Assets/../A.unity", "Library/A.unity", "Assets/A.prefab"):
            with self.subTest(scene=scene):
                self.assertIsNotNone(scene_problem(scene))


class ContentModeTests(unittest.TestCase):
    def test_none_is_refused_on_a_copy_that_built_content(self) -> None:
        with tempfile.TemporaryDirectory() as folder:
            copy = Path(folder)
            self.assertIsNone(content_mode_problem(copy, "none"))

            (copy / "Library" / "com.unity.addressables" / "aa").mkdir(parents=True)

            self.assertIn("pass --fresh", content_mode_problem(copy, "none"))
            self.assertIsNone(content_mode_problem(copy, "build"))


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
    write(root / "unity-cli-packages.tsv", "built-in\tMu3Library_Base\tUnityProject_BuiltIn\tAssets/Mu3LibrarySamples\n")
    write(root / SETTINGS, SAMPLE_SETTINGS)
    write(root / "UnityProject_BuiltIn" / "ProjectSettings" / "ProjectSettings.asset", SAMPLE_PROJECT_SETTINGS)
    write(root / "UnityProject_BuiltIn" / "ProjectSettings" / "ProjectVersion.txt", "m_EditorVersion: 6000.6.7f1\n")
    write(root / "UnityProject_BuiltIn" / "Assets" / "Mu3LibrarySamples.meta", "meta\n")
    write(root / "UnityProject_BuiltIn" / "Assets" / "Scene.unity", "committed\n")
    write(root / "Mu3Library_Base" / "package.json", "{}\n")
    write(root / "Mu3Library_Base" / "Samples~" / "Demo" / "Demo.cs", "class Demo {}\n")
    write(root / BUILD_SCRIPT, "// build script\n")
    isolate_git(test, root.parent)
    git(root.parent, "init", "-q", str(root))
    git(root, "add", "-A")
    git(root, "commit", "-q", "-m", "fixture")
    return root


class CopyTests(unittest.TestCase):
    def test_copies_the_committed_tracked_files_and_the_samples_as_a_real_folder(self) -> None:
        root = fixture_repository(self)
        write(root / "UnityProject_BuiltIn" / "Assets" / "Scene.unity", "uncommitted\n")
        write(root / "UnityProject_BuiltIn" / "Assets" / "Untracked.cs", "untracked\n")
        work = root.parent / "work"

        written = copy_from_archive(root, git(root, "rev-parse", "HEAD").strip(), load_target_specs(root)["built-in"], work)

        self.assertEqual("committed\n", (work / "UnityProject_BuiltIn" / "Assets" / "Scene.unity").read_text(encoding="utf-8"))
        self.assertFalse((work / "UnityProject_BuiltIn" / "Assets" / "Untracked.cs").exists())
        self.assertTrue((work / "Mu3Library_Base" / "Samples~" / "Demo" / "Demo.cs").is_file())
        samples = work / "UnityProject_BuiltIn" / "Assets" / "Mu3LibrarySamples"
        self.assertEqual("class Demo {}\n", (samples / "Demo" / "Demo.cs").read_text(encoding="utf-8"))
        self.assertFalse(samples.is_symlink())
        self.assertFalse((work / "tools").exists())
        # Five files of the project and two of the package, and the sample again in the project.
        self.assertEqual(8, written)

    def test_an_unknown_commit_stops_with_git_s_message(self) -> None:
        root = fixture_repository(self)

        with self.assertRaisesRegex(VerifySetupError, "git archive exited with"):
            copy_from_archive(root, "0" * 40, load_target_specs(root)["built-in"], root.parent / "work")


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
        lines = ["MU3VERIFY settings profile=GitHub_Windows"]
        if self.builds_content:
            (project / "Library" / "com.unity.addressables" / "aa").mkdir(parents=True, exist_ok=True)
            write(argument("-mu3-bundles") / "content.bundle", "bundle")
            lines.append("Addressable content successfully built (duration : 0:00:04.157)")
        if self.builds_player:
            write(player, "exe")
        write(log, "\n".join(lines + [f"MU3VERIFY exit {self.status}"]) + "\n")
        return self.status


class RunTests(unittest.TestCase):
    def setUp(self) -> None:
        self.root = fixture_repository(self)
        self.work = self.root.parent / "work"
        editor = self.root.parent / "Unity.exe"
        write(editor, "")
        patcher = mock.patch("mu3_verify_build.cli.resolve_editor_path", return_value=(editor, False))
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

    def test_no_build_makes_and_changes_the_copy_and_leaves_the_tracked_files(self) -> None:
        self.assertEqual(0, self.run_tool("build", build=False))

        copy = self.work / "UnityProject_BuiltIn"
        settings = (copy / "Assets" / "AddressableAssetsData" / "AddressableAssetSettings.asset").read_text(encoding="utf-8")
        self.assertEqual(1, settings.count(f"        m_Value: '{bundles_dir(self.root)}'\n"))
        self.assertEqual(2, settings.count(f"        m_Value: {LOCAL_BUILD}\n"))
        self.assertIn("  m_BuildAddressablesWithPlayerBuild: 1\n", settings)
        self.assertIn("  productName: VerifyBuild\n", (copy / "ProjectSettings" / "ProjectSettings.asset").read_text(encoding="utf-8"))
        self.assertTrue((copy / COPY_BUILD_SCRIPT).is_file())
        self.assertTrue((self.work / WORK_DIR_MARKER).is_file())
        self.assertEqual("", git(self.root, "status", "--porcelain"))
        self.assertFalse(bundles_dir(self.root).exists())

    def test_build_mode_passes_when_the_content_and_the_player_built(self) -> None:
        unity = FakeUnity(builds_content=True)

        self.assertEqual(0, self.run_tool("build", unity))

        command = unity.commands[0]
        self.assertEqual("build", command[command.index("-mu3-content") + 1])
        self.assertEqual(str(bundles_dir(self.root)), command[command.index("-mu3-bundles") + 1])
        self.assertIn("Content: built, 1 files in " + str(bundles_dir(self.root)), self.output)
        self.assertTrue((bundles_dir(self.root) / WORK_DIR_MARKER).is_file())

    def test_build_mode_fails_when_no_content_was_built(self) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, self.run_tool("build", FakeUnity()))
        self.assertTrue(any(line.startswith("FAIL: no content was built") for line in self.output))

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

    def test_none_mode_fails_when_content_was_built_anyway(self) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, self.run_tool("none", FakeUnity(builds_content=True)))
        self.assertIn("FAIL: content was built although --content none turned it off", self.output)

    def test_a_copy_of_another_commit_is_refused_until_fresh(self) -> None:
        self.assertEqual(0, self.run_tool("build", build=False))
        write(self.root / "UnityProject_BuiltIn" / "Assets" / "Scene.unity", "second\n")
        git(self.root, "commit", "-q", "-am", "second")

        with self.assertRaisesRegex(VerifySetupError, "pass --fresh"):
            self.run_tool("build", build=False)
        self.assertEqual(0, self.run_tool("build", build=False, fresh=True))
        self.assertEqual("second\n", (self.work / "UnityProject_BuiltIn" / "Assets" / "Scene.unity").read_text(encoding="utf-8"))

    def test_a_folder_it_did_not_make_is_refused_before_anything_is_written(self) -> None:
        write(self.work / "keep.txt", "keep")

        with self.assertRaisesRegex(VerifySetupError, f"has no {WORK_DIR_MARKER} marker"):
            self.run_tool("build", build=False)
        self.assertEqual(["keep.txt"], [path.name for path in self.work.iterdir()])

    def test_a_work_dir_that_is_in_or_holds_the_bundles_folder_is_refused_before_anything_is_written(self) -> None:
        bundles = bundles_dir(self.root)
        for work, relation in ((bundles, "is"), (bundles / "copy", "is inside"), (self.root / "log", "holds")):
            with self.subTest(work=work):
                with self.assertRaisesRegex(VerifySetupError, f" {relation} the bundles folder .*, which a content build empties"):
                    run_verify_build(
                        self.root,
                        VerifyOptions(content="build", work_dir=work),
                        runner=FakeUnity(builds_content=True),
                        echo=self.output.append,
                    )
                self.assertFalse((self.root / "log").exists())

        self.assertIsNone(work_dir_problem(self.work, bundles))
        # A sibling whose name only starts with the bundles folder's is apart from it.
        self.assertIsNone(work_dir_problem(bundles.parent / (bundles.name + "-copy"), bundles))

    def test_a_bundles_folder_it_did_not_make_is_refused(self) -> None:
        write(bundles_dir(self.root) / "keep.txt", "keep")

        with self.assertRaisesRegex(VerifySetupError, "will not build content into it"):
            self.run_tool("build", FakeUnity(builds_content=True))
        self.assertTrue((bundles_dir(self.root) / "keep.txt").is_file())


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
