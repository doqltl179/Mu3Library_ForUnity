from __future__ import annotations

import json
import shutil
import subprocess
import tempfile
import time
import unittest
from pathlib import Path

from fixture_repository import FAKE_UNITY, MAPPING, FixtureRepositoryTestCase, repository_ignore_line
from shell_harness import bash_or_skip
from test_compile_selection import CAN_CHECK_LOCK_HOLDERS
from mu3_cli.repository import repo_root
from mu3_cli.unity import is_directory_link, link_samples, load_target_specs, remove_directory_link

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


def nunit_results(passed: int = 3, failed: tuple[str, ...] = (), skipped: int = 0) -> str:
    """An NUnit 3 result file of the shape the Unity Test Framework writes."""
    total = passed + len(failed) + skipped
    cases = "\n".join(
        f'    <test-case id="{index}" name="{name}" fullname="Fixture.Tests.{name}" result="Failed" />'
        for index, name in enumerate(failed, start=1000)
    )
    return (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<test-run id="2" testcasecount="{total}" result="{"Failed" if failed else "Passed"}" total="{total}" '
        f'passed="{passed}" failed="{len(failed)}" inconclusive="0" skipped="{skipped}" asserts="0">\n'
        f"{cases}\n"
        "</test-run>"
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

    def tearDown(self) -> None:
        shutil.rmtree(self.root / "log", ignore_errors=True)

    def test_passing_run_reports_its_counts(self) -> None:
        completed = self.run_tests("leaf", results=nunit_results(passed=3, skipped=1))

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("EditMode tests passed for leaf: 4 tests, 3 passed, 0 failed, 1 skipped", completed.stdout)
        self.assertIn("  - leaf: 4 tests, 3 passed, 0 failed, 1 skipped", completed.stdout.splitlines())
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
        self.assertIn("PlayMode tests passed for leaf: 2 tests, 2 passed, 0 failed, 0 skipped", completed.stdout)
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
        self.assertIn("EditMode tests passed for leaf: 1 tests, 1 passed, 0 failed, 0 skipped", completed.stdout)

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
                    "2",
                    results=nunit_results(),
                    log=log,
                    sleep="120",
                    sleeper_file=str(sleeper_file),
                )

                self.assertLess(time.monotonic() - started, 60, "the silent Unity was not stopped")
                self.assertEqual(1, completed.returncode, completed.stdout)
                self.assertIn(
                    f"error: leaf: Unity wrote nothing to its log for 2 s {phrase}, so the script stopped it; "
                    "see log/unity-tests/leaf-editmode.log",
                    completed.stderr,
                )
                self.assertFalse(self.results_file("leaf").exists())
                # The fake's own child, which taskkill /T does not reach on Git Bash, is gone too.
                sleeper = sleeper_file.read_text(encoding="utf-8").strip()
                alive = subprocess.run([str(bash_or_skip()), "-c", f"kill -0 {sleeper}"], capture_output=True, check=False)
                self.assertNotEqual(0, alive.returncode, f"sleep {sleeper} outlived the stopped run")

    def test_unity_that_keeps_logging_is_not_stopped(self) -> None:
        completed = self.run_tests("leaf", "--stall-timeout", "3", results=nunit_results(), ticks="5")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("fake unity tick 5", completed.stdout.splitlines())

    def test_stall_timeout_zero_waits_for_a_silent_unity(self) -> None:
        completed = self.run_tests("leaf", "--stall-timeout", "0", results=nunit_results(), sleep="3")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("EditMode tests passed for leaf: 3 tests, 3 passed, 0 failed, 0 skipped", completed.stdout)

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
        self.assertIn("error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped", completed.stderr)
        self.assertIn("  failed: Fixture.Tests.Breaks", completed.stderr)
        self.assertIn("EditMode tests failed for 1 target(s)", completed.stderr)

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


class StallTimeoutDefaultTests(FixtureRepositoryTestCase):
    def stall_seconds(self, *arguments: str) -> str:
        completed = self.run_functions(
            "test-unity.sh", 'parse_test_arguments "$@"\nprintf %s "$UNITY_STALL_SECONDS"', *arguments
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_a_run_waits_600_seconds_unless_told_otherwise(self) -> None:
        self.assertEqual("600", self.stall_seconds("leaf"))
        self.assertEqual("0", self.stall_seconds("leaf", "--stall-timeout", "0"))
        self.assertEqual("90", self.stall_seconds("leaf", "--stall-timeout", "090"))


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


if __name__ == "__main__":
    unittest.main()
