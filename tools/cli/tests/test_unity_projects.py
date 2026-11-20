from __future__ import annotations

import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from common.checkout import repo_root
from common.unity_projects import (
    PROJECT_MARKER,
    PROJECT_PATH_LIMIT,
    ConfigError,
    GenerateError,
    WorkDirError,
    create_directory_link,
    default_work_dir,
    generate_project,
    load_configs,
    project_path_problem,
    remove_generated_project,
    sync_template,
    unit_name,
)


BASE = "com.github.doqltl179.mu3library.base"
DI = "com.github.doqltl179.mu3library.di"


class RepositoryConfigTests(unittest.TestCase):
    """The configs this repository holds, as ADR 0001 D12 lists them."""

    def setUp(self) -> None:
        self.configs = load_configs(repo_root())

    def test_the_four_configs_name_their_packages_from_the_checkout(self) -> None:
        self.assertEqual(["full", "minimal", "urp", "watermelon"], list(self.configs))
        self.assertEqual(
            {
                BASE,
                DI,
                "com.github.doqltl179.mu3library.addressables",
                "com.github.doqltl179.mu3library.inputsystem",
                "com.github.doqltl179.mu3library.localization",
                "com.github.doqltl179.mu3library.notifications",
                "com.github.doqltl179.mu3library.steam",
            },
            set(self.configs["full"].packages),
        )
        self.assertEqual({BASE, "com.github.doqltl179.mu3library.urp"}, set(self.configs["urp"].packages))
        self.assertEqual(
            {BASE, "com.github.doqltl179.mu3library.urp", "com.github.doqltl179.mu3library.game.watermelon"},
            set(self.configs["watermelon"].packages),
        )
        for config in self.configs.values():
            with self.subTest(config=config.name):
                self.assertTrue(config.has_testables)
                self.assertRegex(config.editor_version, r"^\d+\.\d+\.\d+[abfp]\d+$")
                for folder in config.packages.values():
                    self.assertTrue((repo_root() / folder / "package.json").is_file())

    def test_minimal_holds_base_and_di_without_an_optional_sdk(self) -> None:
        minimal = self.configs["minimal"]
        dependencies = minimal.manifest["dependencies"]

        self.assertEqual({BASE, DI}, set(minimal.packages))
        for sdk in (
            "com.cysharp.unitask",
            "com.rlabrecque.steamworks.net",
            "com.unity.addressables",
            "com.unity.localization",
            "com.unity.inputsystem",
            "com.unity.mobile.notifications",
        ):
            self.assertNotIn(sdk, dependencies)
        settings = (minimal.folder / "ProjectSettings" / "ProjectSettings.asset").read_text(encoding="utf-8")
        # Without the Input System package, only the old Input Manager can handle input.
        self.assertIn("\n  activeInputHandler: 0\n", settings.replace("\r\n", "\n"))


class GeneratedProjectTests(unittest.TestCase):
    """Generates projects from a config in a throwaway checkout."""

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        temporary = Path(directory.name)
        self.root = temporary / "checkout"
        self.project = temporary / "work" / "sample"
        git_config = temporary / "gitconfig"
        git_config.write_text("[safe]\n\tdirectory = *\n", encoding="utf-8")
        environment = mock.patch.dict(os.environ, {"GIT_CONFIG_GLOBAL": str(git_config), "GIT_CONFIG_NOSYSTEM": "1"})
        environment.start()
        self.addCleanup(environment.stop)

        self.write("Packages/Thing/package.json", json.dumps({"name": "fixture.thing", "version": "1.0.0"}))
        self.config = self.root / "tools" / "unity-projects" / "sample"
        self.write_manifest({"fixture.thing": "checkout", "com.unity.ugui": "2.6.0"})
        self.write("tools/unity-projects/sample/ProjectSettings/ProjectVersion.txt", "m_EditorVersion: 6000.6.7f1\n")
        self.write("tools/unity-projects/sample/Assets/Settings/Asset.asset", "first\n")
        subprocess.run(["git", "init", "--quiet", str(self.root)], check=True)
        # Removing the link first keeps the cleanup from walking through it.
        self.addCleanup(self.remove_project)

    def write(self, relative: str, text: str) -> Path:
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def write_manifest(self, dependencies: dict[str, str]) -> None:
        self.write("tools/unity-projects/sample/Packages/manifest.json", json.dumps({"dependencies": dependencies}))

    def generate(self):
        return generate_project(self.root, load_configs(self.root)["sample"], self.project, probe=lambda path: False)

    def remove_project(self) -> None:
        if (self.project / PROJECT_MARKER).is_file():
            remove_generated_project(self.project)

    def test_project_takes_the_config_and_the_checkout_package_by_name(self) -> None:
        result = self.generate()

        self.assertTrue(result.created)
        manifest = json.loads((self.project / "Packages" / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual("file:" + (self.root / "Packages" / "Thing").as_posix(), manifest["dependencies"]["fixture.thing"])
        self.assertEqual("2.6.0", manifest["dependencies"]["com.unity.ugui"])
        self.assertEqual("first\n", (self.project / "Assets" / "Settings" / "Asset.asset").read_text(encoding="utf-8"))

    def test_refresh_drops_what_the_config_dropped_and_keeps_library_and_unity_s_files(self) -> None:
        self.generate()
        (self.project / "Library" / "cache.bin").parent.mkdir()
        (self.project / "Library" / "cache.bin").write_text("imported\n", encoding="utf-8")
        (self.project / "Assets" / "Settings" / "Asset.asset").write_text("saved by Unity\n", encoding="utf-8")
        (self.project / "Assets" / "Settings" / "Unity.asset").write_text("made by Unity\n", encoding="utf-8")
        (self.config / "Assets" / "Settings" / "Asset.asset").unlink()
        self.write("tools/unity-projects/sample/Assets/Other.asset", "second\n")

        result = self.generate()

        self.assertFalse(result.created)
        self.assertEqual(["Assets/Settings/Asset.asset"], result.removed)
        self.assertIn("Assets/Other.asset", result.written)
        self.assertFalse((self.project / "Assets" / "Settings" / "Asset.asset").exists())
        self.assertEqual("made by Unity\n", (self.project / "Assets" / "Settings" / "Unity.asset").read_text(encoding="utf-8"))
        self.assertEqual("imported\n", (self.project / "Library" / "cache.bin").read_text(encoding="utf-8"))
        self.assertEqual([], self.generate().written)

    def test_a_folder_the_tools_did_not_make_is_refused(self) -> None:
        (self.project / "Assets").mkdir(parents=True)
        (self.project / "Assets" / "Mine.cs").write_text("// someone's\n", encoding="utf-8")

        with self.assertRaisesRegex(GenerateError, "has no .mu3-unity-project marker"):
            self.generate()
        self.assertEqual(["Mine.cs"], [path.name for path in (self.project / "Assets").iterdir()])

    def test_a_project_that_another_checkout_generated_is_refused(self) -> None:
        # Branches whose first 32 characters agree share a unit's folder name.
        self.generate()
        marker = self.project / PROJECT_MARKER
        lines = marker.read_text(encoding="utf-8").splitlines()
        owner = f"checkout {self.root.resolve().as_posix()}"
        self.assertIn(owner, lines)

        marker.write_text("".join(f"{line}\n" for line in lines if line != owner) + "checkout D:/other\n", encoding="utf-8")
        with self.assertRaisesRegex(GenerateError, "generated for the checkout at D:/other, .*pass --work-dir <folder>"):
            self.generate()

        # A marker from before the owner line takes this checkout as its owner.
        marker.write_text("".join(f"{line}\n" for line in lines if line != owner), encoding="utf-8")
        self.generate()
        self.assertIn(owner, marker.read_text(encoding="utf-8").splitlines())

    def test_a_project_an_editor_has_open_is_refused(self) -> None:
        self.generate()
        (self.project / "Temp").mkdir()
        (self.project / "Temp" / "UnityLockfile").write_text("", encoding="utf-8")

        with self.assertRaisesRegex(GenerateError, "is open in Unity"):
            generate_project(self.root, load_configs(self.root)["sample"], self.project, probe=lambda path: True)

    def test_a_link_the_marker_lists_goes_without_following_it(self) -> None:
        # Before Base dropped its last sample (#556), a run linked a package's Samples~ into the
        # project and listed the link in the marker.
        target = self.write("Packages/Thing/Samples~/Sample.cs", "// sample\n").parent
        link = self.project / "Assets" / "Samples"
        marker = self.project / PROJECT_MARKER

        def add_listed_link() -> None:
            create_directory_link(link, target)
            marker.write_text(marker.read_text(encoding="utf-8") + "link Assets/Samples\n", encoding="utf-8")

        self.generate()
        add_listed_link()
        self.generate()
        self.assertFalse(link.exists() or link.is_symlink())
        self.assertNotIn("\nlink Assets/Samples\n", marker.read_text(encoding="utf-8"))
        self.assertTrue((target / "Sample.cs").is_file())

        add_listed_link()
        remove_generated_project(self.project)
        self.assertFalse(self.project.exists())
        self.assertTrue((target / "Sample.cs").is_file())

    def test_a_checkout_package_that_no_package_json_names_is_an_error(self) -> None:
        self.write_manifest({"fixture.missing": "checkout"})

        with self.assertRaisesRegex(ConfigError, 'names fixture.missing as "checkout"'):
            load_configs(self.root)


class TemplateSyncTests(unittest.TestCase):
    """A tool's template folder, copied into a folder of the Unity project the tool builds."""

    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)

    def write(self, path: Path, text: str) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")

    def test_sync_copies_new_files_removes_stale_ones_and_keeps_meta_and_generated_files(self) -> None:
        template = self.root / "template"
        project = self.root / "project"
        self.write(template / "Harness" / "Boot.cs", "boot")
        self.write(project / "Harness" / "Boot.cs.meta", "guid")
        self.write(project / "Harness" / "Old.cs", "old")
        self.write(project / "Plugins" / "HarnessPlugin.dll", "dll")

        changed = sync_template(template, project, ["Plugins/HarnessPlugin.dll"])

        self.assertEqual(["Harness/Boot.cs", "Harness/Old.cs"], changed)
        self.assertEqual("boot", (project / "Harness" / "Boot.cs").read_text(encoding="utf-8"))
        self.assertFalse((project / "Harness" / "Old.cs").exists())
        self.assertTrue((project / "Harness" / "Boot.cs.meta").exists())
        self.assertTrue((project / "Plugins" / "HarnessPlugin.dll").exists())

    def test_sync_leaves_an_unchanged_folder_alone(self) -> None:
        self.write(self.root / "template" / "Harness" / "Boot.cs", "boot")
        sync_template(self.root / "template", self.root / "project")

        self.assertEqual([], sync_template(self.root / "template", self.root / "project"))


class ProjectPathTests(unittest.TestCase):
    def test_refuses_a_deep_project_folder_on_windows_only(self) -> None:
        short = Path("D:/" + "a" * (PROJECT_PATH_LIMIT - 4))
        deep = Path("D:/" + "a" * (PROJECT_PATH_LIMIT - 3))
        self.assertEqual(PROJECT_PATH_LIMIT - 1, len(str(short)))

        self.assertIsNone(project_path_problem(short, "Windows"))
        self.assertIn(f"has {PROJECT_PATH_LIMIT} characters", project_path_problem(deep, "Windows"))
        self.assertIsNone(project_path_problem(deep, "Linux"))


class WorkDirTests(unittest.TestCase):
    def test_unit_takes_32_characters_of_the_branch_with_slashes_as_dashes_and_no_trailing_dots(self) -> None:
        self.assertEqual("chore-tooling-project-configs", unit_name("chore/tooling-project-configs"))
        self.assertEqual("feat-base-a-very-long-summary-th", unit_name("feat/base-a-very-long-summary-that-goes-on"))
        self.assertEqual("fix-x", unit_name("fix/x.."))

    def test_work_dir_without_a_work_root_says_what_to_set(self) -> None:
        with self.assertRaisesRegex(WorkDirError, "set the user environment variable MU3_WORK_ROOT"):
            default_work_dir(repo_root(), environ={})

    def test_work_root_is_absolute_or_starts_with_the_home_folder(self) -> None:
        with mock.patch("common.unity_projects.current_branch", return_value="feat/x"):
            home = default_work_dir(repo_root(), environ={"MU3_WORK_ROOT": "~/w"})
            with self.assertRaisesRegex(WorkDirError, "MU3_WORK_ROOT is w, which is not an absolute folder"):
                default_work_dir(repo_root(), environ={"MU3_WORK_ROOT": "w"})

        self.assertEqual(Path.home() / "w" / "Mu3Library_ForUnity" / "feat-x", home)


if __name__ == "__main__":
    unittest.main()
