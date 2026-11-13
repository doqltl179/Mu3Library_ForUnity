from __future__ import annotations

import json
import os
import platform
import re
import shutil
import signal
import subprocess
import tempfile
import time
import unittest
from pathlib import Path

from fixture_repository import FAKE_UNITY, LOAD_HELPER, MAPPING, FixtureRepositoryTestCase, repository_ignore_line
from shell_harness import bash_or_skip
from test_compile_selection import CAN_CHECK_LOCK_HOLDERS, run_plan
from mu3_cli import fixed_load
from mu3_cli.repository import repo_root
from mu3_cli.unity import (
    bash_script_command,
    is_directory_link,
    link_samples,
    load_target_specs,
    remove_directory_link,
    unlink_samples,
)

# rm and sleep become shell functions, which take precedence over the commands on PATH.
FAILING_REMOVAL = """rm() { printf 'rm: cannot remove: Device or resource busy\\n' >&2; return 1; }
sleep() { :; }
"""


class MirrorCleanupTests(FixtureRepositoryTestCase):
    """A mirror that cannot be removed must not decide what the run reports."""

    def compile_with_failing_removal(self, *arguments: str, unity_status: int = 0):
        return self.run_functions(
            "compile-unity.sh",
            FAILING_REMOVAL + 'compile_main "$@"',
            *arguments,
            environment=self.unity_environment(FAKE_UNITY_STATUS=str(unity_status)),
        )

    def test_mirror_is_removed_after_the_run(self) -> None:
        completed = self.run_entry_point("compile-unity.sh", "leaf", "--isolated", environment=self.unity_environment())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout)
        self.assertEqual([], self.mirrors_left())

    def test_failed_removal_still_reports_success_and_runs_every_target(self) -> None:
        completed = self.compile_with_failing_removal("all", "--isolated")

        self.assertEqual(0, completed.returncode, completed.stderr)
        for key, _, _ in MAPPING:
            self.assertIn(f"Unity compilation succeeded: {key}", completed.stdout.splitlines())
        warnings = [line for line in completed.stderr.splitlines() if "could not remove the isolated project" in line]
        self.assertEqual(len(MAPPING), len(warnings), completed.stderr)
        self.assertIn(str(self.mirrors_left()[0].name), completed.stderr)
        self.assertIn("warning: rm: cannot remove: Device or resource busy", completed.stderr)

    def test_failed_removal_keeps_the_unity_exit_code(self) -> None:
        completed = self.compile_with_failing_removal("leaf", "--isolated", unity_status=3)

        self.assertEqual(3, completed.returncode, completed.stderr)
        self.assertIn("error: Unity compilation failed for leaf (exit 3)", completed.stderr)
        self.assertIn("could not remove the isolated project", completed.stderr)

    def test_removal_retries_before_it_gives_up(self) -> None:
        # The first two attempts fail, as they do while a Unity child process still holds a file.
        # Each attempt runs in its own subshell, so a file counts them.
        flaky_removal = """rm() {
    printf x >> "$TMPDIR/removal-attempts"
    if [[ "$(wc -c < "$TMPDIR/removal-attempts")" -le 2 ]]; then return 1; fi
    command rm "$@"
}
sleep() { :; }
"""
        completed = self.run_functions(
            "compile-unity.sh",
            flaky_removal + 'compile_main "$@"',
            "leaf",
            "--isolated",
            environment=self.unity_environment(),
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertNotIn("could not remove", completed.stderr)
        self.assertEqual("xxx", (self.mirror_directory / "removal-attempts").read_text(encoding="utf-8"))
        self.assertEqual([], self.mirrors_left())


class SampleLinkPreflightTests(FixtureRepositoryTestCase):
    """A project whose samples link is missing must not compile as if it had its samples."""

    SAMPLE_LINKS = {"leaf": "Assets/Samples"}

    def link_leaf_samples(self) -> None:
        specs = load_target_specs(self.root)
        [result] = link_samples({"leaf": specs["leaf"]}, self.root)
        self.assertTrue(result.ok, result.message)
        self.addCleanup(remove_directory_link, self.root / "ProjLeaf" / "Assets" / "Samples")

    def test_missing_link_stops_the_run_before_unity_starts(self) -> None:
        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=self.unity_environment())

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("error: leaf: ProjLeaf/Assets/Samples is missing, so its samples would not compile.", completed.stderr)
        self.assertIn("unity link-samples", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)

    def test_without_samples_runs_and_says_what_it_left_out(self) -> None:
        completed = self.run_entry_point(
            "compile-unity.sh", "leaf", "--without-samples", environment=self.unity_environment()
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("leaf: ProjLeaf/Assets/Samples is missing, so its samples are left out", completed.stderr)
        self.assertIn("fake unity ran", completed.stdout)

    def test_dry_run_warns_about_a_missing_link(self) -> None:
        completed = self.run_entry_point("compile-unity.sh", "leaf", "--dry-run")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("warning: leaf: ProjLeaf/Assets/Samples is missing", completed.stderr)

    def test_real_folder_in_place_of_the_link_stops_the_run(self) -> None:
        folder = self.root / "ProjLeaf" / "Assets" / "Samples"
        folder.mkdir()
        self.addCleanup(folder.rmdir)

        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=self.unity_environment())

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("ProjLeaf/Assets/Samples does not link to PkgLeaf/Samples~", completed.stderr)

    def test_linked_samples_are_copied_into_the_isolated_mirror(self) -> None:
        self.link_leaf_samples()

        completed = self.run_entry_point(
            "compile-unity.sh", "leaf", "--isolated", "--keep-staging", environment=self.unity_environment()
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertNotIn("Samples", completed.stderr)
        [mirror] = self.mirrors_left()
        copied = mirror / "ProjLeaf" / "Assets" / "Samples"
        self.assertTrue((copied / "Sample.cs").is_file())
        self.assertFalse(is_directory_link(copied))


def repository_sample_links() -> dict[str, str]:
    """Each repository target's samples link, relative to the repository."""
    return {
        key: (spec.project_path / spec.samples_link).as_posix()
        for key, spec in load_target_specs(repo_root()).items()
        if spec.samples_link is not None
    }


class SampleLinkIgnoreTests(FixtureRepositoryTestCase):
    """A samples link is an untracked path inside a project's Assets/, so unless Git ignores it,
    changed selection picks it up on every run."""

    SAMPLE_LINKS = {"leaf": "Assets/Samples"}

    @classmethod
    def setUpClass(cls) -> None:
        # The fixture ignores its link with a line written exactly as the repository writes its own.
        real_link = next(iter(repository_sample_links().values()))
        real_line = repository_ignore_line(real_link)
        cls.GITIGNORE_LINES = (real_line.replace(real_link, "ProjLeaf/Assets/Samples"),) if real_line else ()
        super().setUpClass()

    def test_every_repository_samples_link_is_ignored_in_a_form_that_matches_a_symbolic_link(self) -> None:
        for key, link in repository_sample_links().items():
            with self.subTest(target=key):
                line = repository_ignore_line(link)
                self.assertIsNotNone(line, f"{link} has no .gitignore line")
                # Git applies a pattern that ends in / only to real directories.
                self.assertFalse(line.endswith("/"), line)

    def test_linked_samples_select_no_target(self) -> None:
        [result] = link_samples({"leaf": load_target_specs(self.root)["leaf"]}, self.root)
        self.assertTrue(result.ok, result.message)
        self.addCleanup(remove_directory_link, self.root / "ProjLeaf" / "Assets" / "Samples")

        for entry_point, heading in (("compile-unity.sh", "compile"), ("test-unity.sh", "test")):
            with self.subTest(entry_point=entry_point):
                completed = self.run_entry_point(entry_point, "changed", "--dry-run")

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertIn("Git changes inspected: 0", completed.stdout.splitlines(), completed.stdout)
                self.assertIn(f"Selected {heading} targets: none", completed.stdout.splitlines())


class WorktreeRemovalTests(FixtureRepositoryTestCase):
    """On Windows git worktree remove stops at the first junction it meets and leaves the
    worktree's folder behind, so a linked worktree is removed only after unlink-samples."""

    SAMPLE_LINKS = {"leaf": "Assets/Samples"}
    # Ignored as the repository ignores its links, so the linked worktree counts as clean.
    GITIGNORE_LINES = ("ProjLeaf/Assets/Samples",)

    def remove_leftover(self, worktree: Path) -> None:
        # The link goes first, so that nothing walks through it into the folder it points at.
        link = worktree / "ProjLeaf" / "Assets" / "Samples"
        if is_directory_link(link):
            remove_directory_link(link)
        shutil.rmtree(worktree, ignore_errors=True)
        self._git("worktree", "prune")

    def test_worktree_whose_samples_were_unlinked_is_removed_with_its_folder(self) -> None:
        worktree = self.root.parent / "unit"
        self._git("worktree", "add", "--detach", str(worktree))
        self.addCleanup(self.remove_leftover, worktree)
        # Git does not track the fixture's empty Assets folder, which a real project fills.
        (worktree / "ProjLeaf" / "Assets").mkdir()
        specs = load_target_specs(worktree)
        [linked] = link_samples(specs, worktree)
        self.assertTrue(linked.ok, linked.message)

        [unlinked] = unlink_samples(specs, worktree)
        self.assertTrue(unlinked.ok, unlinked.message)
        self.assertTrue((worktree / "PkgLeaf" / "Samples~" / "Sample.cs").is_file())
        completed = subprocess.run(
            ["git", "worktree", "remove", str(worktree)],
            cwd=self.root,
            env=self._environment(),
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertFalse(os.path.lexists(worktree))
        self.assertTrue((self.root / "PkgLeaf" / "Samples~" / "Sample.cs").is_file())


def nunit_results(
    passed: int = 3,
    failed: tuple[str, ...] = (),
    skipped: int = 0,
    inconclusive: tuple[str, ...] = (),
    messages: dict[str, str] | None = None,
) -> str:
    """An NUnit 3 result file of the shape the Unity Test Framework writes.

    The failed and inconclusive tests are named, in a fixture whose result NUnit takes from its tests:
    Failed when one failed, Passed when one passed, and Inconclusive when every one was inconclusive.
    A failed test named in messages carries that failure message, as the framework writes it.
    """
    total = passed + len(failed) + len(inconclusive) + skipped
    result = "Failed" if failed else "Passed" if passed or not inconclusive else "Inconclusive"
    cases = "\n".join(
        test_case(index, name, case_result, (messages or {}).get(name))
        for index, (name, case_result) in enumerate(
            [(name, "Failed") for name in failed] + [(name, "Inconclusive") for name in inconclusive], start=1000
        )
    )
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<test-run id="2" testcasecount="{total}" result="{result}" total="{total}" '
        f'passed="{passed}" failed="{len(failed)}" inconclusive="{len(inconclusive)}" skipped="{skipped}" asserts="0">\n'
        f'  <test-suite type="TestFixture" id="999" name="Tests" fullname="Fixture.Tests" result="{result}">\n'
        f"{cases}\n"
        "  </test-suite>\n"
        "</test-run>"
    )


def test_case(index: int, name: str, result: str, message: str | None) -> str:
    attributes = f'id="{index}" name="{name}" fullname="Fixture.Tests.{name}" result="{result}"'
    if message is None:
        return f"    <test-case {attributes} />"
    return (
        f"    <test-case {attributes}>\n"
        "      <failure>\n"
        f"        <message><![CDATA[{message}]]></message>\n"
        "      </failure>\n"
        "    </test-case>"
    )


# The lines the Unity Test Framework logs, with -automated, as a test begins and ends.
UTP_BEGIN = (
    '##utp:{"type":"TestStatus","time":1,"version":2,"phase":"Begin","processId":1,'
    '"name":"Fixture.Tests.Hangs","state":0}'
)
UTP_END = (
    '##utp:{"type":"TestStatus","time":2,"version":2,"phase":"End","processId":1,'
    '"name":"Fixture.Tests.Hangs","state":4,"message":"","duration":1}'
)
# The summary line under each run that counts the Unity processes the run did not start; other
# Unity processes may run on the machine while the tests do.
OTHER_UNITY_PROCESSES = r"^    other Unity processes: \d+ at the start, \d+( to \d+)? while it ran \(\d+ look\(s\)\)$"
# The line Unity 6000.6.7f1 logs as it launches its Data Store service, with the space it ends in.
DATA_STORE_LAUNCH = "Launching Unity Data Store - C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Data/Tools/Unity.DataStore.exe "
# The error Unity 6000.6.7f1 logged once its Data Store service had ended during a run (#393).
DATA_STORE_LOSS = (
    "Unity Data Store service (pid 13480) is no longer running. Artifacts will be read in process for the rest of "
    "this session, and the service will not be restarted."
)
# The failure messages of a test that failed on that error alone: as the Unity Test Framework writes
# it, and as the TearDown of Mu3WindowTests did in #393, with the stack trace Unity logged, which ends
# in a line break.
FRAMEWORK_LOSS_FAILURE = f"Unhandled log message: '[Error] {DATA_STORE_LOSS}'. Use UnityEngine.TestTools.LogAssert.Expect"
TEARDOWN_LOSS_FAILURE = (
    f"TearDown : Unhandled log message: [Error] {DATA_STORE_LOSS}\n"
    "Stack trace of the first:\n"
    "UnityEditor.AssetDatabase:LoadAssetAtPath<Mu3Library.Editor.Window.Drawer.DrawerListDrawer> (string)\n"
    "Mu3Library.Tests.Mu3WindowTests:DeleteCreatedPaths (bool) (at Mu3WindowTests.cs:502)\n"
)


def utp_status(phase: str, name: str, message: str = "") -> str:
    """The line -automated adds to Unity's log as the test named begins or, with its failure message, ends."""
    if phase == "Begin":
        return f'##utp:{{"type":"TestStatus","time":1,"version":2,"phase":"Begin","processId":1,"name":{json.dumps(name)},"state":0}}'
    return (
        f'##utp:{{"type":"TestStatus","time":2,"version":2,"phase":"End","processId":1,"name":{json.dumps(name)},'
        f'"state":{5 if message else 4},"message":{json.dumps(message)},"duration":1}}'
    )


def data_store_loss_log(running: str | None) -> str:
    """Unity's log of a run whose Data Store service ended while the test named ran, or between two tests.

    The lines take the forms of the run in #393: Unity logs the error as a line of its own and as a
    LogEntry line, and the Unity Test Framework fails the test that was running with it.
    """
    lines = [utp_status("Begin", "Fixture.Tests.Before"), utp_status("End", "Fixture.Tests.Before")]
    if running is None:
        lines += [DATA_STORE_LOSS, utp_error(DATA_STORE_LOSS)]
        lines += [utp_status("Begin", "Fixture.Tests.After"), utp_status("End", "Fixture.Tests.After")]
    else:
        lines += [utp_status("Begin", running), DATA_STORE_LOSS, utp_error(DATA_STORE_LOSS)]
        lines.append(utp_status("End", running, FRAMEWORK_LOSS_FAILURE))
    return "\n".join(lines)


class TestEntryPointTests(FixtureRepositoryTestCase):
    """Runs the real test-unity.sh with a fake Unity that writes the results it is given."""

    TESTABLE_PROJECTS = frozenset({"ProjShared", "ProjLeaf"})

    def run_tests(self, *arguments: str, results: str = "", unity_status: int = 0, **fake: str):
        """Run test-unity.sh; fake sets FAKE_UNITY_<NAME> variables, such as log, ticks, and sleep."""
        return self.run_entry_point(
            "test-unity.sh",
            *arguments,
            environment=self.unity_environment(
                FAKE_UNITY_RESULTS=results,
                FAKE_UNITY_STATUS=str(unity_status),
                **{f"FAKE_UNITY_{name.upper()}": value for name, value in fake.items()},
            ),
        )

    def results_file(self, target: str, platform: str = "editmode") -> Path:
        return self.root / "log" / "unity-tests" / f"{target}-{platform}.xml"

    def log_file(self, target: str, platform: str = "editmode") -> Path:
        return self.results_file(target, platform).with_suffix(".log")

    def test_passing_run_reports_its_counts(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(passed=3, skipped=1))

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("EditMode tests passed for leaf: 4 tests, 3 passed, 0 failed, 1 skipped, 0 inconclusive", completed.stdout)
        self.assertIn("  - leaf: 4 tests, 3 passed, 0 failed, 1 skipped, 0 inconclusive", completed.stdout.splitlines())
        self.assertNotIn("inconclusive: ", completed.stdout + completed.stderr)
        self.assertTrue(self.results_file("leaf").is_file())

    def test_unity_runs_the_editmode_tests_without_quit(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results())

        [arguments] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity arguments: ")]
        self.assertIn("-batchmode", arguments)
        self.assertIn("-runTests -testPlatform EditMode -testResults ", arguments)
        self.assertIn("-logFile -", arguments)
        self.assertNotIn("-quit", arguments)

    def test_playmode_runs_the_playmode_tests_into_their_own_results(self) -> None:
        completed = self.run_tests("leaf", "--platform", "playmode", results=nunit_results(passed=2))

        self.assertEqual(0, completed.returncode, completed.stderr)
        [arguments] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity arguments: ")]
        self.assertIn("-runTests -testPlatform PlayMode -testResults ", arguments)
        self.assertIn("PlayMode tests passed for leaf: 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive", completed.stdout)
        self.assertIn("PlayMode test summary:", completed.stdout.splitlines())
        self.assertTrue(self.results_file("leaf", "playmode").is_file())
        self.assertFalse(self.results_file("leaf").is_file())

    def test_unknown_platform_stops_before_unity_runs(self) -> None:
        completed = self.run_tests("leaf", "--platform", "PlayMode", results=nunit_results())

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("--platform expects editmode or playmode: PlayMode", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)

    def test_filter_reaches_unity_as_one_test_filter_argument(self) -> None:
        pattern = "Fixture.Tests.Benchmark;Fixture.Tests.Case(\"two words\")"
        completed = self.run_tests("leaf", "--filter", pattern, results=nunit_results(passed=1))

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(f"Test filter: {pattern}", completed.stdout.splitlines())
        [arguments] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity arguments: ")]
        self.assertIn(f"-runTests -testPlatform EditMode -testFilter {pattern} -testResults ", arguments)
        self.assertIn(f"fake unity test filter: [{pattern}]", completed.stdout.splitlines())
        self.assertIn("EditMode tests passed for leaf: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive", completed.stdout)

    def test_filter_is_left_out_of_git_bash_path_conversion_by_its_first_part(self) -> None:
        pattern = "Fixture.Tests.Case(a=/b);Fixture.Tests.Other"
        completed = self.run_tests("leaf", "--filter", pattern, results=nunit_results(passed=1))

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(
            "fake unity path conversion exclusions: [Fixture.Tests.Case(a=/b)]", completed.stdout.splitlines()
        )
        self.assertIn(f"fake unity test filter: [{pattern}]", completed.stdout.splitlines())

    def test_run_without_filter_passes_no_test_filter(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertNotIn("-testFilter", completed.stdout)
        self.assertNotIn("Test filter:", completed.stdout)

    def test_filter_that_matches_no_test_fails_and_names_the_filter(self) -> None:
        completed = self.run_tests("leaf", "--filter", "Fixture.Tests.Missing", results=nunit_results(passed=0))

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("error: leaf: no test matched --filter Fixture.Tests.Missing", completed.stderr)

    def test_filter_needs_one_pattern_and_stops_before_unity_runs_without_it(self) -> None:
        for arguments, message in (
            (("--filter",), "--filter requires a test name or pattern"),
            (("--filter", ""), "--filter requires a test name or pattern"),
            (("--filter", "  "), "--filter requires a test name or pattern"),
            (("--filter", "--dry-run"), "--filter expects a test name or pattern, not an option: --dry-run"),
            (("--filter", "/Mu3/Regex"), "--filter cannot start with /, which Git Bash rewrites as a file path"),
            (("--filter", "A", "--filter", "B"), "--filter can be given once"),
        ):
            with self.subTest(arguments=arguments):
                completed = self.run_tests("leaf", *arguments, results=nunit_results())

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn(message, completed.stderr)
                self.assertNotIn("fake unity ran", completed.stdout)

    def test_dry_run_shows_the_filter(self) -> None:
        completed = self.run_tests("leaf", "--filter", "Fixture.Tests.Benchmark", "--dry-run")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Test filter: Fixture.Tests.Benchmark", completed.stdout.splitlines())
        self.assertNotIn("fake unity ran", completed.stdout)

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_filtered_run_of_an_open_project_runs_in_the_mirror(self) -> None:
        lock_file = self.root / "ProjLeaf" / "Temp" / "UnityLockfile"
        lock_file.parent.mkdir(parents=True)
        lock_file.write_text("", encoding="utf-8")
        self.addCleanup(shutil.rmtree, lock_file.parent)

        with lock_file.open("rb"):
            completed = self.run_tests(
                "leaf", "--filter", "Fixture.Tests.Benchmark", results=nunit_results(passed=1)
            )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Testing leaf (ProjLeaf) in an isolated mirror because ProjLeaf is open in Unity.", completed.stdout)
        self.assertIn("fake unity test filter: [Fixture.Tests.Benchmark]", completed.stdout.splitlines())
        self.assertTrue(self.results_file("leaf").is_file())
        self.assertEqual([], self.mirrors_left())

    def test_unity_logs_each_test_into_a_log_kept_beside_the_results(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(), log=UTP_BEGIN)

        self.assertEqual(0, completed.returncode, completed.stderr)
        [arguments] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity arguments: ")]
        self.assertIn(" -automated ", arguments)
        # The script still prints the log, and keeps it after the run.
        self.assertIn(UTP_BEGIN, completed.stdout.splitlines())
        log = self.log_file("leaf").read_text(encoding="utf-8").splitlines()
        self.assertIn(UTP_BEGIN, log)
        self.assertIn(arguments, log)

    def test_silent_unity_is_stopped_and_the_run_names_the_test_it_was_in(self) -> None:
        for log, phrase in (
            (UTP_BEGIN, "while running Fixture.Tests.Hangs"),
            (f"{UTP_BEGIN}\n{UTP_END}", "while no test was running"),
        ):
            with self.subTest(phrase=phrase):
                sleeper_directory = tempfile.TemporaryDirectory()
                self.addCleanup(sleeper_directory.cleanup)
                sleeper_file = Path(sleeper_directory.name) / "sleeper.pid"
                started = time.monotonic()
                completed = self.run_tests(
                    "leaf",
                    "--stall-timeout",
                    "1",
                    results=nunit_results(),
                    log=log,
                    sleep="120",
                    sleeper_file=str(sleeper_file),
                )

                self.assertLess(time.monotonic() - started, 60, "the silent Unity was not stopped")
                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn(
                    f"error: leaf: Unity wrote nothing to its log for 1 s {phrase}, so the script stopped it; "
                    "see log/unity-tests/leaf-editmode.log",
                    completed.stderr,
                )
                self.assertFalse(self.results_file("leaf").exists())
                # The fake's own child, which taskkill /T does not reach on Git Bash, is gone too.
                sleeper = sleeper_file.read_text(encoding="utf-8").strip()
                alive = subprocess.run([str(bash_or_skip()), "-c", f"kill -0 {sleeper}"], capture_output=True, check=False)
                self.assertNotEqual(0, alive.returncode, f"sleep {sleeper} outlived the stopped run")

    def test_unity_that_keeps_logging_is_not_stopped(self) -> None:
        # 12 ticks 0.2 s apart run for 2.4 s, longer than the 2 s the log may stand still, and the
        # gap between two ticks may grow almost tenfold under load before it counts as a stall.
        completed = self.run_tests(
            "leaf", "--stall-timeout", "2", results=nunit_results(), ticks="12", tick_seconds="0.2"
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("fake unity tick 12", completed.stdout.splitlines())

    def test_stall_timeout_zero_waits_for_a_silent_unity(self) -> None:
        # Were 0 taken as a timeout of 0 s, the first look at the log that found no growth would
        # stop the fake, well within its 1 s of silence.
        completed = self.run_tests("leaf", "--stall-timeout", "0", results=nunit_results(), sleep="1")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("EditMode tests passed for leaf: 3 tests, 3 passed, 0 failed, 0 skipped, 0 inconclusive", completed.stdout)

    def test_stall_timeout_needs_whole_seconds(self) -> None:
        for value in ("", "1.5", "-1", "soon"):
            with self.subTest(value=value):
                completed = self.run_tests("leaf", "--stall-timeout", value, results=nunit_results())

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn("--stall-timeout expects a whole number of seconds", completed.stderr)
                self.assertNotIn("fake unity ran", completed.stdout)

    def test_run_without_results_names_the_test_it_was_in(self) -> None:
        completed = self.run_tests("leaf", unity_status=1, log=UTP_BEGIN)

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(
            "Unity exited with 1 and wrote no test results while running Fixture.Tests.Hangs; see its log above",
            completed.stderr,
        )

    def test_failed_test_fails_the_run_and_is_named(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(passed=2, failed=("Breaks",)), unity_status=2)

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive", completed.stderr)
        self.assertIn("  failed: Fixture.Tests.Breaks", completed.stderr)
        self.assertIn("EditMode tests failed for 1 target(s)", completed.stderr)

    def test_inconclusive_test_fails_the_run_and_is_named_in_the_summary(self) -> None:
        # The Unity Test Framework exits with 2 for an inconclusive test, as for a failed one; the
        # counts fail the target even when Unity does not.
        results = nunit_results(passed=3, skipped=1, inconclusive=("Waits", "Times"))
        for unity_status in (2, 0):
            with self.subTest(unity_status=unity_status):
                completed = self.run_tests("leaf", results=results, unity_status=unity_status)

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn("error: leaf: 6 tests, 3 passed, 0 failed, 1 skipped, 2 inconclusive", completed.stderr)
                self.assertIn("  inconclusive: Fixture.Tests.Waits", completed.stderr.splitlines())
                self.assertNotIn("although no test failed", completed.stderr)
                lines = completed.stdout.splitlines()
                summary = lines[lines.index("EditMode test summary:") + 1 :]
                self.assertEqual(
                    [
                        "  - leaf: FAILED, 6 tests, 3 passed, 0 failed, 1 skipped, 2 inconclusive",
                        "    inconclusive: Fixture.Tests.Waits",
                        "    inconclusive: Fixture.Tests.Times",
                    ],
                    summary[:3],
                )
                self.assertRegex(summary[3], OTHER_UNITY_PROCESSES)
                self.assertTrue(summary[4].startswith("Results: "), summary)

    def test_failed_and_inconclusive_tests_of_one_target_are_both_named(self) -> None:
        completed = self.run_tests(
            "leaf", results=nunit_results(passed=1, failed=("Breaks",), inconclusive=("Waits",)), unity_status=2
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("error: leaf: 3 tests, 1 passed, 1 failed, 0 skipped, 1 inconclusive", completed.stderr)
        self.assertIn("  failed: Fixture.Tests.Breaks", completed.stderr.splitlines())
        self.assertIn("  inconclusive: Fixture.Tests.Waits", completed.stderr.splitlines())
        self.assertIn("    inconclusive: Fixture.Tests.Waits", completed.stdout.splitlines())

    def test_a_fixture_whose_every_test_is_inconclusive_is_not_named_as_a_test(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(passed=0, inconclusive=("Waits",)), unity_status=2)

        self.assertEqual(1, completed.returncode, completed.stdout)
        named = [line for line in completed.stdout.splitlines() if "inconclusive: " in line]
        self.assertEqual(["    inconclusive: Fixture.Tests.Waits"], named)

    def test_failed_and_inconclusive_tests_are_named_as_the_tests_name_themselves(self) -> None:
        # The result file writes a test case's string argument with the XML references of an
        # attribute. An &amp; is read once, so the &quot; it leaves stays, and bash 5.2's
        # patsub_replacement would have put the whole &amp; back for the & it stands for.
        failed = ("Case(&quot;a &amp; b&quot;)", "Quote(&apos;&lt;T&gt;&apos;, &amp;quot;)")
        inconclusive = ("Text(&quot;caf&#xE9; &#233; &#x1F600;&quot;)", "Odd(&foo; &#0;)")
        completed = self.run_tests(
            "leaf", results=nunit_results(passed=1, failed=failed, inconclusive=inconclusive), unity_status=2
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        errors = completed.stderr.splitlines()
        start = errors.index("error: leaf: 5 tests, 1 passed, 2 failed, 0 skipped, 2 inconclusive")
        text = f'Fixture.Tests.Text("caf{chr(0xE9)} {chr(0xE9)} {chr(0x1F600)}")'
        self.assertEqual(
            [
                '  failed: Fixture.Tests.Case("a & b")',
                "  failed: Fixture.Tests.Quote('<T>', &quot;)",
                f"  inconclusive: {text}",
                "  inconclusive: Fixture.Tests.Odd(&foo; &#0;)",
            ],
            errors[start + 1 : start + 5],
        )
        self.assertEqual(
            [f"    inconclusive: {text}", "    inconclusive: Fixture.Tests.Odd(&foo; &#0;)"], self.summary(completed)[1:3]
        )

    def summary(self, completed: subprocess.CompletedProcess[str]) -> list[str]:
        lines = completed.stdout.splitlines()
        return lines[lines.index("EditMode test summary:") + 1 :]

    def test_a_target_whose_one_failure_the_end_of_the_data_store_service_caused_fails_on_the_environment(self) -> None:
        completed = self.run_tests(
            "leaf",
            results=nunit_results(passed=2, failed=("Breaks",), messages={"Breaks": FRAMEWORK_LOSS_FAILURE}),
            unity_status=2,
            log=data_store_loss_log("Fixture.Tests.Breaks"),
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        environment = (
            "  environment: Unity's Data Store service (pid 13480) ended while Fixture.Tests.Breaks ran, and the "
            "error Unity logged for it failed that test, so the test reached no verdict; run the target again"
        )
        errors = completed.stderr.splitlines()
        start = errors.index(
            "error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from the environment"
        )
        self.assertEqual(["  failed: Fixture.Tests.Breaks", environment], errors[start + 1 : start + 3])
        self.assertEqual(
            [
                "  - leaf: FAILED, 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from "
                "the environment",
                f"  {environment}",
            ],
            self.summary(completed)[:2],
        )
        self.assertIn(
            "error: EditMode tests failed for 1 target(s); 1 of them failed only on the environment, as the summary says",
            errors,
        )

    def test_a_test_that_the_result_file_names_with_character_references_fails_on_the_environment(self) -> None:
        # The result file writes the name with numeric character references, decimal and
        # hexadecimal, which the check reads as the failed: line does (#419); Unity's log writes
        # the characters themselves.
        failed = "Case(&#34;a &#38; b&#x3C;T&#x3E;&#x22;)"
        name = 'Fixture.Tests.Case("a & b<T>")'
        completed = self.run_tests(
            "leaf",
            results=nunit_results(passed=2, failed=(failed,), messages={failed: FRAMEWORK_LOSS_FAILURE}),
            unity_status=2,
            log=data_store_loss_log(name),
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        environment = (
            f"  environment: Unity's Data Store service (pid 13480) ended while {name} ran, and the error Unity "
            "logged for it failed that test, so the test reached no verdict; run the target again"
        )
        errors = completed.stderr.splitlines()
        start = errors.index(
            "error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from the environment"
        )
        self.assertEqual([f"  failed: {name}", environment], errors[start + 1 : start + 3])
        self.assertEqual(f"  {environment}", self.summary(completed)[1])

    def test_a_target_that_failed_otherwise_too_fails_on_its_counts_and_names_the_environment(self) -> None:
        environment = (
            "    environment: Unity's Data Store service (pid 13480) ended while Fixture.Tests.Breaks ran, and the "
            "error Unity logged for it failed that test, so the test reached no verdict; run the target again"
        )
        breaks = {"Breaks": TEARDOWN_LOSS_FAILURE}
        for results, counts in (
            (
                nunit_results(passed=1, failed=("Breaks", "Other"), messages=breaks),
                "3 tests, 1 passed, 2 failed, 0 skipped, 0 inconclusive",
            ),
            (
                nunit_results(passed=1, failed=("Breaks",), inconclusive=("Waits",), messages=breaks),
                "3 tests, 1 passed, 1 failed, 0 skipped, 1 inconclusive",
            ),
        ):
            with self.subTest(counts=counts):
                completed = self.run_tests(
                    "leaf", results=results, unity_status=2, log=data_store_loss_log("Fixture.Tests.Breaks")
                )

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn(f"error: leaf: {counts}", completed.stderr.splitlines())
                summary = self.summary(completed)
                self.assertEqual(f"  - leaf: FAILED, {counts}", summary[0])
                self.assertIn(environment, summary)
                self.assertNotIn("environment, as the summary says", completed.stderr)

    def test_an_end_of_the_data_store_service_that_failed_no_test_is_a_warning(self) -> None:
        after = "; Unity read the files it had imported in its own process from then on"
        own_failure = {"Breaks": "Expected: 1\n  But was:  2\n"}
        loss_failure = {"Breaks": FRAMEWORK_LOSS_FAILURE}
        for running, results, unity_status, warning in (
            # Between two tests, and during a test that let the error pass, the run passes.
            (None, nunit_results(passed=3), 0, "ended while no test ran"),
            ("Fixture.Tests.Passes", nunit_results(passed=3), 0, "ended while Fixture.Tests.Passes ran"),
            # A test that failed beside the one that was running failed on its own.
            (
                "Fixture.Tests.Passes",
                nunit_results(passed=2, failed=("Breaks",), messages=loss_failure),
                2,
                "ended while Fixture.Tests.Passes ran",
            ),
            # So did the test that was running when its failure message shows another failure, as
            # in a test that lets failing logs pass with LogAssert.ignoreFailingMessages, or none.
            (
                "Fixture.Tests.Breaks",
                nunit_results(passed=2, failed=("Breaks",), messages=own_failure),
                2,
                "ended while Fixture.Tests.Breaks ran",
            ),
            ("Fixture.Tests.Breaks", nunit_results(passed=2, failed=("Breaks",)), 2, "ended while Fixture.Tests.Breaks ran"),
        ):
            with self.subTest(running=running, results=results):
                completed = self.run_tests(
                    "leaf", results=results, unity_status=unity_status, log=data_store_loss_log(running)
                )

                self.assertEqual(1 if unity_status else 0, completed.returncode, completed.stdout)
                line = f"warning: Unity's Data Store service (pid 13480) {warning}{after}"
                self.assertIn(f"  {line}", completed.stderr.splitlines())
                self.assertEqual(f"    {line}", self.summary(completed)[1])
                self.assertNotIn("environment", completed.stdout + completed.stderr)

    def test_a_run_without_results_names_the_end_of_the_data_store_service_too(self) -> None:
        log = "\n".join([utp_status("Begin", "Fixture.Tests.Hangs"), DATA_STORE_LOSS])
        completed = self.run_tests("leaf", unity_status=1, log=log)

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertEqual(
            [
                "  - leaf: FAILED, Unity exited with 1 and wrote no test results while running Fixture.Tests.Hangs; see "
                "its log above",
                "    warning: Unity's Data Store service (pid 13480) ended while Fixture.Tests.Hangs ran; Unity read the "
                "files it had imported in its own process from then on",
            ],
            self.summary(completed)[:2],
        )

    def test_a_run_whose_unity_launched_the_data_store_service_keeps_the_service_s_log(self) -> None:
        # In an isolated mirror, which goes as the run ends, the log is kept before it goes.
        service_log = "Starting Unity Data Store Application\nShut down UDS service system complete"
        completed = self.run_tests(
            "leaf",
            "--isolated",
            results=nunit_results(passed=2, failed=("Breaks",), messages={"Breaks": FRAMEWORK_LOSS_FAILURE}),
            unity_status=2,
            log=f"{DATA_STORE_LAUNCH}\n{data_store_loss_log('Fixture.Tests.Breaks')}",
            service_log=service_log,
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("Testing leaf (ProjLeaf) in an isolated mirror because it was requested.", completed.stdout)
        self.assertEqual([], self.mirrors_left())
        kept = self.root / "log" / "unity-tests" / "leaf-editmode.uds-service.log"
        self.assertEqual(f"{service_log}\n", kept.read_text(encoding="utf-8"))
        # The fake is not a child of the script as Windows sees it, so the helper finds no service.
        line = (
            "data store service: pid 13480 has no end in log/unity-tests/leaf-editmode.load; its log is kept as "
            "log/unity-tests/leaf-editmode.uds-service.log"
        )
        self.assertIn(f"  {line}", completed.stderr.splitlines())
        self.assertEqual(f"    {line}", self.summary(completed)[2])

    def test_a_service_log_that_the_run_s_unity_did_not_launch_the_service_for_is_not_kept(self) -> None:
        self.addCleanup(shutil.rmtree, self.root / "log", ignore_errors=True)
        logs = self.root / "ProjLeaf" / "Logs"
        logs.mkdir()
        self.addCleanup(shutil.rmtree, logs)
        (logs / "UDS-Service.log").write_text("an earlier run's service\n", encoding="utf-8")
        kept = self.root / "log" / "unity-tests" / "leaf-editmode.uds-service.log"
        kept.parent.mkdir(parents=True)
        kept.write_text("kept by an earlier run\n", encoding="utf-8")

        completed = self.run_tests("leaf", results=nunit_results(passed=1), log=UTP_BEGIN)

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertFalse(kept.exists())
        self.assertEqual("an earlier run's service\n", (logs / "UDS-Service.log").read_text(encoding="utf-8"))
        self.assertNotIn("data store service:", completed.stdout + completed.stderr)

    def test_results_without_an_inconclusive_count_fail(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results().replace(' inconclusive="0"', ""))

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(
            "error: leaf: could not read the test counts in log/unity-tests/leaf-editmode.xml", completed.stderr
        )

    def test_run_without_results_fails(self) -> None:
        completed = self.run_tests("leaf", unity_status=1)

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("Unity exited with 1 and wrote no test results", completed.stderr)

    def test_result_of_an_earlier_run_does_not_count(self) -> None:
        self.results_file("leaf").parent.mkdir(parents=True)
        self.results_file("leaf").write_text(nunit_results(), encoding="utf-8")

        completed = self.run_tests("leaf")

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("wrote no test results", completed.stderr)

    def test_run_without_tests_fails(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(passed=0))

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("no tests ran although the project lists testables", completed.stderr)

    def test_unity_failure_fails_the_run_even_when_no_test_failed(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(), unity_status=3)

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("Unity exited with 3 although no test failed", completed.stderr)

    def test_project_without_testables_is_skipped(self) -> None:
        completed = self.run_tests("orphan", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("  - orphan: skipped, ProjOrphan/Packages/manifest.json lists no testables", completed.stdout)
        self.assertNotIn("fake unity ran", completed.stdout)

    def test_every_target_runs_after_one_fails(self) -> None:
        completed = self.run_tests("all", results=nunit_results(passed=1, failed=("Breaks",)), unity_status=2)

        self.assertEqual(1, completed.returncode, completed.stdout)
        ran = [line for line in completed.stdout.splitlines() if line.startswith("fake unity ran ")]
        self.assertEqual(2, len(ran), completed.stdout)
        self.assertIn("EditMode tests failed for 2 target(s)", completed.stderr)

    def test_results_outlive_the_isolated_mirror(self) -> None:
        completed = self.run_tests("leaf", "--isolated", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Testing leaf (ProjLeaf) in an isolated mirror because it was requested.", completed.stdout)
        self.assertTrue(self.results_file("leaf").is_file())
        self.assertEqual([], self.mirrors_left())

    def test_dry_run_shows_where_each_target_would_run(self) -> None:
        completed = self.run_tests("all", "--dry-run")

        self.assertEqual(0, completed.returncode, completed.stderr)
        lines = completed.stdout.splitlines()
        self.assertIn("Selected test targets:", lines)
        self.assertIn("  - shared: in place", lines)
        self.assertIn("  - layered: skipped, ProjLayered/Packages/manifest.json lists no testables", lines)
        self.assertNotIn("fake unity ran", completed.stdout)


def read_record(path: Path) -> dict[str, str]:
    """The key=value lines that the fixed-load helper writes beside a run's results."""
    return dict(line.split("=", 1) for line in path.read_text(encoding="utf-8").splitlines())


class FixedLoadRunTests(FixtureRepositoryTestCase):
    """Runs test-unity.sh with --load-spinners and --repeat; the fixture's spinners wait instead
    of spinning a core."""

    TESTABLE_PROJECTS = frozenset({"ProjShared", "ProjLeaf"})
    # The summary line that totals the other Unity processes of a target's runs.
    OTHERS_OVER_RUNS = r"^    other Unity processes: \d+ (at every count of|to \d+ over) the \d+ runs$"

    def run_tests(self, *arguments: str, results: str = "", unity_status: int = 0, **fake: str):
        """Run test-unity.sh; fake sets FAKE_UNITY_<NAME> variables, such as log, ticks, and run_file."""
        return self.run_entry_point(
            "test-unity.sh",
            *arguments,
            environment=self.unity_environment(
                FAKE_UNITY_RESULTS=results,
                FAKE_UNITY_STATUS=str(unity_status),
                **{f"FAKE_UNITY_{name.upper()}": value for name, value in fake.items()},
            ),
        )

    def results_dir(self) -> Path:
        return self.root / "log" / "unity-tests"

    def summary(self, completed: subprocess.CompletedProcess[str]) -> list[str]:
        lines = completed.stdout.splitlines()
        return lines[lines.index("EditMode test summary:") + 1 :]

    def assert_spinners_gone(self, record: dict[str, str]) -> list[int]:
        pids = [int(pid) for pid in record["spinner_pids"].split()]
        alive = [pid for pid in pids if fixed_load.process_running(pid)]
        self.assertEqual([], alive, "spinners outlived the run")
        self.assertEqual("", record["spinners_left"])
        return pids

    def test_the_load_starts_at_the_first_test_of_each_run_and_ends_with_it(self) -> None:
        # The fake logs the first test at once and keeps running for 0.6 s, the load's time.
        completed = self.run_tests(
            "leaf",
            "--load-spinners",
            "2",
            "--repeat",
            "2",
            results=nunit_results(passed=1),
            log=UTP_BEGIN,
            ticks="3",
            tick_seconds="0.2",
        )

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        lines = completed.stdout.splitlines()
        self.assertIn("Load: 2 spinners, from the first test of each run until its last test ends", lines)
        self.assertIn("Repeat: 2 runs of each target", lines)
        started: list[int] = []
        for run in (1, 2):
            with self.subTest(run=run):
                record = read_record(self.results_dir() / f"leaf-editmode-run{run}.load")
                self.assertEqual("started", record["load"])
                self.assertEqual("Fixture.Tests.Hangs", record["first_test"])
                self.assertEqual("0", record["tests_ended_before_load"])
                self.assertEqual("yes", record["complete"])
                self.assertEqual("", record["error"])
                pids = self.assert_spinners_gone(record)
                self.assertEqual(2, len(pids))
                started.extend(pids)
        # Each run starts spinners of its own, which wait while its Unity starts.
        self.assertEqual(4, len(set(started)))
        loads = [line for line in self.summary(completed) if line.startswith("    load: ")]
        self.assertEqual(2, len(loads), completed.stdout)
        # The fake's line dates the test to no time this clock can read, and names no plan.
        for line in loads:
            self.assertRegex(
                line, r"^    load: 2 spinners on \d+ logical CPUs for \d+\.\d s, from the first test until Unity exited$"
            )
        self.assertEqual([], list(self.results_dir().glob("*.load-stop")))

    def test_the_load_ends_with_the_last_test_the_plan_names(self) -> None:
        began = round(time.time() * 1000)
        plan = '##utp:{"type":"TestPlan","time":%d,"version":2,"phase":"Immediate","tests":["Fixture.Tests.Hangs"]}' % began
        begin = UTP_BEGIN.replace('"time":1,', f'"time":{began},')
        # The fake's only test lasts 0.6 s, after which its Unity takes 3 s more to exit.
        completed = self.run_tests(
            "leaf",
            "--load-spinners",
            "2",
            results=nunit_results(passed=1),
            log=f"{plan}\n{begin}",
            ticks="3",
            tick_seconds="0.2",
            log_after_ticks=UTP_END,
            sleep="3",
        )

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        record = read_record(self.results_dir() / "leaf-editmode.load")
        self.assertEqual(("started", "last-test"), (record["load"], record["load_ended"]))
        self.assertRegex(record["load_lag_ms"], r"^\d+$")
        self.assertLess(float(record["load_seconds"]), 2.5)
        self.assert_spinners_gone(record)
        [line] = [line for line in self.summary(completed) if line.startswith("    load: ")]
        self.assertRegex(
            line,
            r"^    load: 2 spinners on \d+ logical CPUs for \d+\.\d s, "
            r"from \d+ ms after the first test began until the last test ended$",
        )

    def test_a_run_whose_test_ended_before_the_load_started_fails(self) -> None:
        # The fake logs its only test as begun and ended at once, and exits.
        completed = self.run_tests(
            "leaf", "--load-spinners", "1", results=nunit_results(passed=1), log=f"{UTP_BEGIN}\n{UTP_END}"
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertRegex(
            completed.stderr,
            r"error: leaf: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive, but "
            r"(every test had ended before the load started|the run ended before the load could start)",
        )
        self.assert_spinners_gone(read_record(self.results_dir() / "leaf-editmode.load"))

    def test_a_run_in_which_no_test_began_fails_under_the_load(self) -> None:
        completed = self.run_tests("leaf", "--load-spinners", "1", results=nunit_results(passed=3))

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(
            "error: leaf: 3 tests, 3 passed, 0 failed, 0 skipped, 0 inconclusive, "
            "but no test began in Unity's log, so none ran with the load",
            completed.stderr,
        )
        self.assertIn("    load: none, no test began in Unity's log", self.summary(completed))
        record = read_record(self.results_dir() / "leaf-editmode.load")
        self.assertEqual("none", record["load"])
        self.assert_spinners_gone(record)

    def test_every_run_counts_the_other_unity_processes_without_a_load(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        summary = self.summary(completed)
        self.assertEqual("  - leaf: 3 tests, 3 passed, 0 failed, 0 skipped, 0 inconclusive", summary[0])
        self.assertRegex(summary[1], OTHER_UNITY_PROCESSES)
        self.assertEqual(
            "Results: log/unity-tests/<target>-editmode.xml, with Unity logs as <target>-editmode.log "
            "and load records as <target>-editmode.load",
            summary[2],
        )
        self.assertNotIn("load: ", completed.stdout)
        record = read_record(self.results_dir() / "leaf-editmode.load")
        self.assertEqual(("0", "", "yes"), (record["spinners"], record["spinner_pids"], record["complete"]))

    def test_without_the_helper_the_processes_go_uncounted_and_no_load_can_start(self) -> None:
        helper = self.root / LOAD_HELPER
        moved = helper.with_name("fixed_load.py.away")
        helper.rename(moved)
        self.addCleanup(moved.rename, helper)
        missing = "tools/cli/src/mu3_cli/fixed_load.py is missing"

        completed = self.run_tests("leaf", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(f"warning: other Unity processes will not be counted: {missing}", completed.stderr)
        self.assertIn(f"    other Unity processes: not counted, {missing}", self.summary(completed))
        self.assertNotIn("load records", completed.stdout)

        completed = self.run_tests("leaf", "--load-spinners", "2", results=nunit_results())

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(f"error: --load-spinners needs its helper, but {missing}", completed.stderr)
        self.assertIn("preflight failed; no tests were run", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)

    def test_the_options_need_whole_numbers_and_stop_before_unity_runs(self) -> None:
        for arguments, message in (
            (("--load-spinners",), "--load-spinners expects a whole number of spinners"),
            (("--load-spinners", "-1"), "--load-spinners expects a whole number of spinners"),
            (("--load-spinners", "1.5"), "--load-spinners expects a whole number of spinners"),
            (("--repeat", "0"), "--repeat expects a whole number of runs, 1 or more"),
            (("--repeat", "twice"), "--repeat expects a whole number of runs, 1 or more"),
            (("--repeat",), "--repeat expects a whole number of runs, 1 or more"),
        ):
            with self.subTest(arguments=arguments):
                completed = self.run_tests("leaf", *arguments, results=nunit_results())

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn(message, completed.stderr)
                self.assertNotIn("fake unity ran", completed.stdout)

    def test_dry_run_names_the_load_and_the_repeats(self) -> None:
        completed = self.run_tests("leaf", "--dry-run", "--load-spinners", "16", "--repeat", "3")

        self.assertEqual(0, completed.returncode, completed.stderr)
        lines = completed.stdout.splitlines()
        self.assertIn("Load: 16 spinners, from the first test of each run until its last test ends", lines)
        self.assertIn("Repeat: 3 runs of each target", lines)
        self.assertNotIn("fake unity ran", completed.stdout)

    @unittest.skipUnless(CAN_CHECK_LOCK_HOLDERS, "no way to check file holders on this host")
    def test_a_run_that_stops_the_script_before_unity_starts_still_stops_its_spinners(self) -> None:
        lock_file = self.root / "ProjLeaf" / "Temp" / "UnityLockfile"
        lock_file.parent.mkdir(parents=True)
        lock_file.write_text("", encoding="utf-8")
        self.addCleanup(shutil.rmtree, lock_file.parent)

        with lock_file.open("rb"):
            completed = self.run_tests("leaf", "--in-place", "--load-spinners", "2", results=nunit_results())

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("is open in Unity", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)
        record = read_record(self.results_dir() / "leaf-editmode.load")
        self.assertEqual(("none", "yes"), (record["load"], record["complete"]))
        self.assertEqual(2, len(self.assert_spinners_gone(record)))
        self.assertEqual([], list(self.results_dir().glob("*.load-stop")))

    def test_each_repeated_run_keeps_its_results_and_the_summary_totals_them(self) -> None:
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "4",
            run_file=str(Path(run_directory.name) / "runs"),
            results_1=nunit_results(passed=2),
            results_2=nunit_results(passed=1, failed=("Breaks",)),
            results_3=nunit_results(passed=1, failed=("Breaks",), inconclusive=("Waits",)),
            # The fourth run writes no results.
            results_4="",
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("EditMode tests failed in 3 of 4 runs", completed.stderr)
        self.assertIn("error: leaf run 2 of 4: 2 tests, 1 passed, 1 failed, 0 skipped, 0 inconclusive", completed.stderr)
        self.assertIn("Run 3 of 4 of leaf:", completed.stdout.splitlines())
        summary = [
            "<others>" if re.match(OTHER_UNITY_PROCESSES, line) else "<over runs>" if re.match(self.OTHERS_OVER_RUNS, line) else line
            for line in self.summary(completed)
        ]
        self.assertEqual(
            [
                "  - leaf run 1 of 4: 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                "<others>",
                "  - leaf run 2 of 4: FAILED, 2 tests, 1 passed, 1 failed, 0 skipped, 0 inconclusive",
                "<others>",
                "  - leaf run 3 of 4: FAILED, 3 tests, 1 passed, 1 failed, 0 skipped, 1 inconclusive",
                "    inconclusive: Fixture.Tests.Waits",
                "<others>",
                "  - leaf run 4 of 4: FAILED, Unity exited with 0 and wrote no test results; see its log above",
                "<others>",
                "  - leaf, 4 runs: 3 of 4 failed; 7 tests, 4 passed, 2 failed, 0 skipped, 1 inconclusive, "
                "in the 3 runs that wrote results",
                "    failed in 2 of 4 runs: Fixture.Tests.Breaks",
                "    inconclusive in 1 of 4 runs: Fixture.Tests.Waits",
                "<over runs>",
                "Results: log/unity-tests/<target>-editmode-run<k>.xml, with Unity logs as <target>-editmode-run<k>.log "
                "and load records as <target>-editmode-run<k>.load",
            ],
            summary,
        )
        for run in (1, 2, 3, 4):
            self.assertEqual(run < 4, (self.results_dir() / f"leaf-editmode-run{run}.xml").is_file())
            self.assertTrue((self.results_dir() / f"leaf-editmode-run{run}.log").is_file())
            self.assertEqual("yes", read_record(self.results_dir() / f"leaf-editmode-run{run}.load")["complete"])
        self.assertFalse((self.results_dir() / "leaf-editmode.xml").exists())

    def test_the_totals_count_the_runs_in_which_the_data_store_service_ended(self) -> None:
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        breaks = nunit_results(passed=1, failed=("Breaks",))
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "3",
            run_file=str(Path(run_directory.name) / "runs"),
            results_1=nunit_results(passed=1, failed=("Breaks",), messages={"Breaks": TEARDOWN_LOSS_FAILURE}),
            log_1=data_store_loss_log("Fixture.Tests.Breaks"),
            results_2=nunit_results(passed=2),
            log_2=data_store_loss_log(None),
            results_3=breaks,
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(
            "error: EditMode tests failed in 2 of 3 runs; 1 of them failed only on the environment, as the summary says",
            completed.stderr.splitlines(),
        )
        summary = [line for line in self.summary(completed) if not re.match(OTHER_UNITY_PROCESSES, line)]
        self.assertEqual(
            [
                "  - leaf run 1 of 3: FAILED, 2 tests, 1 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure "
                "came from the environment",
                "    environment: Unity's Data Store service (pid 13480) ended while Fixture.Tests.Breaks ran, and the "
                "error Unity logged for it failed that test, so the test reached no verdict; run the target again",
                "  - leaf run 2 of 3: 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                "    warning: Unity's Data Store service (pid 13480) ended while no test ran; Unity read the files it had "
                "imported in its own process from then on",
                "  - leaf run 3 of 3: FAILED, 2 tests, 1 passed, 1 failed, 0 skipped, 0 inconclusive",
                "  - leaf, 3 runs: 2 of 3 failed; 6 tests, 4 passed, 2 failed, 0 skipped, 0 inconclusive",
                "    failed in 2 of 3 runs: Fixture.Tests.Breaks",
                "    environment: Unity's Data Store service ended in 2 of 3 runs, and 1 of them failed on that alone",
            ],
            summary[: len(summary) - 2],
        )

    def test_results_that_an_earlier_repeat_left_are_removed(self) -> None:
        results_dir = self.results_dir()
        results_dir.mkdir(parents=True)
        stale = [results_dir / f"leaf-editmode-run3.{suffix}" for suffix in ("xml", "log", "load")]
        kept = [results_dir / "shared-editmode-run3.xml", results_dir / "leaf-editmode-run3.txt"]
        for path in stale + kept:
            path.write_text(nunit_results(), encoding="utf-8")

        completed = self.run_tests("leaf", "--repeat", "2", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual([], [path.name for path in stale if path.exists()])
        self.assertEqual(kept, [path for path in kept if path.exists()])
        self.assertTrue((results_dir / "leaf-editmode-run2.xml").is_file())
        self.assertIn(
            "  - leaf, 2 runs: 0 of 2 failed; 6 tests, 6 passed, 0 failed, 0 skipped, 0 inconclusive",
            self.summary(completed),
        )


def msys_pid(windows_pid: int) -> str:
    """The process ID Git Bash knows a Windows process by, which its kill takes."""
    listing = subprocess.run([str(bash_or_skip()), "-c", "ps"], capture_output=True, text=True, check=True).stdout
    for line in listing.splitlines()[1:]:
        fields = line.split()
        # Some lines start with a status letter before the PID, PPID, PGID, and WINPID.
        if fields and not fields[0].isdigit():
            fields = fields[1:]
        if len(fields) > 3 and fields[3] == str(windows_pid):
            return fields[0]
    raise AssertionError(f"Git Bash lists no process with the Windows ID {windows_pid}")


class InterruptedLoadRunTests(FixtureRepositoryTestCase):
    """Interrupts test-unity.sh while a fake Unity runs a test under the load, whose spinners wait
    instead of spinning a core, and checks that neither the helper nor a spinner outlives it."""

    TESTABLE_PROJECTS = frozenset({"ProjLeaf"})

    def start_loaded_run(self) -> tuple[subprocess.Popen[str], dict[str, str], Path]:
        """Starts a run whose fake Unity logs its first test and then stays silent for 60 s, and
        returns it once the helper has let its spinners spin, with the helper's record."""
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        sleeper_file = self.sleeper_file = Path(directory.name) / "sleeper.pid"
        output = (Path(directory.name) / "output.txt").open("w", encoding="utf-8")
        self.addCleanup(output.close)
        environment = self.unity_environment(
            FAKE_UNITY_RESULTS=nunit_results(passed=1),
            FAKE_UNITY_LOG=UTP_BEGIN,
            FAKE_UNITY_SLEEP="60",
            FAKE_UNITY_SLEEPER_FILE=str(sleeper_file),
        )
        script = subprocess.Popen(
            bash_script_command(self.root / "test-unity.sh", ["leaf", "--load-spinners", "2"]),
            cwd=self.root,
            env=environment,
            stdout=output,
            stderr=subprocess.STDOUT,
            text=True,
        )
        self.addCleanup(self.end_fake_unity, sleeper_file)
        self.addCleanup(self.end, script)

        record_file = self.root / "log" / "unity-tests" / "leaf-editmode.load"
        record: dict[str, str] = {}

        def spinning() -> bool:
            try:
                record.update(read_record(record_file))
            except (OSError, ValueError):
                return False
            return record.get("load") == "started"

        deadline = time.monotonic() + 60
        while not spinning() and script.poll() is None and time.monotonic() < deadline:
            time.sleep(0.05)
        self.assertEqual("started", record.get("load"), "the helper never let its spinners spin")
        return script, record, record_file

    @staticmethod
    def end(process: subprocess.Popen[str]) -> None:
        if process.poll() is None:
            process.kill()
        process.wait()

    @staticmethod
    def end_fake_unity(sleeper_file: Path) -> None:
        """Ends the fake Unity's silence, which a script killed outright leaves running."""
        if sleeper_file.is_file():
            sleeper = sleeper_file.read_text(encoding="utf-8").strip()
            subprocess.run([str(bash_or_skip()), "-c", f"kill {sleeper} 2>/dev/null"], check=False)

    def assert_helper_and_spinners_end(self, record: dict[str, str]) -> None:
        pids = [int(record["helper_pid"]), *map(int, record["spinner_pids"].split())]
        self.assertEqual(3, len(pids))
        deadline = time.monotonic() + 30
        while any(fixed_load.process_running(pid) for pid in pids) and time.monotonic() < deadline:
            time.sleep(0.1)
        self.assertEqual([], [pid for pid in pids if fixed_load.process_running(pid)], "outlived the script")

    def test_a_terminated_script_stops_unity_the_helper_and_the_spinners(self) -> None:
        script, record, record_file = self.start_loaded_run()
        root_pid = int(record["root_pid"])

        if platform.system() == "Windows":
            # A Windows signal would end bash outright; Git Bash's kill delivers SIGTERM, which
            # its trap handles.
            subprocess.run([str(bash_or_skip()), "-c", f"kill -TERM {msys_pid(root_pid)}"], check=True)
        else:
            os.kill(root_pid, signal.SIGTERM)

        # The trap that run_unity_logged sets stops Unity and exits with 130, and the exit stops the helper.
        self.assertEqual(130, script.wait(timeout=60))
        self.assert_helper_and_spinners_end(record)
        final = read_record(record_file)
        self.assertEqual(("yes", "run-end", ""), (final["complete"], final["load_ended"], final["error"]))
        self.assertEqual([], list(record_file.parent.glob("*.load-stop")))
        sleeper = self.sleeper_file.read_text(encoding="utf-8").strip()
        alive = subprocess.run([str(bash_or_skip()), "-c", f"kill -0 {sleeper}"], capture_output=True, check=False)
        self.assertNotEqual(0, alive.returncode, f"the fake Unity's sleep {sleeper} outlived the script")

    def test_a_script_killed_outright_leaves_neither_the_helper_nor_a_spinner(self) -> None:
        script, record, record_file = self.start_loaded_run()
        root_pid = int(record["root_pid"])

        # Neither a trap nor an exit runs: the helper sees the script end, and each spinner its
        # input close as the helper ends.
        if platform.system() == "Windows":
            subprocess.run(["taskkill", "/F", "/PID", str(root_pid)], capture_output=True, check=True)
        else:
            os.kill(root_pid, signal.SIGKILL)
        script.wait(timeout=60)

        self.assert_helper_and_spinners_end(record)
        final = read_record(record_file)
        self.assertEqual(("yes", "the script ended before it stopped the load"), (final["complete"], final["error"]))


class CompileLogTests(FixtureRepositoryTestCase):
    """compile-unity.sh keeps Unity's log and stops a Unity that stops writing to it."""

    def compile(self, *arguments: str, unity_status: int = 0, **fake: str):
        """Run compile-unity.sh; fake sets FAKE_UNITY_<NAME> variables, such as log, ticks, and sleep."""
        return self.run_entry_point(
            "compile-unity.sh",
            *arguments,
            environment=self.unity_environment(
                FAKE_UNITY_STATUS=str(unity_status),
                **{f"FAKE_UNITY_{name.upper()}": value for name, value in fake.items()},
            ),
        )

    def log_file(self, target: str) -> Path:
        return self.root / "log" / "unity-compile" / f"{target}.log"

    def test_unity_log_is_printed_and_kept(self) -> None:
        completed = self.compile("leaf", log="Compiling scripts")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout.splitlines())
        [arguments] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity arguments: ")]
        self.assertIn(" -quit ", arguments)
        self.assertIn("Compiling scripts", completed.stdout.splitlines())
        log = self.log_file("leaf").read_text(encoding="utf-8").splitlines()
        self.assertIn(arguments, log)
        self.assertIn("Compiling scripts", log)

    def test_silent_unity_is_stopped_and_fails_the_target(self) -> None:
        sleeper_directory = tempfile.TemporaryDirectory()
        self.addCleanup(sleeper_directory.cleanup)
        sleeper_file = Path(sleeper_directory.name) / "sleeper.pid"
        started = time.monotonic()
        completed = self.compile(
            "leaf", "--stall-timeout", "1", log="Compiling scripts", sleep="120", sleeper_file=str(sleeper_file)
        )

        self.assertLess(time.monotonic() - started, 60, "the silent Unity was not stopped")
        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(
            "error: Unity compilation failed for leaf: Unity wrote nothing to its log for 1 s, "
            "so the script stopped it; see log/unity-compile/leaf.log",
            completed.stderr,
        )
        self.assertNotIn("Unity compilation succeeded", completed.stdout)
        self.assertIn("Compiling scripts", self.log_file("leaf").read_text(encoding="utf-8").splitlines())
        # The fake's own child, which taskkill /T does not reach on Git Bash, is gone too.
        sleeper = sleeper_file.read_text(encoding="utf-8").strip()
        alive = subprocess.run([str(bash_or_skip()), "-c", f"kill -0 {sleeper}"], capture_output=True, check=False)
        self.assertNotEqual(0, alive.returncode, f"sleep {sleeper} outlived the stopped run")

    def test_unity_that_keeps_logging_is_not_stopped(self) -> None:
        # 12 ticks 0.2 s apart run for 2.4 s, longer than the 2 s the log may stand still, and the
        # gap between two ticks may grow almost tenfold under load before it counts as a stall.
        completed = self.compile("leaf", "--stall-timeout", "2", ticks="12", tick_seconds="0.2")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("fake unity tick 12", completed.stdout.splitlines())
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout.splitlines())

    def test_a_target_that_stalls_or_fails_ends_the_run_before_the_remaining_targets(self) -> None:
        # all compiles the five fixture targets in mapping order, and the fake stalls or fails in
        # each, so the first target ends the run and none of the other four may start.
        for fake, status, error in (
            (
                {"sleep": "120"},
                1,
                "error: Unity compilation failed for shared: Unity wrote nothing to its log for 1 s, "
                "so the script stopped it; see log/unity-compile/shared.log",
            ),
            ({"unity_status": 3}, 3, "error: Unity compilation failed for shared (exit 3)"),
        ):
            with self.subTest(error=error):
                completed = self.compile("all", "--stall-timeout", "1", **fake)

                self.assertEqual(status, completed.returncode, completed.stdout)
                self.assertIn(error, completed.stderr)
                ran = [line for line in completed.stdout.splitlines() if line.startswith("fake unity ran ")]
                self.assertEqual(1, len(ran), completed.stdout)
                self.assertTrue(ran[0].endswith("/ProjShared"), ran[0])
                self.assertNotIn("Compiling layered", completed.stdout)
                self.assertNotIn("Unity compilation succeeded", completed.stdout)


class UnfinishedTestTests(FixtureRepositoryTestCase):
    """unfinished_test reads the test a run was in from the lines -automated adds to the log."""

    def unfinished(self, *lines: str) -> str:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        log = Path(directory.name) / "unity.log"
        log.write_text("".join(f"{line}\n" for line in lines), encoding="utf-8")
        completed = self.run_functions("test-unity.sh", 'unfinished_test "$1"', str(log))
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_names_the_test_that_began_last_and_did_not_end(self) -> None:
        self.assertEqual("Fixture.Tests.Hangs\n", self.unfinished("Loading", UTP_BEGIN, "[NFR-1] timing"))

    def test_keeps_the_escaped_quotes_of_a_string_argument(self) -> None:
        begin = UTP_BEGIN.replace("Fixture.Tests.Hangs", 'Fixture.Tests.Case(\\"two \\\\ words\\")')

        self.assertEqual('Fixture.Tests.Case("two \\ words")\n', self.unfinished(begin))

    def test_names_nothing_once_the_last_test_ended_or_when_none_began(self) -> None:
        self.assertEqual("", self.unfinished(UTP_BEGIN, UTP_END))
        self.assertEqual("", self.unfinished("Loading"))


class DataStoreLossTests(FixtureRepositoryTestCase):
    """find_data_store_loss reads from Unity's log the process ID of its Data Store service, once the
    service ended, and the test that was running then."""

    def loss(self, *lines: str) -> tuple[str, str]:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        log = Path(directory.name) / "unity.log"
        log.write_text("".join(f"{line}\n" for line in lines), encoding="utf-8")
        completed = self.run_functions(
            "test-unity.sh",
            'find_data_store_loss "$1"\nprintf "%s|%s|%s" "$DATA_STORE_LOSS_PID" "$DATA_STORE_LOSS_TEST" "$DATA_STORE_LOSS_ERROR"',
            str(log),
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        pid, test, error = completed.stdout.split("|")
        self.assertEqual(DATA_STORE_LOSS.replace("13480", pid) if pid else "", error)
        return pid, test

    def test_names_the_service_and_the_test_that_had_begun_and_not_ended(self) -> None:
        self.assertEqual(("13480", "Fixture.Tests.Breaks"), self.loss(*data_store_loss_log("Fixture.Tests.Breaks").split("\n")))
        self.assertEqual(("13480", ""), self.loss(*data_store_loss_log(None).split("\n")))

    def test_keeps_the_escaped_quotes_of_a_string_argument(self) -> None:
        name = 'Fixture.Tests.Case("two \\ words")'

        self.assertEqual(("13480", name), self.loss(utp_status("Begin", name), DATA_STORE_LOSS))

    def test_takes_the_first_end_and_reads_it_from_its_log_entry_too(self) -> None:
        later = DATA_STORE_LOSS.replace("13480", "2")

        self.assertEqual(("13480", ""), self.loss("Loading", utp_error(DATA_STORE_LOSS), utp_error(later), later))

    def test_names_nothing_when_only_a_failure_message_quotes_the_error_or_no_service_ended(self) -> None:
        ended = utp_status("End", "Fixture.Tests.Quotes", f"Expected the log '[Error] {DATA_STORE_LOSS}'")

        self.assertEqual(("", ""), self.loss(utp_status("Begin", "Fixture.Tests.Quotes"), ended))
        self.assertEqual(("", ""), self.loss("Unity Data Store running and initialised", UTP_BEGIN, UTP_END))
        self.assertEqual(("", ""), self.loss("Unity Data Store service (pid x) is no longer running."))


class DataStoreLossFailureTests(FixtureRepositoryTestCase):
    """failed_only_on_data_store_loss tells, from the failure message in a result file, whether the test
    that was running as the Data Store service ended failed on that error alone."""

    def failed_only_on_it(self, results: str, test: str = "Fixture.Tests.Breaks", line_end: str = "\n") -> bool:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        path = Path(directory.name) / "results.xml"
        path.write_bytes(results.replace("\n", line_end).encode("utf-8"))
        completed = self.run_functions(
            "test-unity.sh",
            'DATA_STORE_LOSS_TEST="$2"\nDATA_STORE_LOSS_ERROR="$3"\nfailed_only_on_data_store_loss "$1"',
            str(path),
            test,
            DATA_STORE_LOSS,
        )
        self.assertIn(completed.returncode, (0, 1), completed.stderr)
        return completed.returncode == 0

    def results(self, message: str | None, name: str = "Breaks") -> str:
        return nunit_results(passed=1, failed=(name,), messages=None if message is None else {name: message})

    def test_a_message_of_that_error_alone_in_either_form_is_the_environment(self) -> None:
        for message in (
            FRAMEWORK_LOSS_FAILURE,
            f"TearDown : {FRAMEWORK_LOSS_FAILURE}",
            TEARDOWN_LOSS_FAILURE,
            TEARDOWN_LOSS_FAILURE.rstrip("\n"),
            # The error had no stack trace.
            f"TearDown : Unhandled log message: [Error] {DATA_STORE_LOSS}\nStack trace of the first:\n",
        ):
            for line_end in ("\n", "\r\n"):
                with self.subTest(message=message, line_end=line_end):
                    self.assertTrue(self.failed_only_on_it(self.results(message), line_end=line_end))

    def test_a_message_that_shows_another_failure_is_the_test_s_own(self) -> None:
        for message in (
            # An assertion, as in a test that let failing logs pass with LogAssert.ignoreFailingMessages.
            "Expected: 1\n  But was:  2\n",
            # An assertion first, and the error in the TearDown after it.
            f"Expected: 1\n  But was:  2\nTearDown : {FRAMEWORK_LOSS_FAILURE}",
            f"{FRAMEWORK_LOSS_FAILURE}\nTearDown : Expected: 1\n  But was:  2",
            # Another failing log beside the error, and a failure the TearDown adds after the stack trace.
            TEARDOWN_LOSS_FAILURE.replace(DATA_STORE_LOSS, f"{DATA_STORE_LOSS} | [Error] Something else broke", 1),
            f"{TEARDOWN_LOSS_FAILURE}\nThe test could not delete Assets/Mu3Library, which it created.",
            # The error of another service, and a message that only begins like the error.
            FRAMEWORK_LOSS_FAILURE.replace("13480", "2"),
            TEARDOWN_LOSS_FAILURE.replace("restarted.", "restarted. Then more"),
        ):
            with self.subTest(message=message):
                self.assertFalse(self.failed_only_on_it(self.results(message)))

    def test_a_test_without_a_message_or_that_did_not_fail_is_not_the_environment(self) -> None:
        self.assertFalse(self.failed_only_on_it(self.results(None)))
        self.assertFalse(self.failed_only_on_it(self.results(FRAMEWORK_LOSS_FAILURE, name="Other")))
        self.assertFalse(self.failed_only_on_it(nunit_results(passed=3)))

    def test_reads_the_name_of_a_string_argument_through_its_entities(self) -> None:
        results = nunit_results(passed=1, failed=("Case(&quot;a &amp; b \\ c&quot;)",), messages={
            "Case(&quot;a &amp; b \\ c&quot;)": FRAMEWORK_LOSS_FAILURE,
        })

        self.assertTrue(self.failed_only_on_it(results, test='Fixture.Tests.Case("a & b \\ c")'))
        self.assertFalse(self.failed_only_on_it(results, test="Fixture.Tests.Case(&quot;a &amp; b \\ c&quot;)"))

    def test_reads_the_name_through_its_numeric_character_references_as_the_failed_lines_do(self) -> None:
        name = "Text(&quot;caf&#xE9; &#233; &#x1F600; &#38;amp;&quot;)"
        results = nunit_results(passed=1, failed=(name,), messages={name: FRAMEWORK_LOSS_FAILURE})

        # Each reference is read once, so the &amp; that &#38; leaves stays.
        self.assertTrue(
            self.failed_only_on_it(results, test=f'Fixture.Tests.Text("caf{chr(0xE9)} {chr(0xE9)} {chr(0x1F600)} &amp;")')
        )
        self.assertFalse(
            self.failed_only_on_it(results, test='Fixture.Tests.Text("caf&#xE9; &#233; &#x1F600; &#38;amp;")')
        )


class DataStoreEndTests(FixtureRepositoryTestCase):
    """add_data_store_loss_to_summary gives, under the line about the end of the Data Store service, when
    the service ended and its exit code, as the helper recorded them, and the service's log the run kept."""

    ENVIRONMENT = (
        "  environment: Unity's Data Store service (pid 13480) ended while Fixture.Tests.Breaks ran, and the error "
        "Unity logged for it failed that test, so the test reached no verdict; run the target again"
    )

    def summary(self, record: str | None, kept_log: bool) -> list[str]:
        shutil.rmtree(self.root / "log", ignore_errors=True)
        self.addCleanup(shutil.rmtree, self.root / "log", ignore_errors=True)
        base = self.root / "log" / "unity-tests" / "leaf-editmode"
        base.parent.mkdir(parents=True)
        if record is not None:
            base.with_name("leaf-editmode.load").write_bytes(record.encode("utf-8"))
        if kept_log:
            base.with_name("leaf-editmode.uds-service.log").write_text("Shut down\n", encoding="utf-8")
        completed = self.run_functions(
            "test-unity.sh",
            "DATA_STORE_LOSS_PID=13480\nDATA_STORE_LOSS_TEST=Fixture.Tests.Breaks\n"
            'add_data_store_loss_to_summary 1 "$REPO_ROOT/log/unity-tests/leaf-editmode" 2>/dev/null\n'
            'printf "%s\\n" "${SUMMARY_LINES[@]}"',
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        lines = completed.stdout.splitlines()
        self.assertEqual(self.ENVIRONMENT, lines[0])
        return lines[1:]

    def test_gives_the_exit_code_and_the_end_the_helper_recorded_and_the_kept_log(self) -> None:
        record = (
            "data_store_pids=13470 13480\r\n"
            "data_store_13470_ended=2026-11-08T21:49:40.000+09:00\r\ndata_store_13470_exit_code=0\r\n"
            "data_store_13480_started=2026-11-08T21:49:54.120+09:00\r\n"
            "data_store_13480_ended=2026-11-08T21:50:10.502+09:00\r\ndata_store_13480_exit_code=3221225477\r\n"
            "complete=yes\r\n"
        )
        self.assertEqual(
            [
                "  data store service: pid 13480 exited with code 3221225477 at 2026-11-08T21:50:10.502+09:00; its log "
                "is kept as log/unity-tests/leaf-editmode.uds-service.log"
            ],
            self.summary(record, kept_log=True),
        )

    def test_says_what_the_helper_could_not_read(self) -> None:
        unread_code = "data_store_pids=13480\ndata_store_13480_ended=2026-11-08T21:50:10.502+09:00\ndata_store_13480_exit_code=\n"
        self.assertEqual(
            ["  data store service: pid 13480 ended at 2026-11-08T21:50:10.502+09:00 with an exit code that could not be read"],
            self.summary(unread_code, kept_log=False),
        )

    def test_names_a_kept_log_without_an_end_and_adds_nothing_without_either(self) -> None:
        for record, kept_log, lines in (
            (
                "data_store_pids=\n",
                True,
                [
                    "  data store service: pid 13480 has no end in log/unity-tests/leaf-editmode.load; its log is kept "
                    "as log/unity-tests/leaf-editmode.uds-service.log"
                ],
            ),
            ("data_store_pids=\n", False, []),
            # Without Python the helper does not run, so the run has no record to point at.
            (
                None,
                True,
                [
                    "  data store service: pid 13480 has no end recorded, since the run has no load record; its log "
                    "is kept as log/unity-tests/leaf-editmode.uds-service.log"
                ],
            ),
            (None, False, []),
        ):
            with self.subTest(record=record, kept_log=kept_log):
                self.assertEqual(lines, self.summary(record, kept_log))


class StallTimeoutDefaultTests(FixtureRepositoryTestCase):
    # Each entry point parses its own options first, as its main function does.
    PARSE = {
        "compile-unity.sh": 'parse_arguments "$@"',
        "test-unity.sh": 'parse_test_arguments "$@"\nparse_arguments ${COMPILE_ARGUMENTS[@]+"${COMPILE_ARGUMENTS[@]}"}',
    }

    def parse(self, entry_point: str, *arguments: str) -> subprocess.CompletedProcess[str]:
        return self.run_functions(entry_point, f'{self.PARSE[entry_point]}\nprintf %s "$UNITY_STALL_SECONDS"', *arguments)

    def stall_seconds(self, entry_point: str, *arguments: str) -> str:
        completed = self.parse(entry_point, *arguments)
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_a_compile_waits_300_seconds_and_a_test_run_600_unless_told_otherwise(self) -> None:
        self.assertEqual("300", self.stall_seconds("compile-unity.sh", "leaf"))
        self.assertEqual("600", self.stall_seconds("test-unity.sh", "leaf"))
        for entry_point in self.PARSE:
            with self.subTest(entry_point=entry_point):
                self.assertEqual("0", self.stall_seconds(entry_point, "leaf", "--stall-timeout", "0"))
                self.assertEqual("90", self.stall_seconds(entry_point, "leaf", "--stall-timeout", "090"))

    def test_compile_stall_timeout_needs_whole_seconds(self) -> None:
        for values in ((), ("1.5",), ("-1",), ("soon",)):
            with self.subTest(values=values):
                completed = self.parse("compile-unity.sh", "leaf", "--stall-timeout", *values)

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn("--stall-timeout expects a whole number of seconds", completed.stderr)

    def test_the_log_is_looked_at_every_second_unless_the_environment_says_otherwise(self) -> None:
        body = 'printf %s "$UNITY_LOG_POLL_SECONDS"'
        for entry_point in self.PARSE:
            with self.subTest(entry_point=entry_point):
                self.assertEqual("1", self.run_functions(entry_point, body).stdout)
                shorter = {**self._environment(), "UNITY_LOG_POLL_SECONDS": "0.25"}
                self.assertEqual("0.25", self.run_functions(entry_point, body, environment=shorter).stdout)

    def test_a_poll_interval_that_is_not_a_positive_number_stops_the_run_before_unity(self) -> None:
        for value in ("0", "0.0", "-1", "soon"):
            with self.subTest(value=value):
                completed = self.run_entry_point(
                    "compile-unity.sh", "leaf", environment=self.unity_environment(UNITY_LOG_POLL_SECONDS=value)
                )

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn(f"UNITY_LOG_POLL_SECONDS expects a positive number of seconds: {value}", completed.stderr)
                self.assertNotIn("fake unity ran", completed.stdout)


class StallClockTests(FixtureRepositoryTestCase):
    """The stall watch never stops Unity before the log has stood still for the whole timeout."""

    # Prints, for each reading of the clock after the log last grew at 5 s, whether the watch would stop Unity.
    JUDGE = """for now in "$@"; do
    UNITY_CLOCK="$now"
    if unity_log_stalled 5000000; then printf '%s stalled\\n' "$now"; else printf '%s waits\\n' "$now"; fi
done
"""

    def judge(self, setup: str, *readings: int) -> list[str]:
        completed = self.run_functions("compile-unity.sh", setup + self.JUDGE, *map(str, readings))
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout.splitlines()

    def test_a_clock_to_the_microsecond_stops_unity_once_the_timeout_has_passed(self) -> None:
        completed = self.run_functions(
            "compile-unity.sh", 'read_unity_clock\nprintf "%s %s" "$UNITY_CLOCK" "$UNITY_CLOCK_SLACK"'
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        clock, slack = map(int, completed.stdout.split())
        if slack:
            self.skipTest("this bash is older than 5 and has no EPOCHREALTIME")
        self.assertLess(abs(clock - time.time() * 1_000_000), 60_000_000, "the clock does not count microseconds")
        self.assertEqual(
            ["6999999 waits", "7000000 stalled"],
            self.judge("UNITY_STALL_SECONDS=2\nread_unity_clock\n", 6_999_999, 7_000_000),
        )

    def test_a_clock_in_whole_seconds_waits_a_second_more_than_the_timeout(self) -> None:
        # Readings 2 s apart in whole seconds may be little more than 1 s apart in fact.
        setup = "unset EPOCHREALTIME\nUNITY_STALL_SECONDS=2\nread_unity_clock\n"
        completed = self.run_functions(
            "compile-unity.sh", setup + 'printf "%s %s" "$((UNITY_CLOCK % 1000000))" "$UNITY_CLOCK_SLACK"'
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual("0 1000000", completed.stdout)
        self.assertEqual(["7000000 waits", "8000000 stalled"], self.judge(setup, 7_000_000, 8_000_000))

    def test_a_timeout_of_zero_never_stops_unity(self) -> None:
        self.assertEqual(
            ["5000000 waits", "99000000000 waits"],
            self.judge("UNITY_STALL_SECONDS=0\nread_unity_clock\n", 5_000_000, 99_000_000_000),
        )


class ProcessTreeTests(FixtureRepositoryTestCase):
    """The watchdog stops only the processes of its own run."""

    def test_descendants_stay_inside_the_process_group(self) -> None:
        # 200 still names 100 as its parent after 100's real child ended and the PID came back,
        # but it belongs to another session's group; 103 carries a Git Bash status letter.
        fake_ps = r"""ps() {
    if [[ "$1" == -e ]]; then
        printf '      PID    PPID    PGID     WINPID   TTY         UID    STIME COMMAND\n'
        printf '      100      10      50       1100  ?         197610 00:00:00 /unity\n'
        printf '      101     100      50       1101  ?         197610 00:00:00 /child\n'
        printf '      102     101      50       1102  ?         197610 00:00:00 /grandchild\n'
        printf 'I     103     100      50       1103  ?         197610 00:00:00 /waiting\n'
        printf '      200     100     300       1200  ?         197610 00:00:00 /stranger\n'
        printf '      201     200     300       1201  ?         197610 00:00:00 /stranger-child\n'
        printf '       60      10      50       1060  ?         197610 00:00:00 /sibling\n'
    else
        printf '%s\n' '  100    10    50' '  101   100    50' '  102   101    50' '  103   100    50' \
            '  200   100   300' '  201   200   300' '   60    10    50'
    fi
}
"""
        completed = self.run_functions("compile-unity.sh", fake_ps + "process_descendants 100 | sort")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(["101", "102", "103"], completed.stdout.split())


# What an older Editor bundles, laid out as an Editor keeps it beside its executable: the
# versions its package manifest recommends, and built-in copies with their own package.json.
# Its manifest names TextMeshPro without a version, so that version comes from the copy; the
# entry comes first, so reading on past its end would take the version of the entry after it.
FAKE_EDITOR_PACKAGES = {
    "Editor/manifest.json": {
        "schemaVersion": 4,
        "packages": {
            "com.unity.textmeshpro": {"minimumVersion": "5.0.0", "deprecated": "Included in com.unity.ugui."},
            "com.unity.ugui": {"minimumVersion": "2.0.0", "isDiscoverable": True, "version": "2.0.0"},
            "com.unity.inputsystem": {"minimumVersion": "1.19.0", "mustBeBundled": True, "version": "1.19.0"},
        },
    },
    "BuiltInPackages/com.unity.modules.audio/package.json": {"name": "com.unity.modules.audio", "version": "1.0.0"},
    "BuiltInPackages/com.unity.textmeshpro/package.json": {"name": "com.unity.textmeshpro", "version": "5.0.0"},
}
FLOOR_VERSION = "6000.3.17f1"


class EditorVersionTests(FixtureRepositoryTestCase):
    """--editor-version runs another Editor, which must only ever open a mirror set up for it."""

    TESTABLE_PROJECTS = frozenset({"ProjLeaf"})

    def fake_editor(self, with_packages: bool = True) -> Path:
        """A fake Unity executable, beside the bundled packages of an older Editor when asked."""
        editor_directory = tempfile.TemporaryDirectory()
        self.addCleanup(editor_directory.cleanup)
        executable = Path(editor_directory.name) / "Unity"
        executable.write_text(FAKE_UNITY, encoding="utf-8", newline="\n")
        executable.chmod(0o755)
        if with_packages:
            for relative, content in FAKE_EDITOR_PACKAGES.items():
                path = Path(editor_directory.name) / "Data" / "Resources" / "PackageManager" / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps(content, indent=2) + "\n", encoding="utf-8")
        return executable

    def give_leaf_unity_packages(self) -> tuple[Path, Path]:
        """Gives the leaf project Unity packages and a lock file until the test ends."""
        packages = self.root / "ProjLeaf" / "Packages"
        manifest, lock = packages / "manifest.json", packages / "packages-lock.json"
        original = manifest.read_bytes()
        self.addCleanup(manifest.write_bytes, original)
        self.addCleanup(lock.unlink, missing_ok=True)

        content = json.loads(original)
        # The module the older Editor lacks comes last, so dropping it must also drop a comma.
        content["dependencies"].update(
            {
                "com.unity.inputsystem": "1.20.1",
                "com.unity.terrain-tools": "5.3.3",
                "com.unity.textmeshpro": "3.0.6",
                "com.unity.ugui": "2.6.0",
                "com.unity.modules.audio": "1.0.0",
                "com.unity.modules.tetgen": "1.0.0",
            }
        )
        manifest.write_text(json.dumps(content, indent=2) + "\n", encoding="utf-8", newline="\r\n")
        lock.write_text("{}\n", encoding="utf-8")
        return manifest, lock

    def test_mirror_is_retargeted_to_the_requested_editor(self) -> None:
        manifest, lock = self.give_leaf_unity_packages()
        repository_manifest = manifest.read_bytes()

        completed = self.run_entry_point(
            "compile-unity.sh",
            "leaf",
            "--editor-version",
            FLOOR_VERSION,
            "--keep-staging",
            environment=self.unity_environment(UNITY_EDITOR=str(self.fake_editor())),
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(
            f"Compiling leaf (ProjLeaf) in an isolated mirror because --editor-version asks for Unity {FLOOR_VERSION}.",
            completed.stdout,
        )
        [mirror] = self.mirrors_left()
        project = mirror / "ProjLeaf"
        [ran] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity ran ")]
        self.assertIn(mirror.name, ran)
        # The manifest was written with CRLF, as a core.autocrlf checkout holds it, and every line
        # the retargeting writes back has dropped the CR.
        self.assertNotIn(b"\r", (project / "Packages" / "manifest.json").read_bytes())
        # The mirror holds the packages that manifest references, and no other.
        self.assertEqual(
            ["PkgLayered", "PkgLeaf", "PkgShared", "ProjLeaf"],
            sorted(path.name for path in mirror.iterdir() if path.is_dir()),
        )
        self.assertEqual(
            f"m_EditorVersion: {FLOOR_VERSION}\n",
            (project / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"),
        )
        self.assertFalse((project / "Packages" / "packages-lock.json").exists())
        self.assertEqual(
            {
                "fixture.pkgshared": "file:../../PkgShared",
                "fixture.pkglayered": "file:../../PkgLayered",
                "fixture.pkgleaf": "file:../../PkgLeaf",
                "com.unity.inputsystem": "1.19.0",
                "com.unity.terrain-tools": "5.3.3",
                "com.unity.textmeshpro": "5.0.0",
                "com.unity.ugui": "2.0.0",
                "com.unity.modules.audio": "1.0.0",
            },
            json.loads((project / "Packages" / "manifest.json").read_text(encoding="utf-8"))["dependencies"],
        )
        lines = completed.stdout.splitlines()
        self.assertIn("  - com.unity.ugui 2.6.0 -> 2.0.0", lines)
        self.assertIn(f"  - com.unity.modules.tetgen removed, Unity {FLOOR_VERSION} does not ship it", lines)
        self.assertIn(
            f"warning: Unity {FLOOR_VERSION} does not bundle com.unity.terrain-tools, so the mirror keeps 5.3.3",
            completed.stderr,
        )
        # The repository project keeps its own version, manifest, and lock file.
        self.assertEqual(repository_manifest, manifest.read_bytes())
        self.assertTrue(lock.is_file())
        self.assertEqual(
            "m_EditorVersion: 6000.6.7f1\n",
            (self.root / "ProjLeaf" / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"),
        )

    def test_every_target_of_both_entry_points_runs_in_a_mirror(self) -> None:
        for entry_point in ("compile-unity.sh", "test-unity.sh"):
            with self.subTest(entry_point=entry_point):
                completed = self.run_entry_point(entry_point, "all", "--editor-version", FLOOR_VERSION, "--dry-run")

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertIn(
                    f"  - leaf: isolated mirror, because --editor-version asks for Unity {FLOOR_VERSION}",
                    completed.stdout.splitlines(),
                )

    def test_in_place_is_refused(self) -> None:
        completed = self.run_entry_point("compile-unity.sh", "leaf", "--editor-version", FLOOR_VERSION, "--in-place")

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("cannot be combined with --in-place", completed.stderr)

    def test_version_must_name_a_full_release(self) -> None:
        for version in ("6000.3", "../6000.3.17f1", "6000.3.17f1/x"):
            with self.subTest(version=version):
                completed = self.run_entry_point("compile-unity.sh", "leaf", "--editor-version", version, "--dry-run")

                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn("--editor-version expects a full Unity version", completed.stderr)

    def test_hub_lookup_asks_for_the_requested_version(self) -> None:
        hub = tempfile.TemporaryDirectory()
        self.addCleanup(hub.cleanup)
        environment = self.unity_environment(UNITY_HUB_EDITOR_ROOT=hub.name)
        del environment["UNITY_EDITOR"]

        completed = self.run_entry_point("compile-unity.sh", "leaf", "--editor-version", FLOOR_VERSION, environment=environment)

        self.assertEqual(1, completed.returncode, completed.stdout)
        [missing] = [line for line in completed.stderr.splitlines() if "is not installed" in line]
        self.assertIn("for --editor-version:", missing)
        self.assertIn(f"/{FLOOR_VERSION}/", missing)
        self.assertNotIn("6000.6.7f1", missing)

    def test_editor_without_bundled_packages_stops_before_unity_runs(self) -> None:
        completed = self.run_entry_point(
            "compile-unity.sh",
            "leaf",
            "--editor-version",
            FLOOR_VERSION,
            environment=self.unity_environment(UNITY_EDITOR=str(self.fake_editor(with_packages=False))),
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(f"cannot find the packages that Unity {FLOOR_VERSION} bundles", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)
        self.assertEqual([], self.mirrors_left())


# Where Unity Hub keeps an Editor's executable inside the folder of its version.
HUB_EXECUTABLE = {"Windows": "Editor/Unity.exe", "Darwin": "Unity.app/Contents/MacOS/Unity"}.get(
    platform.system(), "Editor/Unity"
)


class CrlfCheckoutTests(FixtureRepositoryTestCase):
    """A checkout with core.autocrlf holds each project's ProjectVersion.txt and Packages/manifest.json
    with CRLF, and every line the entry points read from them must drop the CR.

    A CR left on a line would change the Editor version the scripts look for. The manifest readers
    match part of a line, which a trailing CR cannot change, so these tests check their results.
    EditorVersionTests retargets a mirror from a manifest written with CRLF.
    """

    TESTABLE_PROJECTS = frozenset({"ProjLeaf"})

    def setUp(self) -> None:
        super().setUp()
        version_file = self.root / "ProjLeaf" / "ProjectSettings" / "ProjectVersion.txt"
        self.addCleanup(version_file.write_bytes, version_file.read_bytes())
        version_file.write_bytes(
            b"m_EditorVersion: 6000.6.7f1\r\nm_EditorVersionWithRevision: 6000.6.7f1 (0123456789ab)\r\n"
        )
        # As bytes, so that every manifest holds CRLF on every OS.
        for _, _, project in MAPPING:
            manifest = self.root / project / "Packages" / "manifest.json"
            content = manifest.read_bytes()
            self.addCleanup(manifest.write_bytes, content)
            manifest.write_bytes(content.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n"))

    def hub_editor(self) -> tuple[Path, Path]:
        """A Unity Hub folder holding a fake of the Editor the fixture asks for, and its executable."""
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        # Hub names the folder Editor, which the scripts look for at the end of an override's path.
        hub = Path(directory.name) / "Hub" / "Editor"
        executable = hub / "6000.6.7f1" / HUB_EXECUTABLE
        executable.parent.mkdir(parents=True)
        executable.write_text(FAKE_UNITY, encoding="utf-8", newline="\n")
        executable.chmod(0o755)
        return hub, executable

    def test_the_editor_version_has_no_cr(self) -> None:
        completed = self.run_functions(
            "compile-unity.sh", 'project_editor_version ProjLeaf\nprintf "[%s]" "$PROJECT_EDITOR_VERSION"'
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual("[6000.6.7f1]", completed.stdout)

    def test_the_hub_editor_compiles_the_project_without_an_override(self) -> None:
        hub, _ = self.hub_editor()
        environment = self.unity_environment(UNITY_HUB_EDITOR_ROOT=str(hub))
        del environment["UNITY_EDITOR"]

        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=environment)

        self.assertEqual(0, completed.returncode, completed.stderr)
        [unity] = [line for line in completed.stdout.splitlines() if line.startswith("Unity: ")]
        self.assertTrue(unity.endswith(f"/Hub/Editor/6000.6.7f1/{HUB_EXECUTABLE}"), unity)
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout.splitlines())

    def test_each_package_compiles_in_the_projects_whose_manifest_references_it(self) -> None:
        completed = self.run_functions(
            "compile-unity.sh",
            'load_package_config\nload_package_consumers\nprintf "%s\\n" "${PACKAGE_CONSUMER_TARGETS[@]}"',
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(["shared layered leaf", "layered leaf", "leaf", "extra", "orphan"], completed.stdout.splitlines())

    def test_only_the_manifest_that_lists_testables_has_tests_to_run(self) -> None:
        completed = self.run_entry_point("test-unity.sh", "all", "--dry-run")

        self.assertEqual(0, completed.returncode, completed.stderr)
        plan = run_plan(completed.stdout)
        self.assertEqual("in place", plan["leaf"])
        for key, _, project in MAPPING:
            if key != "leaf":
                self.assertEqual(f"skipped, {project}/Packages/manifest.json lists no testables", plan[key])

    def test_the_hub_editor_tests_the_project_without_an_override(self) -> None:
        hub, _ = self.hub_editor()
        environment = self.unity_environment(UNITY_HUB_EDITOR_ROOT=str(hub), FAKE_UNITY_RESULTS=nunit_results())
        del environment["UNITY_EDITOR"]

        completed = self.run_entry_point("test-unity.sh", "leaf", environment=environment)

        self.assertEqual(0, completed.returncode, completed.stderr)
        [unity] = [line for line in completed.stdout.splitlines() if line.startswith("Unity: ")]
        self.assertTrue(unity.endswith(f"/Hub/Editor/6000.6.7f1/{HUB_EXECUTABLE}"), unity)
        self.assertIn(
            "EditMode tests passed for leaf: 3 tests, 3 passed, 0 failed, 0 skipped, 0 inconclusive", completed.stdout.splitlines()
        )

    def test_an_override_that_is_the_hub_editor_does_not_warn(self) -> None:
        _, executable = self.hub_editor()

        completed = self.run_entry_point(
            "compile-unity.sh", "leaf", environment=self.unity_environment(UNITY_EDITOR=executable.as_posix())
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertNotIn("but the explicit override will be used", completed.stderr)
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout.splitlines())


class OverrideWarningTests(FixtureRepositoryTestCase):
    """An explicit Editor that is not the one a target asks for is warned about once per target, by
    the preflight, before any Unity starts; the run of the target does not repeat it."""

    TESTABLE_PROJECTS = frozenset({"ProjShared", "ProjLeaf"})

    def test_each_target_is_warned_about_once_before_unity_starts(self) -> None:
        runs = (
            ("compile-unity.sh", "compile_main", [project for _, _, project in MAPPING]),
            ("test-unity.sh", "test_main", ["ProjShared", "ProjLeaf"]),
        )
        for entry_point, main, projects in runs:
            with self.subTest(entry_point=entry_point):
                # One stream keeps the warnings in order with the runs they come before.
                completed = self.run_functions(
                    entry_point,
                    f'{main} "$@" 2>&1',
                    "all",
                    environment=self.unity_environment(FAKE_UNITY_RESULTS=nunit_results()),
                )

                self.assertEqual(0, completed.returncode, completed.stdout)
                lines = completed.stdout.splitlines()
                warnings = [
                    index for index, line in enumerate(lines) if "but the explicit override will be used" in line
                ]
                self.assertEqual(
                    [
                        f"warning: {project} requires Unity 6000.6.7f1 but the explicit override will be used: "
                        f"{self.fake_unity}"
                        for project in projects
                    ],
                    [lines[index] for index in warnings],
                )
                runs_of_unity = [index for index, line in enumerate(lines) if line.startswith("fake unity ran ")]
                self.assertEqual(len(projects), len(runs_of_unity), completed.stdout)
                self.assertLess(warnings[-1], runs_of_unity[0], completed.stdout)


class OutsideWorkingDirectoryTests(FixtureRepositoryTestCase):
    """Each entry point finds the repository from its own path, never from the working directory, so
    it runs the fixture's targets from a folder outside it, by every absolute spelling of that path.

    A script that cut its own folder wrong would fall back to the working directory, which the other
    tests keep at the fixture's root, where the result would look the same."""

    TESTABLE_PROJECTS = frozenset({"ProjLeaf"})

    def setUp(self) -> None:
        super().setUp()
        outside = tempfile.TemporaryDirectory()
        self.addCleanup(outside.cleanup)
        self.outside = Path(outside.name)

    def spellings(self, entry_point: str) -> dict[str, str]:
        """The absolute spellings of an entry point's path that callers on this host use."""
        path = self.root / entry_point
        spellings = {"native": str(path)}
        if platform.system() == "Windows":
            # The native spelling is the Windows one, with backslashes.
            spellings["Windows with forward slashes"] = path.as_posix()
            spellings["POSIX"] = f"/{path.drive[0].lower()}{path.as_posix()[len(path.drive):]}"
        return spellings

    def test_both_entry_points_run_the_fixture_targets_from_outside_the_repository(self) -> None:
        runs = (
            ("compile-unity.sh", "Unity compilation succeeded: leaf", self.root / "log" / "unity-compile" / "leaf.log"),
            ("test-unity.sh", "EditMode tests passed for leaf: 3 tests, 3 passed, 0 failed, 0 skipped, 0 inconclusive",
             self.root / "log" / "unity-tests" / "leaf-editmode.xml"),
        )
        for entry_point, success, kept in runs:
            for spelling, script in self.spellings(entry_point).items():
                with self.subTest(entry_point=entry_point, spelling=spelling):
                    shutil.rmtree(self.root / "log", ignore_errors=True)

                    completed = self.run_entry_point(
                        entry_point,
                        "leaf",
                        environment=self.unity_environment(FAKE_UNITY_RESULTS=nunit_results()),
                        cwd=self.outside,
                        script=script,
                    )

                    self.assertEqual(0, completed.returncode, completed.stderr)
                    self.assertIn(success, completed.stdout.splitlines())
                    [ran] = [line for line in completed.stdout.splitlines() if line.startswith("fake unity ran ")]
                    self.assertTrue(ran.endswith("/repository/ProjLeaf"), ran)
                    self.assertTrue(kept.is_file(), completed.stdout)
                    self.assertEqual([], list(self.outside.iterdir()))

    def test_sourced_entry_points_find_the_repository_from_outside_it(self) -> None:
        body = 'load_package_config\nprintf "root %s\\ntargets %s\\n" "$REPO_ROOT" "${TARGET_KEYS[*]}"'
        for entry_point in ("compile-unity.sh", "test-unity.sh"):
            for spelling, script in self.spellings(entry_point).items():
                with self.subTest(entry_point=entry_point, spelling=spelling):
                    completed = self.run_functions(entry_point, body, cwd=self.outside, script=script)

                    self.assertEqual(0, completed.returncode, completed.stderr)
                    root, targets = completed.stdout.splitlines()
                    self.assertTrue(root.endswith("/repository"), root)
                    self.assertEqual(f"targets {' '.join(key for key, _, _ in MAPPING)}", targets)


def package_cache_path(length: int, name: str) -> str:
    """A Windows path of a package file of exactly length characters whose file name is name."""
    head = "C:\\Deep\\Project\\Library\\PackageCache\\com.unity.example@0123456789ab\\"
    return head + "d" * (length - len(head) - len(name) - 1) + "\\" + name


def unreadable(path: str, asset: str | None = None) -> str:
    """The line Unity logs when it cannot find a file, while it imports asset when one is given."""
    message = f'DirectoryNotFoundException: Could not find a part of the path "{path}"'
    return f'Asset import failed, "{asset}" > {message}' if asset else message


def utp_error(line: str) -> str:
    """The line -automated adds to Unity's log for the error that Unity logged as line."""
    return f'##utp:{{"type":"LogEntry","version":2,"phase":"Immediate","severity":"Error","message":{json.dumps(line)}}}'


class UnreadableLongPathTests(FixtureRepositoryTestCase):
    """find_unreadable_long_paths counts the files Unity's log says it could not find although their
    paths only reached the Windows limit, and the asset imports that failed on them.

    The lines take the forms of a first import from a deep worktree with Unity 6000.6.7f1 (#382)."""

    def warning(self, *lines: str, host: str = "windows") -> str:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.log = Path(directory.name) / "unity.log"
        self.log.write_text("".join(f"{line}\n" for line in lines), encoding="utf-8")
        completed = self.run_functions(
            "compile-unity.sh",
            'HOST_PLATFORM="$2"\nfind_unreadable_long_paths "$1"\nprintf %s "$UNREADABLE_PATHS_WARNING"',
            str(self.log),
            host,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_counts_each_file_and_each_failed_import_once_and_names_the_dlls(self) -> None:
        style = package_cache_path(260, "Window.uss")
        layout = package_cache_path(261, "Window.uxml")
        plugin = package_cache_path(300, "Plugin.dll")
        actions = package_cache_path(270, "Default.inputactions")
        first_import = unreadable(style, asset="Packages/com.unity.example/Window.uxml")

        warning = self.warning(
            first_import,
            utp_error(first_import),
            "  at System.IO.FileStream..ctor (System.String path) [0x0019e] in <a8858c8db>:0",
            # The importer reads the style sheet again, and another import reads the layout.
            first_import,
            unreadable(layout, asset="Packages/com.unity.example/Other.uxml"),
            # The API updater reads a plugin twice, outside any import.
            unreadable(plugin),
            unreadable(plugin),
            f"Could not read file 'Packages/com.unity.example/Default.inputactions' (System.IO.{unreadable(actions)})",
        )

        self.assertEqual(
            "Unity could not read 4 file(s) whose paths have 260 characters or more, among them Plugin.dll, "
            f"and 2 asset import(s) failed on them; see {self.log}",
            warning,
        )

    def test_a_path_short_of_the_limit_is_left_out(self) -> None:
        warning = self.warning(
            unreadable(package_cache_path(259, "Missing.uxml"), asset="Packages/com.unity.example/Missing.uxml"),
            unreadable(package_cache_path(260, "Window.uss")),
        )

        self.assertEqual(f"Unity could not read 1 file(s) whose paths have 260 characters or more; see {self.log}", warning)

    def test_a_log_without_such_a_file_gives_no_warning(self) -> None:
        self.assertEqual("", self.warning("Loading", utp_error(unreadable(package_cache_path(280, "Window.uss")))))
        self.assertEqual("", self.warning(unreadable("C:\\Project\\Assets\\Missing.txt")))

    def test_another_host_is_not_warned(self) -> None:
        # Only Windows has the limit, so a long path missing elsewhere has another cause.
        line = unreadable(package_cache_path(280, "Window.uss"), asset="Packages/com.unity.example/Window.uxml")

        self.assertTrue(self.warning(line).startswith("Unity could not read 1 file(s) "))
        self.assertEqual("", self.warning(line, host="linux"))
        self.assertEqual("", self.warning(line, host="macos"))


class ProjectPathLengthTests(FixtureRepositoryTestCase):
    """check_project_path_length warns on Windows when the longest path measured below a project, 161
    characters, reaches the 260-character limit in the folder Unity opens."""

    def warning(self, length: int, location: str = "in-place", host: str = "windows") -> str:
        # On Windows /c/... becomes C:\... of the same length; elsewhere it stays as it is.
        folder = "/c/" + "p" * (length - 3)
        completed = self.run_functions(
            "compile-unity.sh",
            'HOST_PLATFORM="$3"\ncheck_project_path_length "$1" "$2"\nprintf %s "$PATH_LIMIT_WARNING"',
            folder,
            location,
            host,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_a_folder_that_pushes_the_longest_package_path_to_the_limit_is_warned_about(self) -> None:
        self.assertEqual("", self.warning(97))

        warning = self.warning(98)

        self.assertIn(" is 98 characters, so the longest package file path measured below a project reaches 260 there", warning)
        self.assertIn("Unity cannot import a file whose path has 260 characters or more on Windows", warning)
        self.assertTrue(
            warning.endswith(
                "run from a project path of at most 97 characters, such as on a drive letter that subst maps to "
                "the checkout (tools/README.md)"
            ),
            warning,
        )

    def test_a_mirror_is_told_to_take_a_shorter_tmpdir(self) -> None:
        warning = self.warning(120, location="isolated")

        self.assertTrue(warning.startswith("the mirror path "), warning)
        self.assertIn(" is 120 characters, so the longest package file path measured below a project reaches 282 there", warning)
        self.assertTrue(
            warning.endswith("set TMPDIR so that the mirror path has at most 97 characters (tools/README.md)"), warning
        )

    def test_another_host_is_not_warned(self) -> None:
        self.assertEqual("", self.warning(200, host="linux"))
        self.assertEqual("", self.warning(200, host="macos"))


@unittest.skipUnless(platform.system() == "Windows", "only Windows limits the paths Unity's asset import reads")
class UnreadableLongPathRunTests(FixtureRepositoryTestCase):
    """Both entry points warn when Unity's log names a file it could not read past the path limit."""

    TESTABLE_PROJECTS = frozenset({"ProjShared", "ProjLeaf"})
    LOG = unreadable(package_cache_path(270, "Window.uss"))

    def test_compile_warns_after_each_target_and_again_at_the_end_of_a_run_of_several(self) -> None:
        for target, keys in (("leaf", ["leaf"]), ("all", [key for key, _, _ in MAPPING])):
            with self.subTest(target=target):
                # One stream keeps the warnings in order with the runs they follow.
                completed = self.run_functions(
                    "compile-unity.sh",
                    'compile_main "$@" 2>&1',
                    target,
                    environment=self.unity_environment(FAKE_UNITY_LOG=self.LOG),
                )

                self.assertEqual(0, completed.returncode, completed.stdout)
                lines = completed.stdout.splitlines()
                warnings = [
                    f"warning: {key}: Unity could not read 1 file(s) whose paths have 260 characters or more; "
                    f"see log/unity-compile/{key}.log"
                    for key in keys
                ]
                for key, warning in zip(keys, warnings):
                    self.assertEqual(warning, lines[lines.index(f"Unity compilation succeeded: {key}") - 1])
                if len(keys) > 1:
                    self.assertEqual(warnings, lines[-len(keys):])
                else:
                    self.assertEqual(1, lines.count(warnings[0]), completed.stdout)

    def test_a_failed_compile_ends_the_run_after_its_own_warning_without_repeating_it(self) -> None:
        completed = self.run_functions(
            "compile-unity.sh",
            'compile_main "$@" 2>&1',
            "all",
            environment=self.unity_environment(FAKE_UNITY_LOG=self.LOG, FAKE_UNITY_STATUS="3"),
        )

        self.assertEqual(3, completed.returncode, completed.stdout)
        lines = completed.stdout.splitlines()
        warning = (
            "warning: shared: Unity could not read 1 file(s) whose paths have 260 characters or more; "
            "see log/unity-compile/shared.log"
        )
        self.assertEqual(
            [warning, "error: Unity compilation failed for shared (exit 3)"], lines[-2:], completed.stdout
        )
        self.assertEqual(1, lines.count(warning), completed.stdout)

    def test_test_summary_gives_the_warning_under_its_target(self) -> None:
        warning = (
            "    warning: Unity could not read 1 file(s) whose paths have 260 characters or more; "
            "see log/unity-tests/leaf-editmode.log"
        )
        for results, status, target_line in (
            (nunit_results(), 0, "  - leaf: 3 tests, 3 passed, 0 failed, 0 skipped, 0 inconclusive"),
            # A plugin that does not import can break the compile, after which no test runs.
            ("", 1, "  - leaf: FAILED, Unity exited with 1 and wrote no test results; see its log above"),
        ):
            with self.subTest(status=status):
                completed = self.run_entry_point(
                    "test-unity.sh",
                    "leaf",
                    environment=self.unity_environment(
                        FAKE_UNITY_LOG=self.LOG, FAKE_UNITY_RESULTS=results, FAKE_UNITY_STATUS=str(status)
                    ),
                )

                self.assertEqual(status, completed.returncode, completed.stdout)
                lines = completed.stdout.splitlines()
                self.assertEqual(warning, lines[lines.index(target_line) + 1], completed.stdout)
                self.assertIn(warning.strip().replace("warning: ", "warning: leaf: ", 1), completed.stderr)


@unittest.skipUnless(platform.system() == "Windows", "only Windows limits the paths Unity's asset import reads")
class DeepCheckoutTests(FixtureRepositoryTestCase):
    """A checkout deep enough that the longest package path measured below a project reaches the
    Windows limit is warned about before Unity starts, in place and in a mirror."""

    TESTABLE_PROJECTS = frozenset({"ProjLeaf"})
    # Long enough that the project folder passes 97 characters below any temporary directory.
    ROOT_NAME = "repository-" + "deep" * 22

    def test_both_entry_points_warn_before_unity_starts(self) -> None:
        for entry_point, main in (("compile-unity.sh", "compile_main"), ("test-unity.sh", "test_main")):
            with self.subTest(entry_point=entry_point):
                completed = self.run_functions(
                    entry_point,
                    f'{main} "$@" 2>&1',
                    "leaf",
                    environment=self.unity_environment(FAKE_UNITY_RESULTS=nunit_results()),
                )

                self.assertEqual(0, completed.returncode, completed.stdout)
                lines = completed.stdout.splitlines()
                [warning] = [index for index, line in enumerate(lines) if line.startswith("warning: leaf: the project path ")]
                self.assertRegex(lines[warning], r"\\ProjLeaf is \d+ characters, so the longest package file path")
                self.assertIn("at most 97 characters, such as on a drive letter that subst maps", lines[warning])
                [ran] = [index for index, line in enumerate(lines) if line.startswith("fake unity ran ")]
                self.assertLess(warning, ran, completed.stdout)
                if entry_point == "test-unity.sh":
                    self.assertIn(f"    {lines[warning].replace('warning: leaf: ', 'warning: ', 1)}", lines)

    def test_a_mirror_in_a_deep_tmpdir_is_warned_about_and_told_to_shorten_it(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        deep_tmpdir = Path(directory.name) / ("t" * 90)
        deep_tmpdir.mkdir()

        completed = self.run_entry_point(
            "compile-unity.sh", "leaf", "--isolated", environment=self.unity_environment(TMPDIR=str(deep_tmpdir))
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        [warning] = [line for line in completed.stderr.splitlines() if line.startswith("warning: leaf: ")]
        self.assertIn("the mirror path ", warning)
        self.assertIn("mu3library-unity-compile.", warning)
        self.assertTrue(warning.endswith("set TMPDIR so that the mirror path has at most 97 characters (tools/README.md)"))
        self.assertNotIn("the project path ", completed.stderr)


if __name__ == "__main__":
    unittest.main()
