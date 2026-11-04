from __future__ import annotations

import contextlib
import json
import os
import re
import tempfile
import time
import unittest
from pathlib import Path
from typing import Callable
from unittest import mock

from typer.testing import CliRunner

from shell_harness import run_sourced
from test_unity_runs import FAKE_EDITOR_PACKAGES, FLOOR_VERSION
from mu3_cli.cli import app
from mu3_cli.repository import repo_root
from mu3_cli.strip_harness import (
    EXIT_BUILD_FAILED,
    EXIT_CHECKS_FAILED,
    WORK_DIR_MARKER,
    HarnessOptions,
    HarnessRun,
    HarnessCheck,
    HarnessSetupError,
    claim_work_dir,
    mirror_manifest,
    parse_player_log,
    retarget_dependencies,
    run_harness,
    run_problems,
    stop_process_tree,
    sync_template,
    template_dir,
    unitask_source,
)
from mu3_cli.unity import editor_bundled_versions, editor_package_manager_dir


UNITASK_URL = "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
PASSING_LOG = """Mono path[0] = 'C:/x/Harness_Data/Managed'
HARNESS START unity=6000.6.7f1 backend=mono debugBuild=False
(Filename: C:\\build\\output\\Runtime\\Export\\Debug\\Debug.bindings.h Line: 39)

HARNESS UNUSED UnusedService present=False
HARNESS TYPE Clock: ctors=[()] fields=[] props=[Now(get=True,set=False)]
HARNESS CHECK core.initialized-event PASS
HARNESS CHECK inject.field PASS
HARNESS INFO player-only member without [Preserve] injected=False
HARNESS RESULT PASS checks=2 failures=0 errorLogs=0
"""


def temporary_root(test: unittest.TestCase) -> Path:
    directory = tempfile.TemporaryDirectory()
    test.addCleanup(directory.cleanup)
    return Path(directory.name)


def write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def fixture_root(test: unittest.TestCase) -> Path:
    """A repository with the files the harness reads: the mapping, two projects, Base, and the template."""
    root = temporary_root(test)
    write(
        root / "unity-cli-packages.tsv",
        "built-in\tMu3Library_Base\tUnityProject_BuiltIn\n"
        "urp\tMu3Library_URP\tUnityProject_URP\n",
    )
    write(root / "UnityProject_URP" / "ProjectSettings" / "ProjectVersion.txt", "m_EditorVersion: 6000.6.7f1\n")
    write(
        root / "UnityProject_URP" / "Packages" / "manifest.json",
        json.dumps(
            {
                "dependencies": {
                    "com.unity.render-pipelines.universal": "17.6.0",
                    "com.unity.test-framework": "1.8.0",
                    "com.unity.ugui": "2.6.0",
                    "com.unity.modules.audio": "1.0.0",
                },
                "testables": ["com.github.doqltl179.mu3library.base"],
            }
        ),
    )
    write(
        root / "UnityProject_BuiltIn" / "Packages" / "manifest.json",
        json.dumps({"dependencies": {"com.cysharp.unitask": UNITASK_URL}}),
    )
    write(
        root / "UnityProject_BuiltIn" / "Packages" / "packages-lock.json",
        json.dumps({"dependencies": {"com.cysharp.unitask": {"version": UNITASK_URL, "hash": "abc123"}}}),
    )
    (root / "Mu3Library_Base").mkdir()
    write(root / "tools" / "di-strip-harness" / "Assets" / "Harness" / "Boot.cs", "// boot\n")
    write(root / "tools" / "di-strip-harness" / "Plugin" / "HarnessPlugin.cs", "// plugin\n")
    return root


def fake_editor(root: Path, with_packages: bool = True) -> Path:
    """An Editor executable, beside the packages an older Editor bundles when asked."""
    editor = write(root / "Editor" / "Unity.exe", "")
    if with_packages:
        for relative, content in FAKE_EDITOR_PACKAGES.items():
            write(root / "Editor" / "Data" / "Resources" / "PackageManager" / relative, json.dumps(content, indent=2) + "\n")
    return editor


class FakeUnity:
    """Stands in for mcs, the Editor's build, and the player, recording each command it ran."""

    def __init__(self, player_log: str = PASSING_LOG, build_status: int | None = 0, player_status: int | None = 0):
        self.player_log = player_log
        self.build_status = build_status
        self.player_status = player_status
        self.commands: list[list[str]] = []

    def __call__(self, command: list[str], timeout: float) -> int | None:
        self.commands.append(command)
        if any(argument.startswith("-out:") for argument in command):
            output = next(argument for argument in command if argument.startswith("-out:")).removeprefix("-out:")
            write(Path(output), "dll")
            return 0
        if "-executeMethod" in command:
            player = Path(command[command.index("-mu3-out") + 1])
            log = Path(command[command.index("-logFile") + 1])
            write(log, "HARNESS BUILD Succeeded errors=0 warnings=0\n" if self.build_status == 0 else "HARNESS BUILD Failed\n")
            if self.build_status == 0:
                write(player, "exe")
                write(player.parent / "Harness_Data" / "Managed" / "Mu3Library.DI.dll", "12345")
                write(player.parent / "Harness_Data" / "Managed" / "Assembly-CSharp.dll", "123")
            return self.build_status
        log = Path(command[command.index("-logFile") + 1])
        write(log, self.player_log)
        return self.player_status

    def ran(self, marker: str) -> list[list[str]]:
        return [command for command in self.commands if any(marker in argument for argument in command)]


class StripHarnessManifestTests(unittest.TestCase):
    def test_mirror_keeps_only_modules_and_ugui_and_adds_this_checkouts_base(self) -> None:
        root = temporary_root(self)
        manifest = mirror_manifest(
            {
                "dependencies": {
                    "com.unity.render-pipelines.universal": "17.6.0",
                    "com.unity.ugui": "2.6.0",
                    "com.unity.modules.audio": "1.0.0",
                    "com.github.doqltl179.mu3library.base": "file:../../Mu3Library_Base",
                },
                "testables": ["com.github.doqltl179.mu3library.base"],
            },
            root / "Mu3Library_Base",
            None,
        )

        self.assertEqual(["dependencies"], list(manifest))
        self.assertEqual(
            {
                "com.github.doqltl179.mu3library.base": "file:" + (root / "Mu3Library_Base").resolve().as_posix(),
                "com.unity.modules.audio": "1.0.0",
                "com.unity.ugui": "2.6.0",
            },
            manifest["dependencies"],
        )

    def test_mirror_adds_unitask_only_when_asked(self) -> None:
        root = temporary_root(self)

        with_unitask = mirror_manifest({"dependencies": {}}, root, "url#abc")

        self.assertEqual("url#abc", with_unitask["dependencies"]["com.cysharp.unitask"])
        self.assertNotIn("com.cysharp.unitask", mirror_manifest({"dependencies": {}}, root, None)["dependencies"])

    def test_unitask_is_pinned_to_the_commit_the_lock_file_names(self) -> None:
        root = fixture_root(self)

        self.assertEqual(f"{UNITASK_URL}#abc123", unitask_source(root))

    def test_unitask_url_that_already_names_a_commit_is_kept(self) -> None:
        root = fixture_root(self)
        write(
            root / "UnityProject_BuiltIn" / "Packages" / "manifest.json",
            json.dumps({"dependencies": {"com.cysharp.unitask": f"{UNITASK_URL}#pinned"}}),
        )

        self.assertEqual(f"{UNITASK_URL}#pinned", unitask_source(root))


class StripHarnessTemplateTests(unittest.TestCase):
    def test_sync_copies_new_files_removes_stale_ones_and_keeps_meta_and_generated_files(self) -> None:
        root = temporary_root(self)
        template = root / "template"
        project = root / "project"
        write(template / "Harness" / "Boot.cs", "boot")
        write(project / "Harness" / "Boot.cs.meta", "guid")
        write(project / "Harness" / "Old.cs", "old")
        write(project / "Plugins" / "HarnessPlugin.dll", "dll")

        changed = sync_template(template, project, ["Plugins/HarnessPlugin.dll"])

        self.assertEqual(["Harness/Boot.cs", "Harness/Old.cs"], changed)
        self.assertEqual("boot", (project / "Harness" / "Boot.cs").read_text(encoding="utf-8"))
        self.assertFalse((project / "Harness" / "Old.cs").exists())
        self.assertTrue((project / "Harness" / "Boot.cs.meta").exists())
        self.assertTrue((project / "Plugins" / "HarnessPlugin.dll").exists())

    def test_sync_leaves_an_unchanged_mirror_alone(self) -> None:
        root = temporary_root(self)
        write(root / "template" / "Harness" / "Boot.cs", "boot")
        sync_template(root / "template", root / "project")

        self.assertEqual([], sync_template(root / "template", root / "project"))

    def test_repository_template_logs_the_lines_the_parser_reads(self) -> None:
        template = template_dir(repo_root())
        boot = (template / "Assets" / "Harness" / "StripHarnessBoot.cs").read_text(encoding="utf-8")

        for path in (
            "Assets/Harness/StripHarnessServices.cs",
            "Assets/Editor/StripHarnessBuild.cs",
            "Assets/Domain/HarnessDomain.asmdef",
            "Assets/Domain/HarnessDomain.cs",
            "Plugin/HarnessPlugin.cs",
        ):
            self.assertTrue((template / path).is_file(), path)
        self.assertIn('Debug.Log($"HARNESS CHECK {name} {(ok ? "PASS" : "FAIL")} {info}");', boot)
        self.assertIn("HARNESS RESULT {(pass ? \"PASS\" : \"FAIL\")} checks={_checks} failures={_failures} errorLogs={_errorLogs}", boot)
        # The domain assembly and the plugin stand for code that knows nothing of the container.
        asmdef = json.loads((template / "Assets" / "Domain" / "HarnessDomain.asmdef").read_text(encoding="utf-8"))
        self.assertEqual([], asmdef["references"])
        self.assertNotIn("Mu3Library", (template / "Plugin" / "HarnessPlugin.cs").read_text(encoding="utf-8").split("namespace")[1])

    def test_repository_template_reaches_the_static_inject_members_only_by_reflection(self) -> None:
        # The static-member checks show what the link.xml keeps only while no code reads or
        # writes those members: any reference would keep them in the player without it.
        harness = template_dir(repo_root()) / "Assets" / "Harness"
        services = (harness / "StripHarnessServices.cs").read_text(encoding="utf-8")
        boot = (harness / "StripHarnessBoot.cs").read_text(encoding="utf-8")

        for name in ("_strippedStaticClock", "StrippedStaticClock", "_strippedBaseStaticClock"):
            self.assertEqual(1, services.count(name), name)
            self.assertEqual(1, boot.count(name + '"'), name)
            self.assertEqual(boot.count(name), boot.count(name + '"'), name)

    def test_repository_template_reaches_the_unread_instance_inject_field_only_by_reflection(self) -> None:
        # The linker keeps the [Inject] attribute of a field that code reads, so the check shows
        # what the link.xml keeps only while the field's declaration is its only mention.
        harness = template_dir(repo_root()) / "Assets" / "Harness"
        services = (harness / "StripHarnessServices.cs").read_text(encoding="utf-8")
        boot = (harness / "StripHarnessBoot.cs").read_text(encoding="utf-8")

        self.assertEqual(1, services.count("_injectedButUnread"))
        self.assertEqual(0, boot.count("_injectedButUnread"))
        self.assertIn("[Inject] private IUnreadFieldService _injectedButUnread;", services)
        # Reflection on the class itself, such as typeof(...).GetField, can make the linker keep
        # the field too, so the boot only creates it.
        self.assertEqual(1, boot.count("UnreadFieldHolder"))
        self.assertIn("new UnreadFieldHolder()", boot)


class StripHarnessLogTests(unittest.TestCase):
    def test_parser_reads_checks_result_and_notes_among_unity_lines(self) -> None:
        run = parse_player_log(PASSING_LOG)

        self.assertEqual(
            [HarnessCheck("core.initialized-event", True, ""), HarnessCheck("inject.field", True, "")], run.checks
        )
        self.assertEqual(("PASS", 2, 0), (run.result, run.declared_checks, run.error_logs))
        self.assertEqual(
            [
                "START unity=6000.6.7f1 backend=mono debugBuild=False",
                "UNUSED UnusedService present=False",
                "INFO player-only member without [Preserve] injected=False",
            ],
            run.notes,
        )

    def test_parser_keeps_a_failed_checks_detail(self) -> None:
        run = parse_player_log("HARNESS CHECK ctor.precompiled-dll FAIL MissingMethodException: ctor\n")

        self.assertEqual([HarnessCheck("ctor.precompiled-dll", False, "MissingMethodException: ctor")], run.checks)

    def test_a_complete_passing_run_has_no_problem(self) -> None:
        self.assertEqual([], run_problems(parse_player_log(PASSING_LOG), 0))

    def test_each_way_a_run_can_fail_is_named(self) -> None:
        failed = parse_player_log(PASSING_LOG.replace("inject.field PASS", "inject.field FAIL").replace("RESULT PASS", "RESULT FAIL"))
        missing = parse_player_log(PASSING_LOG.replace("checks=2", "checks=3"))
        errors = parse_player_log(PASSING_LOG.replace("errorLogs=0", "errorLogs=1"))
        no_result = parse_player_log("\n".join(line for line in PASSING_LOG.splitlines() if "RESULT" not in line))

        self.assertIn("1 checks failed: inject.field", run_problems(failed, 3))
        self.assertIn("the player exited with 3", run_problems(failed, 3))
        self.assertIn("the player counted 3 checks but logged 2", run_problems(missing, 0))
        self.assertIn("the player logged 1 errors or exceptions", run_problems(errors, 0))
        self.assertIn("the player logged no HARNESS RESULT line", run_problems(no_result, 0))
        self.assertIn("the player did not exit before the timeout and was stopped", run_problems(parse_player_log(PASSING_LOG), None))
        self.assertIn("the player logged no HARNESS CHECK line", run_problems(HarnessRun(), 0))


@mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows")
class StripHarnessRunTests(unittest.TestCase):
    def run_fixture(self, unity: FakeUnity, options: HarnessOptions | None = None) -> tuple[int, list[str], Path]:
        root = fixture_root(self)
        editor = write(root / "Editor" / "Unity.exe", "")
        work = root / "work"
        lines: list[str] = []
        with mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)):
            code = run_harness(root, options or HarnessOptions(work_dir=work), unity, lines.append)
        return code, lines, work

    def test_a_passing_player_exits_zero_and_prints_every_check(self, _system: mock.Mock) -> None:
        unity = FakeUnity()

        code, lines, work = self.run_fixture(unity)

        self.assertEqual(0, code)
        self.assertIn("PASS core.initialized-event", lines)
        self.assertIn("PASS inject.field", lines)
        self.assertIn("Managed DLLs: 8 bytes in 2 files", lines)
        self.assertIn("mono-high: 2 checks, 2 passed, 0 failed", lines)
        build = unity.ran("-executeMethod")[0]
        self.assertEqual(["mono", "High"], [build[build.index("-mu3-backend") + 1], build[build.index("-mu3-stripping") + 1]])
        self.assertEqual(str(work / "project"), build[build.index("-projectPath") + 1])
        player = unity.ran("Harness.exe")[-1]
        self.assertEqual(["-batchmode", "-nographics"], player[1:3])
        manifest = json.loads((work / "project" / "Packages" / "manifest.json").read_text(encoding="utf-8"))
        self.assertNotIn("com.cysharp.unitask", manifest["dependencies"])
        self.assertTrue((work / "project" / "Assets" / "Harness" / "Boot.cs").is_file())
        self.assertTrue((work / "project" / "Assets" / "Plugins" / "HarnessPlugin.dll").is_file())

    def test_a_failed_check_exits_one_and_names_it(self, _system: mock.Mock) -> None:
        unity = FakeUnity(PASSING_LOG.replace("inject.field PASS", "inject.field FAIL null"), player_status=3)

        code, lines, _ = self.run_fixture(unity)

        self.assertEqual(EXIT_CHECKS_FAILED, code)
        self.assertIn("FAIL inject.field (null)", lines)
        self.assertIn("FAIL: 1 checks failed: inject.field", lines)

    def test_a_failed_build_exits_three_without_running_a_player(self, _system: mock.Mock) -> None:
        unity = FakeUnity(build_status=1)

        code, lines, _ = self.run_fixture(unity)

        self.assertEqual(EXIT_BUILD_FAILED, code)
        self.assertEqual([], unity.ran("Harness.exe -batchmode"))
        self.assertEqual(1, len(unity.ran("-executeMethod")))
        self.assertTrue(any(line.startswith("error: the build exited with 1") for line in lines))

    def test_il2cpp_with_unitask_gets_its_own_player_and_the_pinned_unitask(self, _system: mock.Mock) -> None:
        root_work: list[Path] = []
        unity = FakeUnity()

        def options_for(work: Path) -> HarnessOptions:
            root_work.append(work)
            return HarnessOptions(backend="il2cpp", stripping="Medium", with_unitask=True, work_dir=work)

        root = fixture_root(self)
        editor = write(root / "Editor" / "Unity.exe", "")
        with mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)):
            code = run_harness(root, options_for(root / "work"), unity, lambda line: None)

        self.assertEqual(0, code)
        build = unity.ran("-executeMethod")[0]
        self.assertTrue(build[build.index("-mu3-out") + 1].endswith(os.path.join("players", "il2cpp-medium-unitask", "Harness.exe")))
        manifest = json.loads((root_work[0] / "project" / "Packages" / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(f"{UNITASK_URL}#abc123", manifest["dependencies"]["com.cysharp.unitask"])

    def test_the_plugin_compiles_again_only_after_its_source_changes(self, _system: mock.Mock) -> None:
        root = fixture_root(self)
        editor = write(root / "Editor" / "Unity.exe", "")
        work = root / "work"
        unity = FakeUnity()
        with mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)):
            run_harness(root, HarnessOptions(work_dir=work), unity, lambda line: None)
            run_harness(root, HarnessOptions(work_dir=work), unity, lambda line: None)
            source = root / "tools" / "di-strip-harness" / "Plugin" / "HarnessPlugin.cs"
            later = time.time() + 10
            os.utime(source, (later, later))
            run_harness(root, HarnessOptions(work_dir=work), unity, lambda line: None)

        compiles = unity.ran("-out:")
        self.assertEqual(2, len(compiles))
        self.assertIn("-sdk:4.5", compiles[0])
        self.assertTrue(compiles[0][1].endswith(os.path.join("lib", "mono", "4.5", "mcs.exe")))


class StripHarnessWorkDirTests(unittest.TestCase):
    def test_a_new_or_empty_folder_becomes_the_harnesss_own(self) -> None:
        root = temporary_root(self)
        empty = root / "empty"
        empty.mkdir()

        self.assertTrue(claim_work_dir(root / "new" / "work"))
        self.assertTrue((root / "new" / "work" / WORK_DIR_MARKER).is_file())
        self.assertFalse(claim_work_dir(root / "new" / "work"))
        self.assertTrue(claim_work_dir(empty))

    @mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows")
    def test_a_folder_without_the_marker_is_refused_and_keeps_every_file(self, _system: mock.Mock) -> None:
        root = fixture_root(self)
        editor = write(root / "Editor" / "Unity.exe", "")
        work = root / "work"
        # A Unity project where the mirror would be, as an earlier harness made one or a wrong --work-dir names one.
        asset = write(work / "project" / "Assets" / "Mine.cs", "// mine\n")
        unity = FakeUnity()

        with mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)):
            with self.assertRaisesRegex(HarnessSetupError, f"has no {re.escape(WORK_DIR_MARKER)} marker"):
                run_harness(root, HarnessOptions(work_dir=work), unity, lambda line: None)

        self.assertEqual("// mine\n", asset.read_text(encoding="utf-8"))
        self.assertFalse((work / WORK_DIR_MARKER).exists())
        self.assertFalse((work / "project" / "Packages").exists())
        self.assertEqual([], unity.commands)


class StripHarnessSetupFailureTests(unittest.TestCase):
    """A run that fails around the player, not in it, ends with one line and exit 3, never as a failed check."""

    def invoke(self, root: Path, unity: FakeUnity, *arguments: str) -> object:
        editor = write(root / "Editor" / "Unity.exe", "")
        with (
            mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows"),
            mock.patch("mu3_cli.strip_harness.repo_root", return_value=root),
            mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)),
            mock.patch("mu3_cli.strip_harness.run_with_timeout", new=unity),
        ):
            return CliRunner().invoke(app, ["unity", "di-strip-harness", "--work-dir", str(root / "work"), *arguments])

    def assert_setup_failure(self, result: object, cause: str) -> None:
        self.assertEqual(EXIT_BUILD_FAILED, result.exit_code, result.output)
        self.assertIsInstance(result.exception, SystemExit)
        self.assertNotIn("Traceback", result.output)
        [error] = [line for line in result.output.splitlines() if line.startswith("error:")]
        self.assertIn(cause, error)

    def test_each_setup_and_cleanup_failure_exits_three_with_its_cause(self) -> None:
        locked = PermissionError(13, "The process cannot access the file because it is being used by another process")
        floor = ("--editor-version", FLOOR_VERSION)

        def older_editor_with(relative: str, text: str) -> Callable[[Path], None]:
            """An older Editor whose bundled-package file at relative holds text."""

            def arrange(root: Path) -> None:
                fake_editor(root)
                write(root / "Editor" / "Data" / "Resources" / "PackageManager" / relative, text)

            return arrange

        def urp_dependencies(dependencies: str) -> Callable[[Path], None]:
            def arrange(root: Path) -> None:
                fake_editor(root)
                write(root / "UnityProject_URP" / "Packages" / "manifest.json", f'{{"dependencies": {dependencies}}}')

            return arrange

        cases = {
            "dependency version that is not a string": (urp_dependencies('{"com.unity.ugui": 2}'), floor, None,
                                                        "manifest.json is malformed: the dependency com.unity.ugui is not a string"),
            "Editor package manifest whose packages is not an object": (
                older_editor_with("Editor/manifest.json", '{"packages": []}'), floor, None, "packages is not a JSON object"),
            "Editor package manifest that is not an object": (
                older_editor_with("Editor/manifest.json", "[]"), floor, None, "manifest.json does not hold a JSON object"),
            "built-in copy that is not an object": (
                older_editor_with("BuiltInPackages/com.unity.modules.audio/package.json", "[]"), floor, None,
                "package.json does not hold a JSON object"),
            "lock file entry that is not an object": (
                lambda root: write(
                    root / "UnityProject_BuiltIn" / "Packages" / "packages-lock.json", '{"dependencies": {"com.cysharp.unitask": "x"}}'
                ),
                ("--with-unitask",), None, "the dependency com.cysharp.unitask is not a JSON object"),
            "unmarked work folder": (lambda root: write(root / "work" / "project" / "Assets" / "Mine.cs", ""), (), None,
                                     f"has no {WORK_DIR_MARKER} marker"),
            "malformed manifest": (lambda root: write(root / "UnityProject_URP" / "Packages" / "manifest.json", "{"), (), None,
                                   "manifest.json is not valid JSON: JSONDecodeError"),
            "manifest that is not an object": (
                lambda root: write(root / "UnityProject_URP" / "Packages" / "manifest.json", '{"dependencies": []}'), (), None,
                "manifest.json is not a package manifest"),
            "manifest without UniTask": (
                lambda root: write(root / "UnityProject_BuiltIn" / "Packages" / "manifest.json", '{"dependencies": {}}'),
                ("--with-unitask",), None, "has no com.cysharp.unitask dependency"),
            "UniTask lock file missing": (
                lambda root: (root / "UnityProject_BuiltIn" / "Packages" / "packages-lock.json").unlink(),
                ("--with-unitask",), None, "FileNotFoundError"),
            "locked mirror file": (lambda root: None, (), ("mu3_cli.strip_harness.write_if_changed", locked),
                                   "preparing the mirror project"),
        }
        for name, (arrange, arguments, failing, cause) in cases.items():
            with self.subTest(name):
                root = fixture_root(self)
                arrange(root)
                unity = FakeUnity()
                with mock.patch(failing[0], side_effect=failing[1]) if failing else contextlib.nullcontext():
                    result = self.invoke(root, unity, *arguments)

                self.assert_setup_failure(result, cause)
                self.assertEqual([], unity.ran("-executeMethod"))

    def test_a_previous_player_that_cannot_be_removed_exits_three(self) -> None:
        root = fixture_root(self)
        self.assertEqual(0, self.invoke(root, FakeUnity()).exit_code)
        unity = FakeUnity()

        with mock.patch("mu3_cli.strip_harness.shutil.rmtree", side_effect=PermissionError(13, "Access is denied")):
            result = self.invoke(root, unity)

        self.assert_setup_failure(result, "removing the previous mono-high player and logs failed: PermissionError: [Errno 13] Access is denied")
        self.assertEqual([], unity.ran("-executeMethod"))

    def test_a_player_that_cannot_start_exits_three(self) -> None:
        unity = FakeUnity()
        original = unity.__call__

        def missing_player(command: list[str], timeout: float) -> int | None:
            if command[0].endswith("Harness.exe"):
                raise FileNotFoundError(2, "The system cannot find the file specified")
            return original(command, timeout)

        result = self.invoke(fixture_root(self), missing_player)

        self.assert_setup_failure(result, "FileNotFoundError")

    def test_a_failed_check_still_exits_one(self) -> None:
        unity = FakeUnity(PASSING_LOG.replace("inject.field PASS", "inject.field FAIL"), player_status=1)

        result = self.invoke(fixture_root(self), unity)

        self.assertEqual(EXIT_CHECKS_FAILED, result.exit_code, result.output)
        self.assertNotIn("error:", result.output)

    def test_a_failure_after_the_player_ran_keeps_its_check_results(self) -> None:
        for name, unity, expected in (
            ("passing", FakeUnity(), 0),
            ("failing", FakeUnity(PASSING_LOG.replace("inject.field PASS", "inject.field FAIL"), player_status=1), EXIT_CHECKS_FAILED),
        ):
            with self.subTest(name):
                with mock.patch("mu3_cli.strip_harness.player_sizes", side_effect=PermissionError(13, "Access is denied")):
                    result = self.invoke(fixture_root(self), unity)

                self.assertEqual(expected, result.exit_code, result.output)
                lines = result.output.splitlines()
                self.assertIn("PASS core.initialized-event", lines)
                self.assertIn("warning: could not measure the player's DLLs: PermissionError: [Errno 13] Access is denied", lines)
                self.assertNotIn("error:", result.output)

    def test_a_timeout_stop_without_taskkill_still_stops_the_process(self) -> None:
        process = mock.Mock(pid=123)

        with (
            mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows"),
            mock.patch("mu3_cli.strip_harness.subprocess.run", side_effect=FileNotFoundError(2, "taskkill")),
        ):
            stop_process_tree(process)

        process.kill.assert_called_once_with()
        process.wait.assert_called_once_with()


class StripHarnessEditorVersionTests(unittest.TestCase):
    """--editor-version builds with another installed Editor, in a mirror retargeted to the packages it bundles."""

    def test_the_mirror_gets_its_own_folder_and_the_unity_packages_that_editor_bundles(self) -> None:
        root = fixture_root(self)
        manifest_file = root / "UnityProject_URP" / "Packages" / "manifest.json"
        reference = json.loads(manifest_file.read_text(encoding="utf-8"))
        reference["dependencies"]["com.unity.modules.tetgen"] = "1.0.0"
        write(manifest_file, json.dumps(reference))
        editor = fake_editor(root)
        work = root / "work"
        unity = FakeUnity()
        lines: list[str] = []
        options = HarnessOptions(stripping="Medium", work_dir=work, editor_version=FLOOR_VERSION)

        with (
            mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows"),
            mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)) as resolve,
        ):
            code = run_harness(root, options, unity, lines.append)

        self.assertEqual(0, code, lines)
        resolve.assert_called_once_with(FLOOR_VERSION)
        mirror = work / f"project-{FLOOR_VERSION}"
        build = unity.ran("-executeMethod")[0]
        self.assertEqual(str(mirror), build[build.index("-projectPath") + 1])
        self.assertTrue(build[build.index("-mu3-out") + 1].endswith(os.path.join("players", f"mono-medium-{FLOOR_VERSION}", "Harness.exe")))
        self.assertFalse((work / "project").exists())
        self.assertEqual(
            f"m_EditorVersion: {FLOOR_VERSION}\n", (mirror / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")
        )
        self.assertEqual(
            {
                "com.github.doqltl179.mu3library.base": "file:" + (root / "Mu3Library_Base").resolve().as_posix(),
                "com.unity.modules.audio": "1.0.0",
                "com.unity.ugui": "2.0.0",
            },
            json.loads((mirror / "Packages" / "manifest.json").read_text(encoding="utf-8"))["dependencies"],
        )
        self.assertIn("  - com.unity.ugui 2.6.0 -> 2.0.0", lines)
        self.assertIn(f"  - com.unity.modules.tetgen removed, Unity {FLOOR_VERSION} does not ship it", lines)
        self.assertIn(f"mono-medium-{FLOOR_VERSION}: 2 checks, 2 passed, 0 failed", lines)

        # Unity adds its revision line when it opens the mirror; the next run keeps it.
        opened = f"m_EditorVersion: {FLOOR_VERSION}\nm_EditorVersionWithRevision: {FLOOR_VERSION} (abc)\n"
        write(mirror / "ProjectSettings" / "ProjectVersion.txt", opened)
        lines.clear()
        with (
            mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows"),
            mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)),
        ):
            self.assertEqual(0, run_harness(root, options, unity, lines.append))
        self.assertEqual(opened, (mirror / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"))
        self.assertIn("Mirror changes: none", lines)

    @mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows")
    def test_an_override_that_is_not_the_requested_editor_is_named_in_a_warning(self, _system: mock.Mock) -> None:
        for name, location, editor_version, warning in (
            ("other Editor for --editor-version", "elsewhere", FLOOR_VERSION,
             f"warning: --editor-version requires Unity {FLOOR_VERSION} but the explicit override will be used: "),
            ("other Editor for the URP project", "elsewhere", None,
             "warning: UnityProject_URP requires Unity 6000.6.7f1 but the explicit override will be used: "),
            ("Hub install of the requested Editor", f"Hub/Editor/{FLOOR_VERSION}", FLOOR_VERSION, None),
        ):
            with self.subTest(name):
                root = fixture_root(self)
                editor = fake_editor(root / location)
                lines: list[str] = []

                with mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, True)):
                    code = run_harness(
                        root, HarnessOptions(work_dir=root / "work", editor_version=editor_version), FakeUnity(), lines.append
                    )

                self.assertEqual(0, code, lines)
                warnings = [line for line in lines if "the explicit override will be used" in line]
                self.assertEqual([warning + str(editor)] if warning else [], warnings)

    @mock.patch("mu3_cli.strip_harness.platform.system", return_value="Windows")
    def test_an_editor_without_bundled_packages_stops_before_unity_runs(self, _system: mock.Mock) -> None:
        root = fixture_root(self)
        editor = fake_editor(root, with_packages=False)
        unity = FakeUnity()

        with mock.patch("mu3_cli.strip_harness.resolve_editor_path", return_value=(editor, False)):
            with self.assertRaisesRegex(HarnessSetupError, f"cannot find the packages that Unity {FLOOR_VERSION} bundles"):
                run_harness(root, HarnessOptions(work_dir=root / "work", editor_version=FLOOR_VERSION), unity, lambda line: None)

        self.assertEqual([], unity.commands)

    def test_what_the_editor_does_not_bundle_is_kept_or_dropped_as_compile_unity_does(self) -> None:
        package_manager = editor_package_manager_dir(fake_editor(temporary_root(self)))

        dependencies, changes, warnings = retarget_dependencies(
            {
                "com.unity.inputsystem": "1.20.1",
                "com.unity.terrain-tools": "5.3.3",
                "com.unity.textmeshpro": "3.0.6",
                "com.unity.modules.tetgen": "1.0.0",
                "com.unity.git-package": "https://example.com/package.git",
                "com.example.pinned": "1.0.0",
            },
            package_manager,
            FLOOR_VERSION,
        )

        self.assertEqual(
            {
                "com.unity.inputsystem": "1.19.0",
                "com.unity.terrain-tools": "5.3.3",
                "com.unity.textmeshpro": "5.0.0",
                "com.unity.git-package": "https://example.com/package.git",
                "com.example.pinned": "1.0.0",
            },
            dependencies,
        )
        self.assertEqual(
            [
                "com.unity.inputsystem 1.20.1 -> 1.19.0",
                "com.unity.textmeshpro 3.0.6 -> 5.0.0",
                f"com.unity.modules.tetgen removed, Unity {FLOOR_VERSION} does not ship it",
            ],
            changes,
        )
        self.assertEqual([f"Unity {FLOOR_VERSION} does not bundle com.unity.terrain-tools, so the mirror keeps 5.3.3"], warnings)

    def test_bundled_versions_are_the_ones_compile_unity_reads(self) -> None:
        package_manager = editor_package_manager_dir(fake_editor(temporary_root(self)))
        names = [
            "com.unity.ugui",
            "com.unity.inputsystem",
            "com.unity.textmeshpro",
            "com.unity.modules.audio",
            "com.unity.modules.tetgen",
            "com.unity.terrain-tools",
        ]

        completed = run_sourced(
            repo_root() / "compile-unity.sh",
            'for name in "${@:2}"; do printf \'%s=%s\\n\' "$name" "$(editor_bundled_version "$1" "$name" || printf -- -)"; done',
            arguments=[package_manager.as_posix(), *names],
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        bundled = editor_bundled_versions(package_manager, names)
        self.assertEqual([f"{name}={bundled.get(name, '-')}" for name in names], completed.stdout.splitlines())
        self.assertEqual({"com.unity.ugui", "com.unity.inputsystem", "com.unity.textmeshpro", "com.unity.modules.audio"}, set(bundled))


class StripHarnessCommandTests(unittest.TestCase):
    def test_unknown_stripping_level_is_a_usage_error(self) -> None:
        result = CliRunner().invoke(app, ["unity", "di-strip-harness", "--stripping", "Strongest"])

        self.assertEqual(2, result.exit_code)
        self.assertTrue(re.search(r"--stripping expects one of", result.output))

    def test_unknown_backend_is_a_usage_error(self) -> None:
        result = CliRunner().invoke(app, ["unity", "di-strip-harness", "--backend", "dotnet"])

        self.assertEqual(2, result.exit_code)

    def test_editor_version_must_name_a_full_release(self) -> None:
        for version in ("6000.3", "../6000.3.17f1", "6000.3.17f1/x"):
            with self.subTest(version=version):
                result = CliRunner().invoke(app, ["unity", "di-strip-harness", "--editor-version", version])

                self.assertEqual(2, result.exit_code)
                self.assertIn("--editor-version expects a full Unity version", result.output)


if __name__ == "__main__":
    unittest.main()
