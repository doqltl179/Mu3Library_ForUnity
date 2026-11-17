from __future__ import annotations

import json
import os
import re
import subprocess
import tempfile
import unittest
from pathlib import Path
from typing import Sequence
from unittest import mock

from typer.testing import CliRunner

from test_unity_runs import FAKE_EDITOR_PACKAGES, FLOOR_VERSION
from common.checkout import repo_root
from multiplayer_harness.cli import (
    EXIT_CHECKS_FAILED,
    EXIT_SETUP_FAILED,
    PLAYER_EXIT_GRACE,
    PROJECT_PATH_LIMIT,
    TEMPLATE,
    WORK_DIR_MARKER,
    HarnessOptions,
    HarnessSetupError,
    PlayerRun,
    app,
    parse_player_log,
    project_path_problem,
    run_harness,
    run_problems,
)


MINIMAL = Path("tools", "unity-projects", "minimal")
HOST_LOG = """Mono path[0] = 'C:/x/MultiplayerHarness_Data/Managed'
MPHARNESS START role=host lobby=mpharness-1 timeout=120 network=mu3library-multiplayer-harness unity=6000.6.7f1 pid=1
MPHARNESS PLAN host backend.start lobby.create guest.kick
(Filename: C:\\build\\output\\Runtime\\Export\\Debug\\Debug.bindings.h Line: 39)

MPHARNESS CHECK backend.start PASS the Local backend as Harness Host (7)
MPHARNESS CHECK lobby.create PASS lobby ID 3, lobby data "map" "forest"
MPHARNESS INFO the guest's ping as this host measures it: 1 ms
MPHARNESS CHECK guest.kick PASS the guest left: Kicked
MPHARNESS RESULT PASS role=host checks=3 failures=0 errorLogs=0
"""
GUEST_LOG = """MPHARNESS START role=guest lobby=mpharness-1 timeout=120 network=mu3library-multiplayer-harness unity=6000.6.7f1 pid=2
MPHARNESS PLAN guest backend.start lobby.search kicked
MPHARNESS CHECK backend.start PASS the Local backend as Harness Guest (8)
MPHARNESS CHECK lobby.search PASS lobby ID 3, lobby data "map" "forest"
MPHARNESS CHECK kicked PASS the session ended: Kicked
MPHARNESS RESULT PASS role=guest checks=3 failures=0 errorLogs=0
"""
# A guest that never finds the lobby, as one given another lobby name does.
TIMED_OUT_GUEST_LOG = """MPHARNESS PLAN guest backend.start lobby.search kicked
MPHARNESS CHECK backend.start PASS the Local backend as Harness Guest (8)
MPHARNESS CHECK lobby.search FAIL timed out after 120 s
MPHARNESS RESULT FAIL role=guest checks=2 failures=1 errorLogs=0
"""


def write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


def fixture_root(test: unittest.TestCase) -> Path:
    """A repository with what the harness reads: the minimal config, Base, and the template.

    It is a Git repository, since the configs find Base by the name in its package.json among the
    files Git lists.
    """
    directory = tempfile.TemporaryDirectory()
    test.addCleanup(directory.cleanup)
    root = Path(directory.name) / "repo"
    git_config = write(Path(directory.name) / "gitconfig", "[safe]\n\tdirectory = *\n")
    environment = mock.patch.dict(os.environ, {"GIT_CONFIG_GLOBAL": str(git_config), "GIT_CONFIG_NOSYSTEM": "1"})
    environment.start()
    test.addCleanup(environment.stop)
    subprocess.run(["git", "init", "--quiet", str(root)], check=True)
    manifest = {
        "dependencies": {"com.github.doqltl179.mu3library.base": "checkout", "com.unity.ugui": "2.6.0"},
        "testables": ["com.github.doqltl179.mu3library.base"],
    }
    write(root / MINIMAL / "Packages" / "manifest.json", json.dumps(manifest, indent=2) + "\n")
    write(root / MINIMAL / "ProjectSettings" / "ProjectVersion.txt", "m_EditorVersion: 6000.6.7f1\n")
    write(root / "packages" / "base" / "package.json", '{"name": "com.github.doqltl179.mu3library.base"}\n')
    write(root / TEMPLATE / "Runtime" / "MultiplayerHarnessPlayer.cs", "// player\n")
    write(root / TEMPLATE / "Editor" / "MultiplayerHarnessBuild.cs", "// build\n")
    return root


def argument(command: Sequence[str], name: str) -> str:
    return command[list(command).index(name) + 1]


class FakeUnity:
    """Stands in for the Editor's build and for the two players, recording what each was asked to do."""

    def __init__(
        self, host_log: str = HOST_LOG, guest_log: str = GUEST_LOG, build_status: int | None = 0, exit_codes: tuple = (0, 0)
    ) -> None:
        self.logs = {"host": host_log, "guest": guest_log}
        self.build_status = build_status
        self.exit_codes = list(exit_codes)
        self.builds: list[list[str]] = []
        self.players: list[list[str]] = []
        self.player_timeouts: list[float] = []

    def build(self, command: list[str], timeout: float) -> int | None:
        self.builds.append(command)
        write(Path(argument(command, "-logFile")), "MPHARNESS SETTINGS backend=Mono2x\nMPHARNESS BUILD Succeeded errors=0\n")
        if self.build_status == 0:
            write(Path(argument(command, "-mu3-out")), "exe")
        return self.build_status

    def run_players(self, commands: Sequence[list[str]], timeout: float) -> list[int | None]:
        self.players += commands
        self.player_timeouts.append(timeout)
        for command in commands:
            write(Path(argument(command, "-logFile")), self.logs[argument(command, "-mpharness-role")])
        return self.exit_codes


@mock.patch("multiplayer_harness.cli.platform.system", return_value="Windows")
class MultiplayerHarnessRunTests(unittest.TestCase):
    def setUp(self) -> None:
        self.root = fixture_root(self)
        self.work = self.root.parent / "work"
        self.editor = write(self.root.parent / "Editor" / "Unity.exe", "")
        patcher = mock.patch("multiplayer_harness.cli.resolve_editor_path", return_value=(self.editor, False))
        self.resolve = patcher.start()
        self.addCleanup(patcher.stop)
        self.output: list[str] = []

    def run_tool(self, unity: FakeUnity, **options: object) -> int:
        return run_harness(
            self.root, HarnessOptions(work_dir=self.work, **options), unity.build, unity.run_players, self.output.append
        )

    def test_a_passing_pair_exits_zero_and_prints_each_players_checks(self, _system: mock.Mock) -> None:
        unity = FakeUnity()

        self.assertEqual(0, self.run_tool(unity, player_timeout=60), self.output)

        project = self.work / "minimal"
        self.assertTrue((self.work / WORK_DIR_MARKER).is_file())
        self.assertTrue((project / "Packages" / "manifest.json").is_file())
        self.assertTrue((project / "Assets" / "MultiplayerHarness" / "Runtime" / "MultiplayerHarnessPlayer.cs").is_file())
        self.assertTrue((project / "Assets" / "MultiplayerHarness" / "Editor" / "MultiplayerHarnessBuild.cs").is_file())
        build = unity.builds[0]
        self.assertEqual(str(project), argument(build, "-projectPath"))
        self.assertEqual("MultiplayerHarnessBuild.BuildPlayer", argument(build, "-executeMethod"))

        host, guest = unity.players
        self.assertEqual(["host", "guest"], [argument(host, "-mpharness-role"), argument(guest, "-mpharness-role")])
        self.assertEqual(argument(host, "-mpharness-lobby"), argument(guest, "-mpharness-lobby"))
        self.assertNotEqual(argument(host, "-logFile"), argument(guest, "-logFile"))
        self.assertEqual(["-batchmode", "-nographics"], host[1:3])
        self.assertEqual("60", argument(host, "-mpharness-timeout"))
        self.assertEqual([60 + PLAYER_EXIT_GRACE], unity.player_timeouts)

        self.assertIn('host: PASS lobby.create (lobby ID 3, lobby data "map" "forest")', self.output)
        self.assertIn("guest: PASS kicked (the session ended: Kicked)", self.output)
        self.assertIn("host: INFO the guest's ping as this host measures it: 1 ms", self.output)
        self.assertIn("guest: 3 checks, 3 passed, 0 failed, 0 not reached; exit 0", self.output)
        self.assertEqual("PASS: the host and the guest passed every check (harness)", self.output[-1])

    def test_a_guest_that_never_finds_the_lobby_exits_one_and_names_what_it_did_not_reach(self, _system: mock.Mock) -> None:
        unity = FakeUnity(guest_log=TIMED_OUT_GUEST_LOG, exit_codes=(0, 1))

        self.assertEqual(EXIT_CHECKS_FAILED, self.run_tool(unity))

        self.assertIn("guest: FAIL lobby.search (timed out after 120 s)", self.output)
        self.assertIn("guest: NOT REACHED kicked", self.output)
        self.assertIn("FAIL: guest: 1 checks failed: lobby.search", self.output)
        self.assertIn("FAIL: guest: 1 planned checks were not reached: kicked", self.output)
        self.assertFalse(any(line.startswith("FAIL: host") for line in self.output), self.output)

    def test_a_failed_build_exits_three_without_starting_the_players(self, _system: mock.Mock) -> None:
        unity = FakeUnity(build_status=1)

        self.assertEqual(EXIT_SETUP_FAILED, self.run_tool(unity))

        self.assertEqual([], unity.players)
        self.assertIn("MPHARNESS BUILD Succeeded errors=0", self.output)
        self.assertTrue(any(line.startswith("error: the build exited with 1") for line in self.output), self.output)

    def test_a_later_run_refreshes_the_template_and_keeps_what_unity_made(self, _system: mock.Mock) -> None:
        self.assertEqual(0, self.run_tool(FakeUnity()))
        folder = self.work / "minimal" / "Assets" / "MultiplayerHarness"
        write(folder / "Harness.unity", "scene")
        write(folder / "Runtime" / "MultiplayerHarnessPlayer.cs.meta", "guid")
        write(folder / "Runtime" / "Removed.cs", "an earlier template's file")
        write(self.root / TEMPLATE / "Runtime" / "MultiplayerHarnessPlayer.cs", "// player, changed\n")
        self.output.clear()

        self.assertEqual(0, self.run_tool(FakeUnity()))

        self.assertIn(
            "Template changes: Assets/MultiplayerHarness/Runtime/MultiplayerHarnessPlayer.cs, "
            "Assets/MultiplayerHarness/Runtime/Removed.cs",
            self.output,
        )
        self.assertFalse((folder / "Runtime" / "Removed.cs").exists())
        self.assertTrue((folder / "Harness.unity").is_file())
        self.assertTrue((folder / "Runtime" / "MultiplayerHarnessPlayer.cs.meta").is_file())

    def test_another_editor_gets_a_project_and_logs_of_its_own(self, _system: mock.Mock) -> None:
        for relative, content in FAKE_EDITOR_PACKAGES.items():
            write(self.editor.parent / "Data" / "Resources" / "PackageManager" / relative, json.dumps(content, indent=2) + "\n")
        unity = FakeUnity()

        self.assertEqual(0, self.run_tool(unity, editor_version=FLOOR_VERSION), self.output)

        self.resolve.assert_called_once_with(FLOOR_VERSION)
        project = self.work / f"minimal-{FLOOR_VERSION}"
        self.assertEqual(str(project), argument(unity.builds[0], "-projectPath"))
        self.assertEqual(f"m_EditorVersion: {FLOOR_VERSION}\n", (project / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"))
        self.assertTrue(argument(unity.players[0], "-logFile").endswith(f"harness-{FLOOR_VERSION}-host.log"))
        self.assertFalse((self.work / "minimal").exists())

    def test_a_folder_it_did_not_make_is_refused_before_anything_is_written(self, _system: mock.Mock) -> None:
        write(self.work / "keep.txt", "keep")

        with self.assertRaisesRegex(HarnessSetupError, f"has no {WORK_DIR_MARKER} marker"):
            self.run_tool(FakeUnity())
        self.assertEqual(["keep.txt"], [path.name for path in self.work.iterdir()])


class MultiplayerHarnessLogTests(unittest.TestCase):
    def test_parser_reads_the_plan_checks_result_and_notes_among_unity_lines(self) -> None:
        run = parse_player_log(HOST_LOG)

        self.assertEqual(["backend.start", "lobby.create", "guest.kick"], run.plan)
        self.assertEqual(["backend.start", "lobby.create", "guest.kick"], [check.name for check in run.checks])
        self.assertTrue(all(check.passed for check in run.checks))
        self.assertEqual(("PASS", 3, 0), (run.result, run.declared_checks, run.error_logs))
        self.assertEqual("INFO the guest's ping as this host measures it: 1 ms", run.transcript[3])
        self.assertEqual("PASS guest.kick (the guest left: Kicked)", run.transcript[4])
        self.assertEqual([], run_problems(run, 0))

    def test_each_way_a_run_can_fail_is_named(self) -> None:
        timed_out = parse_player_log(TIMED_OUT_GUEST_LOG)
        errors = parse_player_log(GUEST_LOG.replace("errorLogs=0", "errorLogs=2"))
        no_result = parse_player_log("\n".join(line for line in GUEST_LOG.splitlines() if "RESULT" not in line))

        self.assertIn("exited with 1", run_problems(timed_out, 1))
        self.assertIn("reported FAIL", run_problems(timed_out, 1))
        self.assertIn("1 planned checks were not reached: kicked", run_problems(timed_out, 1))
        self.assertIn("logged 2 errors or exceptions", run_problems(errors, 0))
        self.assertIn("logged no MPHARNESS RESULT line", run_problems(no_result, 0))
        self.assertIn("did not exit before the timeout and was stopped", run_problems(parse_player_log(GUEST_LOG), None))
        self.assertIn("logged no MPHARNESS PLAN line", run_problems(PlayerRun(), 0))

    def test_a_deep_project_folder_is_refused_on_windows_only(self) -> None:
        deep = Path("D:/" + "a" * (PROJECT_PATH_LIMIT - 3))

        self.assertIsNone(project_path_problem(Path("D:/" + "a" * (PROJECT_PATH_LIMIT - 4)), "Windows"))
        self.assertIn(f"has {PROJECT_PATH_LIMIT} characters", project_path_problem(deep, "Windows"))
        self.assertIsNone(project_path_problem(deep, "Linux"))


class MultiplayerHarnessTemplateTests(unittest.TestCase):
    def test_the_player_drives_base_without_the_di_package_and_logs_the_lines_the_parser_reads(self) -> None:
        template = repo_root() / TEMPLATE
        asmdef = json.loads((template / "Runtime" / "MultiplayerHarness.asmdef").read_text(encoding="utf-8"))
        player = (template / "Runtime" / "MultiplayerHarnessPlayer.cs").read_text(encoding="utf-8")
        build = (template / "Editor" / "MultiplayerHarnessBuild.cs").read_text(encoding="utf-8")

        # The player references Base's Multiplayer and the contracts it implements, and no container.
        self.assertEqual(["Mu3Library.Multiplayer", "Mu3Library.Foundation"], asmdef["references"])
        self.assertNotIn("Mu3Library.DI", re.sub(r"//.*", "", player))
        self.assertIn("_manager.Update();", player)
        self.assertIn('Debug.Log($"{Prefix}PLAN {RoleName} {string.Join(" ", names)}");', player)
        self.assertIn('Debug.Log($"{Prefix}CHECK {name} {(verdict.IsPassed ? "PASS" : "FAIL")} {verdict.Detail}");', player)
        self.assertIn("{Prefix}RESULT {(pass ? \"PASS\" : \"FAIL\")} role={RoleName} checks={_checks} failures={_failures} errorLogs={errorLogs}", player)
        self.assertIn("public static void BuildPlayer()", build)


class MultiplayerHarnessCommandTests(unittest.TestCase):
    def test_timeouts_must_be_positive_and_an_editor_version_a_full_release(self) -> None:
        for arguments in (["--player-timeout", "0"], ["--build-timeout", "-1"], ["--editor-version", "6000.3"]):
            with self.subTest(arguments=arguments):
                self.assertEqual(2, CliRunner().invoke(app, arguments).exit_code)


if __name__ == "__main__":
    unittest.main()
