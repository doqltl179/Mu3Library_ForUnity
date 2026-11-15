from __future__ import annotations

import contextlib
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
from datetime import datetime
from pathlib import Path

from fixture_repository import CONFIGS, FAKE_UNITY, LOAD_HELPER, FixtureRepositoryTestCase
from shell_harness import bash_or_skip
from test_compile_selection import run_plan
from mu3_common.bash import bash_script_command
from mu3_common.unity_projects import is_directory_link
from mu3_fixed_load import fixed_load


class GeneratedProjectRunTests(FixtureRepositoryTestCase):
    """Each run generates its target's project under the unit's work folder, or refreshes it, and
    Unity opens it there."""

    SAMPLE_LINKS = {"leaf": ("Assets/Samples", "PkgLeaf")}

    def test_a_run_generates_the_project_and_the_next_one_refreshes_it_keeping_library(self) -> None:
        environment = self.unity_environment()
        project = self.project_folder("leaf")

        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=environment)

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(f"Generated {project.as_posix()} from tools/unity-projects/leaf:", completed.stdout)
        self.assertIn(f"fake unity ran {project.as_posix()}", completed.stdout.splitlines())
        manifest = json.loads((project / "Packages" / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual("file:" + (self.root / "PkgLeaf").as_posix(), manifest["dependencies"]["fixture.pkgleaf"])
        # The samples compile through the link, which a refresh keeps without walking through it.
        self.assertTrue(is_directory_link(project / "Assets" / "Samples"))
        (project / "Library").mkdir()
        (project / "Library" / "imported").write_text("", encoding="utf-8")

        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=environment)

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(f"Refreshed {project.as_posix()} from tools/unity-projects/leaf: already up to date", completed.stdout)
        self.assertTrue((project / "Library" / "imported").is_file())
        self.assertTrue((self.root / "PkgLeaf" / "Samples~" / "Sample.cs").is_file())

    def test_a_folder_the_tools_did_not_make_stops_the_run_before_unity(self) -> None:
        environment = self.unity_environment()
        project = self.project_folder("leaf")
        project.mkdir(parents=True)
        (project / "Notes.txt").write_text("someone's\n", encoding="utf-8")

        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=environment)

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("has no .mu3-unity-project marker", completed.stderr)
        self.assertNotIn("fake unity ran", completed.stdout)
        self.assertEqual(["Notes.txt"], [path.name for path in project.iterdir()])


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

# A locale whose multibyte encoding is not UTF-8: Git Bash's gawk reads CP932 there and drops bytes
# of a UTF-8 name that it cannot read as such (#494). The Data Store checks must read a name byte
# for byte in it, as they do in the C locale.
NOT_UTF_8_LOCALE = "ja_JP.SJIS"
TEARDOWN_LOSS_FAILURE = (
    f"TearDown : Unhandled log message: [Error] {DATA_STORE_LOSS}\n"
    "Stack trace of the first:\n"
    "UnityEditor.AssetDatabase:LoadAssetAtPath<Mu3Library.Editor.Window.Drawer.DrawerListDrawer> (string)\n"
    "Mu3Library.Tests.Mu3WindowTests:DeleteCreatedPaths (bool) (at Mu3WindowTests.cs:502)\n"
)
# The full name of a test case whose name holds a line break and a tab, as a result file writes the
# test case's name, as the test names itself, and as the script prints it, on one line (#459).
CONTROL_NAME_ATTRIBUTE = "Case(&quot;a&#xA;b&#x9;c&quot;)"
CONTROL_NAME = f'Fixture.Tests.Case("a{chr(10)}b{chr(9)}c")'
CONTROL_NAME_SHOWN = r'Fixture.Tests.Case("a\nb\tc")'


def utp_status(phase: str, name: str, message: str = "") -> str:
    """The line -automated adds to Unity's log as the test named begins or, with its failure message, ends."""
    if phase == "Begin":
        return f'##utp:{{"type":"TestStatus","time":1,"version":2,"phase":"Begin","processId":1,"name":{json.dumps(name)},"state":0}}'
    return (
        f'##utp:{{"type":"TestStatus","time":2,"version":2,"phase":"End","processId":1,"name":{json.dumps(name)},'
        f'"state":{5 if message else 4},"message":{json.dumps(message)},"duration":1}}'
    )


def counted_repeat_name(name: str) -> str:
    """A test's full name as [CountedRepeat] writes it into each of its lines, on one line, as LogName
    in Mu3Library_Base/Tests/Editor/CountedRepeatAttribute.cs writes it (#492): a backslash as two, a
    line break as \\n, and a carriage return as \\r."""
    b = chr(92)
    return name.replace(b, b + b).replace(chr(10), b + "n").replace(chr(13), b + "r")


def counted_repeat_totals(
    name: str, ended: int, runs: int, passed: int, failed: int = 0, inconclusive: int = 0, skipped: int = 0
) -> str:
    """The line [CountedRepeat] logs once the runs of the test named have ended, with the name as
    counted_repeat_name gives it and the totals as Tally.Totals in
    Mu3Library_Base/Tests/Editor/CountedRepeatAttribute.cs writes them."""
    rate = f"{100.0 * failed / ended:.2f}%" if ended else "none, no run ended"
    return (
        f"[Repeat] {counted_repeat_name(name)}: totals: runs {ended} of {runs}, passed {passed}, failed {failed}, "
        f"inconclusive {inconclusive}, skipped {skipped}, failure rate {rate}"
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

    TESTABLE_CONFIGS = frozenset({"shared", "leaf"})

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
            # A name with a line break and a tab is named on one line.
            (utp_status("Begin", CONTROL_NAME), f"while running {CONTROL_NAME_SHOWN}"),
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
                    completed.stderr.splitlines(),
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

    def test_a_failed_or_inconclusive_test_whose_name_holds_a_line_break_and_a_tab_is_named_on_one_line(self) -> None:
        # The result file writes the line break and the tab as character references, which the
        # script replaces, and prints them as \n and \t; a backslash stays as it is.
        inconclusive = "Dir(&quot;C:\\Temp&#x9;&#xA;&quot;)"
        completed = self.run_tests(
            "leaf",
            results=nunit_results(passed=1, failed=(CONTROL_NAME_ATTRIBUTE,), inconclusive=(inconclusive,)),
            unity_status=2,
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        errors = completed.stderr.splitlines()
        start = errors.index("error: leaf: 3 tests, 1 passed, 1 failed, 0 skipped, 1 inconclusive")
        path = r'Fixture.Tests.Dir("C:\Temp\t\n")'
        self.assertEqual([f"  failed: {CONTROL_NAME_SHOWN}", f"  inconclusive: {path}"], errors[start + 1 : start + 3])
        self.assertEqual(
            ["  - leaf: FAILED, 3 tests, 1 passed, 1 failed, 0 skipped, 1 inconclusive", f"    inconclusive: {path}"],
            self.summary(completed)[:2],
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
        for variables in ({}, {"LC_ALL": NOT_UTF_8_LOCALE}):
            with self.subTest(**variables):
                completed = self.run_entry_point(
                    "test-unity.sh",
                    "leaf",
                    environment=self.unity_environment(
                        FAKE_UNITY_RESULTS=nunit_results(
                            passed=2, failed=(failed,), messages={failed: FRAMEWORK_LOSS_FAILURE}
                        ),
                        FAKE_UNITY_STATUS="2",
                        FAKE_UNITY_LOG=data_store_loss_log(name),
                        **variables,
                    ),
                )

                self.assertEqual(1, completed.returncode, completed.stdout)
                environment = (
                    f"  environment: Unity's Data Store service (pid 13480) ended while {name} ran, and the error "
                    "Unity logged for it failed that test, so the test reached no verdict; run the target again"
                )
                errors = completed.stderr.splitlines()
                start = errors.index(
                    "error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from "
                    "the environment"
                )
                self.assertEqual([f"  failed: {name}", environment], errors[start + 1 : start + 3])
                self.assertEqual(f"  {environment}", self.summary(completed)[1])
                self.assertNotIn("Invalid multibyte data", completed.stderr)

    def test_a_test_whose_name_the_log_writes_with_json_escapes_fails_on_the_environment(self) -> None:
        # Unity's log writes the name as a JSON string, here with the escapes json.dumps writes for
        # a line break, a tab, and characters outside ASCII, one of them as a surrogate pair; the
        # result file writes the line break and the tab as character references and the others as
        # themselves, in UTF-8 (#439).
        name = f'Fixture.Tests.Case("a{chr(10)}b{chr(9)}caf{chr(0xE9)} {chr(0x1F600)}")'
        failed = f"Case(&quot;a&#xA;b&#x9;caf{chr(0xE9)} {chr(0x1F600)}&quot;)"
        log = data_store_loss_log(name)
        b = chr(92)
        self.assertIn(f"a{b}nb{b}tcaf{b}u00e9 {b}ud83d{b}ude00", log)
        for variables in ({}, {"LC_ALL": NOT_UTF_8_LOCALE}):
            with self.subTest(**variables):
                completed = self.run_entry_point(
                    "test-unity.sh",
                    "leaf",
                    environment=self.unity_environment(
                        FAKE_UNITY_RESULTS=nunit_results(
                            passed=2, failed=(failed,), messages={failed: FRAMEWORK_LOSS_FAILURE}
                        ),
                        FAKE_UNITY_STATUS="2",
                        FAKE_UNITY_LOG=log,
                        **variables,
                    ),
                )

                self.assertEqual(1, completed.returncode, completed.stdout)
                # The script matches the names themselves, and prints the line break and the tab as
                # \n and \t, so that each line names the test on one line (#459).
                shown = f'Fixture.Tests.Case("a{b}nb{b}tcaf{chr(0xE9)} {chr(0x1F600)}")'
                environment = (
                    f"  environment: Unity's Data Store service (pid 13480) ended while {shown} ran, and the error "
                    "Unity logged for it failed that test, so the test reached no verdict; run the target again"
                )
                errors = completed.stderr.splitlines()
                start = errors.index(
                    "error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from "
                    "the environment"
                )
                self.assertEqual([f"  failed: {shown}", environment], errors[start + 1 : start + 3])
                self.assertEqual(f"  {environment}", self.summary(completed)[1])
                self.assertNotIn("warning: Unity's Data Store service", completed.stdout + completed.stderr)
                self.assertNotIn("Invalid multibyte data", completed.stderr)

    def assert_fails_on_the_environment(
        self, name: str, written: str, logged: str, shown: str, **variables: str
    ) -> None:
        """Run a target whose one failure is Fixture.Tests.<name>, which failed on the error Unity logs
        as its Data Store service ends and nothing else, and check that the run fails on the environment.

        written is the name as the result file writes it, logged as Unity's log writes it in a JSON
        string, and shown as the failed: and the environment: lines print it, which print it alike
        (#493), each after Fixture.Tests.; variables go to the script's
        environment.
        """
        full_name = f"Fixture.Tests.{name}"
        log = data_store_loss_log(full_name).replace(json.dumps(full_name)[1:-1], f"Fixture.Tests.{logged}")
        self.assertIn(f'"name":"Fixture.Tests.{logged}"', log)
        completed = self.run_entry_point(
            "test-unity.sh",
            "leaf",
            environment=self.unity_environment(
                FAKE_UNITY_RESULTS=nunit_results(passed=2, failed=(written,), messages={written: FRAMEWORK_LOSS_FAILURE}),
                FAKE_UNITY_STATUS="2",
                FAKE_UNITY_LOG=log,
                **variables,
            ),
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        environment = (
            f"  environment: Unity's Data Store service (pid 13480) ended while Fixture.Tests.{shown} "
            "ran, and the error Unity logged for it failed that test, so the test reached no verdict; run the target "
            "again"
        )
        errors = completed.stderr.splitlines()
        start = errors.index(
            "error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from the "
            "environment"
        )
        self.assertEqual([f"  failed: Fixture.Tests.{shown}", environment], errors[start + 1 : start + 3])
        self.assertEqual(f"  {environment}", self.summary(completed)[1])
        self.assertNotIn("warning: Unity's Data Store service", completed.stdout + completed.stderr)

    def test_a_test_whose_name_holds_a_character_that_xml_cannot_hold_fails_on_the_environment(self) -> None:
        # The result file writes such a character as the text \u and four lowercase hexadecimal
        # digits, and Unity's log writes an escape and U+0001 as JSON escapes with uppercase digits
        # and U+FFFE and U+FFFF as themselves, as Unity 6000.6.7f1 and com.unity.ext.nunit 2.1.0
        # wrote the names of tests named so (#476). The failed: line prints the result file's text.
        # The environment: line prints the name from the log through one_line_name, which writes
        # each of them as the result file does (#459, #493), so the two lines print it alike. A name
        # read in the C locale, or in a locale whose multibyte encoding is not UTF-8 (#494), is
        # matched alike.
        b = chr(92)
        for character, digits, logged in (
            (chr(0x1B), "001b", f"{b}u001B"),
            (chr(0x01), "0001", f"{b}u0001"),
            (chr(0xFFFE), "fffe", chr(0xFFFE)),
            (chr(0xFFFF), "ffff", chr(0xFFFF)),
        ):
            written = f"Named_a{b}u{digits}b"
            for variables in ({}, {"LC_ALL": "C"}, {"LC_ALL": NOT_UTF_8_LOCALE}):
                with self.subTest(character=f"U+{ord(character):04X}", **variables):
                    self.assert_fails_on_the_environment(
                        f"Named_a{character}b", written, f"Named_a{logged}b", written, **variables
                    )
        # Each time the character comes, it is written so.
        with self.subTest(character="U+001B twice"):
            twice = f"Named_a{b}u001bb{b}u001bc"
            self.assert_fails_on_the_environment(
                f"Named_a{chr(0x1B)}b{chr(0x1B)}c", twice, f"Named_a{b}u001Bb{b}u001Bc", twice
            )

    def test_a_test_whose_name_holds_a_control_character_that_xml_can_hold_fails_on_the_environment(self) -> None:
        # The result file and Unity's log both wrote DEL, U+009B, and U+2028 as themselves (#476), as
        # NUnit's rule writes every character that XML can hold, U+0085 among them, and the result
        # file writes a carriage return as the character reference &#xD;, as it does a tab and a line
        # break, while the log writes it as \r. So the name from the log keeps them, and both lines
        # print them as one_line_name does (#459).
        b = chr(92)
        for character, written, logged, shown in (
            (chr(0x7F), chr(0x7F), chr(0x7F), f"{b}u007f"),
            (chr(0x85), chr(0x85), chr(0x85), f"{b}u0085"),
            (chr(0x2028), chr(0x2028), chr(0x2028), f"{b}u2028"),
            (chr(0x0D), "&#xD;", f"{b}r", f"{b}r"),
        ):
            with self.subTest(character=f"U+{ord(character):04X}"):
                self.assert_fails_on_the_environment(
                    f"Named_a{character}b", f"Named_a{written}b", f"Named_a{logged}b", f"Named_a{shown}b"
                )

    def test_a_test_whose_name_holds_the_text_of_such_an_escape_fails_on_the_environment(self) -> None:
        # The result file writes an escape and the text \u001b alike, while Unity's log writes the
        # text as \\u001b, so the check must not read the result file's \u text as an escape. The
        # text \^[ is written as it is, and as \\^[ in the log (#476).
        b = chr(92)
        for text in (f"{b}u001b", f"{b}^["):
            with self.subTest(text=text):
                name = f"Fixture.Tests.Named_a{text}b"
                failed = f"Named_a{text}b"
                log = data_store_loss_log(name)
                self.assertIn(f'"name":"Fixture.Tests.Named_a{b}{text}b"', log)
                completed = self.run_tests(
                    "leaf",
                    results=nunit_results(passed=2, failed=(failed,), messages={failed: FRAMEWORK_LOSS_FAILURE}),
                    unity_status=2,
                    log=log,
                )

                self.assertEqual(1, completed.returncode, completed.stdout)
                environment = (
                    f"  environment: Unity's Data Store service (pid 13480) ended while {name} ran, and the error "
                    "Unity logged for it failed that test, so the test reached no verdict; run the target again"
                )
                errors = completed.stderr.splitlines()
                start = errors.index(
                    "error: leaf: 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive; its one failure came from "
                    "the environment"
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

    def test_a_run_without_results_names_a_test_whose_name_holds_a_line_break_and_a_tab_on_one_line(self) -> None:
        log = "\n".join([utp_status("Begin", CONTROL_NAME), DATA_STORE_LOSS])
        completed = self.run_tests("leaf", unity_status=1, log=log)

        self.assertEqual(1, completed.returncode, completed.stdout)
        reason = f"Unity exited with 1 and wrote no test results while running {CONTROL_NAME_SHOWN}; see its log above"
        warning = (
            f"warning: Unity's Data Store service (pid 13480) ended while {CONTROL_NAME_SHOWN} ran; Unity read the "
            "files it had imported in its own process from then on"
        )
        errors = completed.stderr.splitlines()
        start = errors.index(f"error: leaf: {reason}")
        self.assertEqual(f"  {warning}", errors[start + 1])
        self.assertEqual([f"  - leaf: FAILED, {reason}", f"    {warning}"], self.summary(completed)[:2])

    def test_a_run_whose_unity_launched_the_data_store_service_keeps_the_service_s_log(self) -> None:
        service_log = "Starting Unity Data Store Application\nShut down UDS service system complete"
        completed = self.run_tests(
            "leaf",
            results=nunit_results(passed=2, failed=("Breaks",), messages={"Breaks": FRAMEWORK_LOSS_FAILURE}),
            unity_status=2,
            log=f"{DATA_STORE_LAUNCH}\n{data_store_loss_log('Fixture.Tests.Breaks')}",
            service_log=service_log,
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn(f"Testing leaf in {self.project_folder('leaf').as_posix()}.", completed.stdout)
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
        self.assertEqual(0, self.run_tests("leaf", results=nunit_results(passed=1)).returncode)
        logs = self.project_folder("leaf") / "Logs"
        logs.mkdir()
        (logs / "UDS-Service.log").write_text("an earlier run's service\n", encoding="utf-8")
        kept = self.root / "log" / "unity-tests" / "leaf-editmode.uds-service.log"
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
        self.assertIn("  - orphan: skipped, tools/unity-projects/orphan/Packages/manifest.json lists no testables", completed.stdout)
        self.assertNotIn("fake unity ran", completed.stdout)

    def test_every_target_runs_after_one_fails(self) -> None:
        completed = self.run_tests("all", results=nunit_results(passed=1, failed=("Breaks",)), unity_status=2)

        self.assertEqual(1, completed.returncode, completed.stdout)
        ran = [line for line in completed.stdout.splitlines() if line.startswith("fake unity ran ")]
        self.assertEqual(2, len(ran), completed.stdout)
        self.assertIn("EditMode tests failed for 2 target(s)", completed.stderr)

    def test_dry_run_shows_where_each_target_would_run(self) -> None:
        completed = self.run_tests("all", "--dry-run")

        self.assertEqual(0, completed.returncode, completed.stderr)
        lines = completed.stdout.splitlines()
        self.assertIn("Selected test targets:", lines)
        self.assertIn(f"  - shared: generate {self.project_folder('shared').as_posix()}", lines)
        self.assertIn("  - layered: skipped, tools/unity-projects/layered/Packages/manifest.json lists no testables", lines)
        self.assertNotIn("fake unity ran", completed.stdout)


def read_record(path: Path) -> dict[str, str]:
    """The key=value lines that the fixed-load helper writes beside a run's results."""
    return dict(line.split("=", 1) for line in path.read_text(encoding="utf-8").splitlines())


class FixedLoadRunTests(FixtureRepositoryTestCase):
    """Runs test-unity.sh with --load-spinners and --repeat; the fixture's spinners wait instead
    of spinning a core."""

    TESTABLE_CONFIGS = frozenset({"shared", "leaf"})
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
            "Results: log/unity-tests/<target>-editmode.xml, with Unity logs as <target>-editmode.log, "
            "load records as <target>-editmode.load, and Data Store service logs as <target>-editmode.uds-service.log",
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
        missing = "tools/cli/src/mu3_fixed_load/fixed_load.py is missing"

        completed = self.run_tests("leaf", results=nunit_results())

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(f"warning: other Unity processes will not be counted: {missing}", completed.stderr)
        self.assertIn(f"    other Unity processes: not counted, {missing}", self.summary(completed))
        self.assertIn(
            "Results: log/unity-tests/<target>-editmode.xml, with Unity logs as <target>-editmode.log "
            "and Data Store service logs as <target>-editmode.uds-service.log",
            completed.stdout.splitlines(),
        )

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

    def test_a_run_that_stops_the_script_before_unity_starts_still_stops_its_spinners(self) -> None:
        # A folder that the tools did not make stops the run as it would generate the project there.
        project = self.project_folder("leaf")
        project.mkdir(parents=True)
        (project / "Notes.txt").write_text("someone's\n", encoding="utf-8")

        completed = self.run_tests("leaf", "--load-spinners", "2", results=nunit_results())

        self.assertEqual(1, completed.returncode, completed.stdout)
        self.assertIn("has no .mu3-unity-project marker", completed.stderr)
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
                "Results: log/unity-tests/<target>-editmode-run<k>.xml, with Unity logs as <target>-editmode-run<k>.log, "
                "load records as <target>-editmode-run<k>.load, and Data Store service logs as "
                "<target>-editmode-run<k>.uds-service.log",
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

    def test_the_totals_name_a_test_whose_name_holds_a_line_break_and_a_tab_on_one_line(self) -> None:
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "2",
            run_file=str(Path(run_directory.name) / "runs"),
            results_1=nunit_results(passed=1, failed=(CONTROL_NAME_ATTRIBUTE,)),
            results_2=nunit_results(
                passed=1, failed=(CONTROL_NAME_ATTRIBUTE,), inconclusive=("Dir(&quot;C:\\Temp&#x9;&#xA;&quot;)",)
            ),
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        summary = self.summary(completed)
        totals = summary.index("  - leaf, 2 runs: 2 of 2 failed; 5 tests, 2 passed, 2 failed, 0 skipped, 1 inconclusive")
        self.assertEqual(
            [
                f"    failed in 2 of 2 runs: {CONTROL_NAME_SHOWN}",
                r'    inconclusive in 1 of 2 runs: Fixture.Tests.Dir("C:\Temp\t\n")',
            ],
            summary[totals + 1 : totals + 3],
        )

    def test_a_run_gives_the_totals_that_counted_repeat_logged_for_each_test(self) -> None:
        # The name keeps what a test case's string argument may hold, the text that precedes the
        # totals included; a line that quotes the totals inside another, such as a test's failure
        # message, is not one; and Unity's log may end its lines in CR LF.
        name = 'Fixture.Tests.Case("a: totals: b")'
        log = "\r\n".join(
            [
                f"[Repeat] {name}: start, runs 400 (MU3LIBRARY_TEST_REPEAT=400)",
                f"[Repeat] {name}: run 1 of 400: Passed in 274.4 ms",
                counted_repeat_totals(name, 400, 400, passed=398, failed=2),
                utp_status("End", name, counted_repeat_totals(name, 1, 1, passed=0, failed=1)),
                counted_repeat_totals("Fixture.Tests.Stopped", 3, 10, passed=2, inconclusive=1),
            ]
        )

        completed = self.run_tests("leaf", results=nunit_results(passed=2), log=log)

        self.assertEqual(0, completed.returncode, completed.stderr)
        summary = self.summary(completed)
        self.assertEqual(
            [
                "  - leaf: 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {name}: runs 400 of 400, passed 398, failed 2, inconclusive 0, skipped 0, "
                "failure rate 0.50%",
                "    counted repeat: Fixture.Tests.Stopped: runs 3 of 10, passed 2, failed 0, inconclusive 1, skipped 0, "
                "failure rate 0.00%",
            ],
            summary[:3],
        )
        self.assertRegex(summary[3], OTHER_UNITY_PROCESSES)
        self.assertTrue(summary[4].startswith("Results: "), summary)

    def test_the_totals_of_repeated_runs_sum_what_counted_repeat_logged_in_each(self) -> None:
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        repeated = "Fixture.Tests.Repeated"
        cancelled = "Fixture.Tests.Cancelled"
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "3",
            run_file=str(Path(run_directory.name) / "runs"),
            results_1=nunit_results(passed=0, failed=("Repeated",)),
            log_1=counted_repeat_totals(repeated, 2, 2, passed=1, failed=1),
            results_2=nunit_results(passed=2),
            log_2="\n".join(
                [counted_repeat_totals(repeated, 1, 1, passed=1), counted_repeat_totals(cancelled, 0, 5, passed=0)]
            ),
            # The third run writes no results, but its log gives the totals of a test it began.
            results_3="",
            log_3=counted_repeat_totals(repeated, 0, 3, passed=0),
        )

        self.assertEqual(1, completed.returncode, completed.stdout)
        summary = [line for line in self.summary(completed) if not re.match(OTHER_UNITY_PROCESSES, line)]
        self.assertEqual(
            [
                "  - leaf run 1 of 3: FAILED, 1 tests, 0 passed, 1 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {repeated}: runs 2 of 2, passed 1, failed 1, inconclusive 0, skipped 0, "
                "failure rate 50.00%",
                "  - leaf run 2 of 3: 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {repeated}: runs 1 of 1, passed 1, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                f"    counted repeat: {cancelled}: runs 0 of 5, passed 0, failed 0, inconclusive 0, skipped 0, "
                "failure rate none, no run ended",
                "  - leaf run 3 of 3: FAILED, Unity exited with 0 and wrote no test results; see its log above",
                f"    counted repeat: {repeated}: runs 0 of 3, passed 0, failed 0, inconclusive 0, skipped 0, "
                "failure rate none, no run ended",
                "  - leaf, 3 runs: 2 of 3 failed; 3 tests, 2 passed, 1 failed, 0 skipped, 0 inconclusive, "
                "in the 2 runs that wrote results",
                f"    failed in 1 of 3 runs: {repeated}",
                f"    counted repeat in 3 of 3 runs: {repeated}: runs 3 of 6, passed 2, failed 1, inconclusive 0, "
                "skipped 0, failure rate 33.33%",
                f"    counted repeat in 1 of 3 runs: {cancelled}: runs 0 of 5, passed 0, failed 0, inconclusive 0, "
                "skipped 0, failure rate none, no run ended",
            ],
            summary[: len(summary) - 2],
        )
        self.assertRegex(summary[-2], self.OTHERS_OVER_RUNS)
        self.assertTrue(summary[-1].startswith("Results: "), summary)

    def test_a_run_that_gives_a_test_s_totals_twice_adds_both_and_counts_as_one_run(self) -> None:
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        repeated = "Fixture.Tests.Repeated"
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "2",
            run_file=str(Path(run_directory.name) / "runs"),
            results=nunit_results(passed=1),
            log_1=chr(10).join(
                [
                    counted_repeat_totals(repeated, 2, 2, passed=1, failed=1),
                    counted_repeat_totals(repeated, 1, 1, passed=1),
                ]
            ),
            log_2=counted_repeat_totals(repeated, 1, 1, passed=1),
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        summary = [line for line in self.summary(completed) if not re.match(OTHER_UNITY_PROCESSES, line)]
        self.assertEqual(
            [
                "  - leaf run 1 of 2: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {repeated}: runs 2 of 2, passed 1, failed 1, inconclusive 0, skipped 0, "
                "failure rate 50.00%",
                f"    counted repeat: {repeated}: runs 1 of 1, passed 1, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                "  - leaf run 2 of 2: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {repeated}: runs 1 of 1, passed 1, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                "  - leaf, 2 runs: 0 of 2 failed; 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat in 2 of 2 runs: {repeated}: runs 4 of 4, passed 3, failed 1, inconclusive 0, "
                "skipped 0, failure rate 25.00%",
            ],
            summary[: len(summary) - 2],
        )

    def test_the_counted_repeat_lines_print_a_name_that_holds_a_tab_and_an_esc_as_the_failed_lines_do(self) -> None:
        # Both lines print the name as the failed in lines do (#475), while the totals still add up
        # by the name itself, so a test named with the text \t and \u001b, printed alike, keeps
        # totals of its own.
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        controls = f'Fixture.Tests.Case("a{chr(9)}b{chr(0x1B)}c")'
        shown = r'Fixture.Tests.Case("a\tb\u001bc")'
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "2",
            run_file=str(Path(run_directory.name) / "runs"),
            results=nunit_results(passed=1),
            log_1=counted_repeat_totals(controls, 2, 2, passed=1, failed=1),
            log_2=chr(10).join(
                [counted_repeat_totals(controls, 1, 1, passed=1), counted_repeat_totals(shown, 4, 4, passed=4)]
            ),
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        summary = [line for line in self.summary(completed) if not re.match(OTHER_UNITY_PROCESSES, line)]
        self.assertEqual(
            [
                "  - leaf run 1 of 2: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {shown}: runs 2 of 2, passed 1, failed 1, inconclusive 0, skipped 0, "
                "failure rate 50.00%",
                "  - leaf run 2 of 2: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {shown}: runs 1 of 1, passed 1, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                f"    counted repeat: {shown}: runs 4 of 4, passed 4, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                "  - leaf, 2 runs: 0 of 2 failed; 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat in 2 of 2 runs: {shown}: runs 3 of 3, passed 2, failed 1, inconclusive 0, "
                "skipped 0, failure rate 33.33%",
                f"    counted repeat in 1 of 2 runs: {shown}: runs 4 of 4, passed 4, failed 0, inconclusive 0, "
                "skipped 0, failure rate 0.00%",
            ],
            summary[: len(summary) - 2],
        )

    def test_the_counted_repeat_lines_give_the_totals_of_a_name_that_holds_a_line_break(self) -> None:
        # [CountedRepeat] writes the name on one line (#492), so the totals of a name with a line
        # break reach the summary, even when the part after the line break starts as a [Repeat] line
        # does, which was then read as the totals of a test named b"). A name with the text \n,
        # printed alike, keeps totals of its own.
        run_directory = tempfile.TemporaryDirectory()
        self.addCleanup(run_directory.cleanup)
        line_break = f'Fixture.Tests.Case("a{chr(10)}[Repeat] b")'
        shown = r'Fixture.Tests.Case("a\n[Repeat] b")'
        completed = self.run_tests(
            "leaf",
            "--repeat",
            "2",
            run_file=str(Path(run_directory.name) / "runs"),
            results=nunit_results(passed=1),
            log_1=counted_repeat_totals(line_break, 2, 2, passed=1, failed=1),
            log_2=chr(10).join(
                [counted_repeat_totals(line_break, 1, 1, passed=1), counted_repeat_totals(shown, 4, 4, passed=4)]
            ),
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        summary = [line for line in self.summary(completed) if not re.match(OTHER_UNITY_PROCESSES, line)]
        self.assertEqual(
            [
                "  - leaf run 1 of 2: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {shown}: runs 2 of 2, passed 1, failed 1, inconclusive 0, skipped 0, "
                "failure rate 50.00%",
                "  - leaf run 2 of 2: 1 tests, 1 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat: {shown}: runs 1 of 1, passed 1, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                f"    counted repeat: {shown}: runs 4 of 4, passed 4, failed 0, inconclusive 0, skipped 0, "
                "failure rate 0.00%",
                "  - leaf, 2 runs: 0 of 2 failed; 2 tests, 2 passed, 0 failed, 0 skipped, 0 inconclusive",
                f"    counted repeat in 2 of 2 runs: {shown}: runs 3 of 3, passed 2, failed 1, inconclusive 0, "
                "skipped 0, failure rate 33.33%",
                f"    counted repeat in 1 of 2 runs: {shown}: runs 4 of 4, passed 4, failed 0, inconclusive 0, "
                "skipped 0, failure rate 0.00%",
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

    TESTABLE_CONFIGS = frozenset({"leaf"})

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

    @staticmethod
    def end_what_the_kill_left(script: fixed_load.ProcessWatch) -> None:
        """Ends every process that descends from a script that taskkill ended outright, and waits
        for each, before the folder that holds the script's output is removed.

        A kill that lands while Git Bash forks leaves the child, which holds the output file it
        inherited and waits for the script to finish the fork until it gives up 300 s later with
        "fatal error in forked process - WFSO timed out after longjmp", so the removal failed with
        WinError 32 (#428). The script is held open so that its ID, which its children name as
        their parent, cannot pass to another process, and each child is held open before it is
        looked up again, for the same reason. The fake Unity and its sleep name a parent that ended
        as Git Bash ran them, so they are not found here; end_fake_unity ends them.
        """
        outlived: list[int] = []
        try:
            first = fixed_load.descendants(fixed_load.list_processes(), script.pid, fixed_load.creation_time_reader())
            watches = [fixed_load.ProcessWatch(pid) for pid in sorted(first - {script.pid})]
            try:
                again = fixed_load.descendants(fixed_load.list_processes(), script.pid, fixed_load.creation_time_reader())
                left = [watch for watch in watches if watch.pid in again and watch.running()]
                for watch in left:
                    # On Windows os.kill ends the process outright, whatever the signal.
                    with contextlib.suppress(OSError):
                        os.kill(watch.pid, signal.SIGTERM)
                deadline = time.monotonic() + 30
                while any(watch.running() for watch in left) and time.monotonic() < deadline:
                    time.sleep(0.05)
                outlived = [watch.pid for watch in left if watch.running()]
            finally:
                for watch in watches:
                    watch.close()
        finally:
            script.close()
        if outlived:
            raise AssertionError(f"processes that the killed script left could not be ended: {outlived}")

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
            script_watch = fixed_load.ProcessWatch(root_pid)
            self.addCleanup(script_watch.close)
            # Only a script held open while it runs keeps its ID from another process, whose
            # children the cleanup would end.
            self.assertTrue(script_watch.running(), "the script ended before the test killed it")
            self.addCleanup(self.end_what_the_kill_left, script_watch)
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
        # all compiles the five fixture targets in the order of their names, and the fake stalls or
        # fails in each, so the first target ends the run and none of the other four may start.
        for fake, status, error in (
            (
                {"sleep": "120"},
                1,
                "error: Unity compilation failed for extra: Unity wrote nothing to its log for 1 s, "
                "so the script stopped it; see log/unity-compile/extra.log",
            ),
            ({"unity_status": 3}, 3, "error: Unity compilation failed for extra (exit 3)"),
        ):
            with self.subTest(error=error):
                completed = self.compile("all", "--stall-timeout", "1", **fake)

                self.assertEqual(status, completed.returncode, completed.stdout)
                self.assertIn(error, completed.stderr)
                ran = [line for line in completed.stdout.splitlines() if line.startswith("fake unity ran ")]
                self.assertEqual(1, len(ran), completed.stdout)
                self.assertTrue(ran[0].endswith("/extra"), ran[0])
                self.assertNotIn("Compiling layered", completed.stdout)
                self.assertNotIn("Unity compilation succeeded", completed.stdout)

    def test_a_compile_keeps_the_record_of_its_data_store_service_and_the_service_s_log(self) -> None:
        service_log = "Starting Unity Data Store Application\nShut down UDS service system complete"
        completed = self.compile("leaf", log=DATA_STORE_LAUNCH, service_log=service_log)

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(f"Compiling leaf in {self.project_folder('leaf').as_posix()}.", completed.stdout)
        logs = self.log_file("leaf").parent
        self.assertEqual(f"{service_log}\n", (logs / "leaf.uds-service.log").read_text(encoding="utf-8"))
        # The helper ran beside the compile, without spinners; the fake is not a child of the
        # script as Windows sees it, so it finds no service (DataStoreRecordTests finds one).
        record = read_record(logs / "leaf.load")
        self.assertEqual(("0", "", "yes", ""), (record["spinners"], record["data_store_pids"], record["complete"], record["error"]))
        self.assertEqual([], list(logs.glob("*.load-stop")))
        self.assertNotIn("will not be recorded", completed.stderr)

    def test_a_compile_whose_unity_did_not_launch_the_service_keeps_no_service_log(self) -> None:
        self.assertEqual(0, self.compile("leaf", log="Compiling scripts").returncode)
        project_logs = self.project_folder("leaf") / "Logs"
        project_logs.mkdir()
        (project_logs / "UDS-Service.log").write_text("an earlier run's service\n", encoding="utf-8")
        kept = self.log_file("leaf").with_name("leaf.uds-service.log")
        kept.write_text("kept by an earlier run\n", encoding="utf-8")

        completed = self.compile("leaf", log="Compiling scripts")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertFalse(kept.exists())
        self.assertEqual("an earlier run's service\n", (project_logs / "UDS-Service.log").read_text(encoding="utf-8"))

    def test_without_the_helper_a_compile_warns_and_keeps_only_the_service_s_log(self) -> None:
        helper = self.root / LOAD_HELPER
        moved = helper.with_name("fixed_load.py.away")
        helper.rename(moved)
        self.addCleanup(moved.rename, helper)
        self.addCleanup(shutil.rmtree, self.root / "log", ignore_errors=True)
        record = self.log_file("leaf").with_name("leaf.load")
        record.parent.mkdir(parents=True)
        record.write_text("complete=yes\n", encoding="utf-8")

        completed = self.compile("leaf", log=DATA_STORE_LAUNCH, service_log="Shut down")

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn(
            "warning: the Data Store service of each run will not be recorded: "
            "tools/cli/src/mu3_fixed_load/fixed_load.py is missing",
            completed.stderr.splitlines(),
        )
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout.splitlines())
        # The record an earlier run left does not stand in for this one's.
        self.assertFalse(record.exists())
        self.assertEqual("Shut down\n", record.with_name("leaf.uds-service.log").read_text(encoding="utf-8"))

    def test_each_target_of_a_compile_stops_its_own_helper(self) -> None:
        # A helper that a later target's start replaced would not be told that its run ended.
        completed = self.compile("all")

        self.assertEqual(0, completed.returncode, completed.stderr)
        logs = self.log_file("leaf").parent
        for key in CONFIGS:
            with self.subTest(target=key):
                self.assertIn(f"Unity compilation succeeded: {key}", completed.stdout.splitlines())
                record = read_record(logs / f"{key}.load")
                self.assertEqual(("yes", ""), (record["complete"], record["error"]))
        self.assertEqual([], list(logs.glob("*.load-stop")))

    def test_records_that_cannot_be_removed_do_not_stop_the_compile(self) -> None:
        # rm becomes a shell function that cannot remove the run's records, as when another program
        # holds them open.
        failing_record_removal = """rm() {
    case "$*" in
        *.load|*.load-stop|*.uds-service.log) return 1 ;;
    esac
    command rm "$@"
}
"""
        self.addCleanup(shutil.rmtree, self.root / "log", ignore_errors=True)
        completed = self.run_functions(
            "compile-unity.sh", failing_record_removal + 'compile_main "$@"', "leaf", environment=self.unity_environment()
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertIn("Unity compilation succeeded: leaf", completed.stdout.splitlines())
        for name in ("leaf.load", "leaf.load-stop", "leaf.uds-service.log"):
            self.assertIn(
                f"warning: could not remove log/unity-compile/{name}, which an earlier run left",
                completed.stderr.splitlines(),
            )

    def test_records_of_an_earlier_run_go_before_a_run_that_fails_to_start(self) -> None:
        # A folder that the tools did not make stops the run as it would generate the project there.
        self.addCleanup(shutil.rmtree, self.root / "log", ignore_errors=True)
        logs = self.log_file("leaf").parent
        logs.mkdir(parents=True)
        (logs / "leaf.uds-service.log").write_text("kept by an earlier run\n", encoding="utf-8")
        (logs / "leaf.load").write_text("earlier=yes\n", encoding="utf-8")
        project = self.project_folder("leaf")
        project.mkdir(parents=True)
        (project / "Notes.txt").write_text("someone's\n", encoding="utf-8")

        completed = self.run_entry_point("compile-unity.sh", "leaf", environment=self.unity_environment())

        self.assertNotEqual(0, completed.returncode, completed.stdout)
        self.assertNotIn("fake unity ran", completed.stdout)
        self.assertFalse((logs / "leaf.uds-service.log").exists())
        # The helper started for the run writes its own record as the script ends.
        self.assertNotIn("earlier", read_record(logs / "leaf.load"))

@unittest.skipUnless(platform.system() == "Windows", "a stand-in service needs a native Windows program to run")
class DataStoreRecordTests(FixtureRepositoryTestCase):
    """The helper that compile-unity.sh starts beside a run records the service the run starts.

    Under Git Bash a program that runs another one built for it, as the fake Unity does, leaves the
    tree that Windows sees, so the service has to be a native program that the script runs itself,
    as Unity runs its service: a copy of cmd.exe named as Unity names the service.
    """

    def test_the_record_gives_the_end_and_the_exit_code_of_a_service_that_descends_from_the_script(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        service = Path(directory.name) / fixed_load.default_data_store_process_name()
        shutil.copy2(Path(os.environ.get("SystemRoot", r"C:\Windows")) / "System32" / "cmd.exe", service)
        base = Path(directory.name) / "leaf"

        completed = self.run_functions(
            "compile-unity.sh",
            'find_load_python || { printf "%s\\n" "$LOAD_UNAVAILABLE"; exit 2; }\n'
            'UNITY_LOG_FILE="$2.log"\n'
            'start_load_helper "$2"\n'
            "status=0\n"
            # The service lives about a second, several of the helper's looks.
            'MSYS2_ARG_CONV_EXCL="*" "$1" /d /c "ping -n 2 127.0.0.1 >nul & exit 7" || status=$?\n'
            "stop_load_helper\n"
            'printf "service exited with %s\\n" "$status"\n',
            service.as_posix(),
            base.as_posix(),
        )

        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertIn("service exited with 7", completed.stdout.splitlines())
        record = read_record(base.with_name("leaf.load"))
        self.assertEqual(("0", "yes"), (record["spinners"], record["complete"]))
        [pid] = record["data_store_pids"].split()
        self.assertEqual("7", record[f"data_store_{pid}_exit_code"])
        started = datetime.fromisoformat(record[f"data_store_{pid}_started"])
        ended = datetime.fromisoformat(record[f"data_store_{pid}_ended"])
        self.assertLess(started, ended)
        self.assertFalse(base.with_name("leaf.load-stop").exists())


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

    def test_replaces_the_escapes_of_a_line_break_a_tab_and_characters_outside_ascii(self) -> None:
        name = f'Fixture.Tests.Case("a{chr(10)}b{chr(9)}caf{chr(0xE9)} {chr(0x1F600)}")'

        self.assertEqual(f"{name}\n", self.unfinished(utp_status("Begin", name)))

    def test_names_nothing_once_the_last_test_ended_or_when_none_began(self) -> None:
        self.assertEqual("", self.unfinished(UTP_BEGIN, UTP_END))
        self.assertEqual("", self.unfinished("Loading"))


class JsonStringTextTests(FixtureRepositoryTestCase):
    """json_string_text replaces the escapes of a JSON string, the form in which Unity's log writes a
    test's name, by JSON's rules, and writes the characters in UTF-8 as xml_attribute_text does."""

    def text(self, escaped: str) -> bytes:
        # A file carries the text both ways, since a command line can change its backslashes and
        # quotes, and output read as text, its carriage returns.
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        source = Path(directory.name) / "escaped.txt"
        target = Path(directory.name) / "text.txt"
        source.write_bytes(escaped.encode("ascii"))
        completed = self.run_functions(
            "test-unity.sh",
            'IFS= read -r escaped <"$1" || true\njson_string_text "$escaped"\nprintf "%s" "$JSON_TEXT" >"$2"',
            str(source),
            str(target),
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return target.read_bytes()

    def test_replaces_each_escape_of_one_character(self) -> None:
        b = chr(92)
        escaped = "".join(f"{b}{character}" for character in f'"{b}/bfnrt')

        self.assertEqual(
            f'a"{b}/{chr(8)}{chr(12)}{chr(10)}{chr(13)}{chr(9)}z'.encode("utf-8"), self.text(f"a{escaped}z")
        )

    def test_replaces_a_unicode_escape_in_either_case_and_a_surrogate_pair_with_the_character_in_utf_8(self) -> None:
        b = chr(92)
        escaped = f"{b}u0041 {b}u00e9{b}u00C9 {b}u4E2D {b}uD83D{b}uDE00 {b}ud83d{b}ude00"

        self.assertEqual(
            f"A {chr(0xE9)}{chr(0xC9)} {chr(0x4E2D)} {chr(0x1F600)} {chr(0x1F600)}".encode("utf-8"), self.text(escaped)
        )

    def test_reads_from_left_to_right_and_leaves_an_escape_that_stands_for_no_character_as_it_is(self) -> None:
        # The backslash that an escaped backslash leaves starts no escape. A bash string cannot hold
        # U+0000, a surrogate without its other half is no character, and neither is an escape that
        # JSON does not define, so each of them stays, as xml_attribute_text leaves &#0;.
        b = chr(92)
        kept = f"{b}u0000 {b}uD83D {b}uDE00{b}uD83D {b}x {b}u12G {b}"

        self.assertEqual(f"{b}u0041 {b}uD83DA {kept}".encode("ascii"), self.text(f"{b}{b}u0041 {b}uD83D{b}u0041 {kept}"))


class OneLineNameTests(FixtureRepositoryTestCase):
    """one_line_name gives a test's name in the form the script prints it in, on one line (#459)."""

    def shown(self, name: str, environment: dict[str, str] | None = None) -> str:
        # A file carries the name both ways, since a command line cannot carry every control
        # character, and output read as text turns a carriage return into a line break.
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        source = Path(directory.name) / "name.txt"
        target = Path(directory.name) / "shown.txt"
        source.write_bytes(name.encode("utf-8"))
        completed = self.run_functions(
            "test-unity.sh",
            "IFS= read -r -d '' name <\"$1\" || true\none_line_name \"$name\"\nprintf '%s' \"$ONE_LINE_NAME\" >\"$2\"",
            str(source),
            str(target),
            environment=environment,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return target.read_bytes().decode("utf-8")

    def test_writes_a_tab_a_line_break_and_a_carriage_return_as_t_n_and_r(self) -> None:
        self.assertEqual(r'F.Case("a\nb\tc\r\nd")', self.shown(f'F.Case("a{chr(10)}b{chr(9)}c{chr(13)}{chr(10)}d")'))

    def test_writes_every_other_control_character_and_the_line_and_paragraph_separators_as_u_and_four_digits(self) -> None:
        # As NUnit's XML writes a character that XML cannot hold, such as \u001b and \ufffe; so a
        # name with U+FFFE or U+FFFF from Unity's log, which writes them as themselves, is printed as
        # the result file gives it (#493).
        self.assertEqual(
            r"a\u001bb\u0007\u007f\u0085\u009f\u2028\u2029\ufffe\uffff",
            self.shown(
                f"a{chr(0x1B)}b{chr(7)}{chr(0x7F)}{chr(0x85)}{chr(0x9F)}{chr(0x2028)}{chr(0x2029)}"
                f"{chr(0xFFFE)}{chr(0xFFFF)}"
            ),
        )
        codes = [*range(0x01, 0x20), *range(0x7F, 0xA0), 0x2028, 0x2029, 0xFFFE, 0xFFFF]
        named = {0x09: r"\t", 0x0A: r"\n", 0x0D: r"\r"}
        expected = "|".join(named.get(code, f"\\u{code:04x}") for code in codes)
        for environment in (None, {**self._environment(), "LC_ALL": "C"}, {**self._environment(), "LC_ALL": NOT_UTF_8_LOCALE}):
            with self.subTest(environment=environment["LC_ALL"] if environment else "default"):
                self.assertEqual(expected, self.shown("|".join(chr(code) for code in codes), environment))

    def test_keeps_every_other_character_and_a_name_without_control_characters_as_it_is(self) -> None:
        # A backslash stays as it is, as in the \\ and \n of the name NUnit gives a test case that
        # takes a string, and as in the \u001b with which NUnit's XML writes a control character.
        for name in (
            r'F.Case("C:\\Temp\n", "\u001b")',
            f'F.Text("caf{chr(0xE9)} {chr(0x4E2D)} {chr(0x1F600)} {chr(0xA0)}{chr(0x2027)}{chr(0x202A)}{chr(0xFFFD)}{chr(0xFFEF)}")',
            # The text \ufffe is printed as it is, which is how U+FFFE is printed too.
            r"F.Named_a\ufffeb",
            "F.Odd(&foo; &#0; \\u0000) ~ !",
            "",
        ):
            with self.subTest(name=name):
                self.assertEqual(name, self.shown(name))


class CountedRepeatTotalsTests(FixtureRepositoryTestCase):
    """find_counted_repeats reads the totals that [CountedRepeat] logs, and compute_failure_rate gives
    the failure rate of summed totals as the attribute gives it."""

    # Writes what find_counted_repeats found into a file, a test a record, each ended by a NUL, so
    # that a name keeps its line breaks and carriage returns, which output read as text would change.
    PRINT_FOUND = (
        'find_counted_repeats "$1"\n'
        "for ((i = 0; i < ${#COUNTED_REPEAT_TESTS[@]}; i++)); do\n"
        '    printf "%s|%s|%s\\0" "${COUNTED_REPEAT_TESTS[$i]}" "${COUNTED_REPEAT_COUNTS[$i]}" "${COUNTED_REPEAT_RATES[$i]}"\n'
        'done >"$2"'
    )

    def found(self, *lines: str) -> list[str]:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        log = Path(directory.name) / "unity.log"
        found = Path(directory.name) / "found.txt"
        log.write_bytes("".join(f"{line}\n" for line in lines).encode("utf-8"))
        completed = self.run_functions("test-unity.sh", self.PRINT_FOUND, str(log), str(found))
        self.assertEqual(0, completed.returncode, completed.stderr)
        return found.read_bytes().decode("utf-8").split(chr(0))[:-1]

    def test_reads_each_test_s_counts_and_rate_from_a_line_of_its_own(self) -> None:
        self.assertEqual(
            ["Fixture.Tests.Repeated|400 400 398 2 0 0|0.50%", "Fixture.Tests.None|0 5 0 0 0 0|none, no run ended"],
            self.found(
                "[Repeat] Fixture.Tests.Repeated: start, runs 400 (MU3LIBRARY_TEST_REPEAT=400)",
                "[Repeat] Fixture.Tests.Repeated: run 400 of 400: Failed in 12.5 ms: Expected 1 | But was 2",
                counted_repeat_totals("Fixture.Tests.Repeated", 400, 400, passed=398, failed=2) + "\r",
                counted_repeat_totals("Fixture.Tests.None", 0, 5, passed=0),
            ),
        )

    def test_takes_the_longest_name_that_leaves_the_totals(self) -> None:
        # A string argument that holds a whole totals line stays in the name.
        name = f'Fixture.Tests.Case("{counted_repeat_totals("x", 1, 1, passed=1)}")'

        self.assertEqual([f"{name}|2 2 1 1 0 0|50.00%"], self.found(counted_repeat_totals(name, 2, 2, passed=1, failed=1)))

    def test_passes_over_lines_that_only_quote_the_totals_or_do_not_hold_them_whole(self) -> None:
        line = counted_repeat_totals("Fixture.Tests.Repeated", 2, 2, passed=1, failed=1)

        self.assertEqual(
            [],
            self.found(
                utp_status("End", "Fixture.Tests.Repeated", line),
                f" {line}",
                line.replace("runs 2 of 2", "runs 02 of 2"),
                line.replace("50.00%", "50.00"),
                line.replace(", skipped 0", ""),
            ),
        )
        self.assertEqual([], self.found())

    def test_reads_the_name_back_from_the_one_line_form_of_the_attribute(self) -> None:
        # [CountedRepeat] writes a backslash as two, a line break as \n, and a carriage return as \r
        # (#492), so the totals of a name with a line break take one line, and the name read back is
        # the test's own: a name with the text \n stays apart from one with a line break, and a tab,
        # a quote, and the text of another escape, such as \u001b, stay as they are.
        b = chr(92)
        names = [
            f'Fixture.Tests.Case("a{chr(10)}b")',
            f'Fixture.Tests.Case("a{b}nb")',
            f'Fixture.Tests.Case("c{chr(13)}{chr(10)}d{chr(13)}e")',
            f'Fixture.Tests.Case("C:{b}{b}Temp{b}{b}", "{b}u001b {b}{b}n {b}"{chr(9)}")',
            f'Fixture.Tests.Case("f{chr(10)}")',
        ]
        self.assertEqual(
            r'[Repeat] Fixture.Tests.Case("a\nb"): totals: runs 2 of 2, passed 1, failed 1, inconclusive 0, '
            "skipped 0, failure rate 50.00%",
            counted_repeat_totals(names[0], 2, 2, passed=1, failed=1),
        )
        self.assertEqual(
            r'Fixture.Tests.Case("C:\\\\Temp\\\\", "\\u001b \\\\n \\"' + chr(9) + '")', counted_repeat_name(names[3])
        )
        self.assertEqual(
            [f"{name}|2 2 1 1 0 0|50.00%" for name in names],
            self.found(*(counted_repeat_totals(name, 2, 2, passed=1, failed=1) for name in names)),
        )

    def test_reads_no_other_test_s_totals_from_a_name_whose_line_break_precedes_a_repeat_line_s_start(self) -> None:
        # Logged as it is, the part of such a name after its line break began a line that read as the
        # totals of a test named y") (#492).
        name = f'Fixture.Tests.Case("x{chr(10)}[Repeat] y")'

        self.assertEqual(
            [f"{name}|2 2 1 1 0 0|50.00%", "Fixture.Tests.After|1 1 1 0 0 0|0.00%"],
            self.found(
                counted_repeat_totals(name, 2, 2, passed=1, failed=1),
                counted_repeat_totals("Fixture.Tests.After", 1, 1, passed=1),
            ),
        )

    def test_the_failure_rate_is_given_to_two_decimals_with_a_half_rounded_up(self) -> None:
        completed = self.run_functions(
            "test-unity.sh",
            'for counts in "3 1" "3 2" "8 1" "800 1" "1 0" "5 5" "0 0"; do\n'
            "    compute_failure_rate $counts\n"
            '    printf "%s\\n" "$FAILURE_RATE"\n'
            "done",
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(
            ["33.33%", "66.67%", "12.50%", "0.13%", "0.00%", "100.00%", "none, no run ended"],
            completed.stdout.splitlines(),
        )


class DataStoreLossTests(FixtureRepositoryTestCase):
    """find_data_store_loss reads from Unity's log the process ID of its Data Store service, once the
    service ended, and the test that was running then."""

    def loss(self, *lines: str, **variables: str) -> tuple[str, str]:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        log = Path(directory.name) / "unity.log"
        log.write_text("".join(f"{line}\n" for line in lines), encoding="utf-8")
        completed = self.run_functions(
            "test-unity.sh",
            'find_data_store_loss "$1"\nprintf "%s|%s|%s" "$DATA_STORE_LOSS_PID" "$DATA_STORE_LOSS_TEST" "$DATA_STORE_LOSS_ERROR"',
            str(log),
            environment={**self._environment(), **variables} if variables else None,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual("", completed.stderr)
        pid, test, error = completed.stdout.split("|")
        self.assertEqual(DATA_STORE_LOSS.replace("13480", pid) if pid else "", error)
        return pid, test

    def test_names_the_service_and_the_test_that_had_begun_and_not_ended(self) -> None:
        self.assertEqual(("13480", "Fixture.Tests.Breaks"), self.loss(*data_store_loss_log("Fixture.Tests.Breaks").split("\n")))
        self.assertEqual(("13480", ""), self.loss(*data_store_loss_log(None).split("\n")))

    def test_keeps_the_escaped_quotes_of_a_string_argument(self) -> None:
        name = 'Fixture.Tests.Case("two \\ words")'

        self.assertEqual(("13480", name), self.loss(utp_status("Begin", name), DATA_STORE_LOSS))

    def test_replaces_the_escapes_of_a_line_break_a_tab_and_characters_outside_ascii(self) -> None:
        name = f'Fixture.Tests.Case("a{chr(10)}b{chr(9)}caf{chr(0xE9)} {chr(0x1F600)}")'

        self.assertEqual(("13480", name), self.loss(utp_status("Begin", name), DATA_STORE_LOSS))

    def test_reads_a_name_byte_for_byte_in_a_locale_that_is_not_utf_8(self) -> None:
        # Unity's log writes U+FFFE as itself (#476), and other characters outside ASCII may come
        # as themselves too; in CP932 gawk dropped bytes of such a name, so that U+FFFE's EF BF BE
        # became BF BE (#494).
        name = f"Fixture.Tests.Named_a{chr(0xFFFE)}b{chr(0xE9)}{chr(0x1F600)}"
        begin = utp_status("Begin", name).replace(json.dumps(name), json.dumps(name, ensure_ascii=False))
        self.assertIn(chr(0xFFFE), begin)

        for variables in ({}, {"LC_ALL": "C"}, {"LC_ALL": NOT_UTF_8_LOCALE}):
            with self.subTest(**variables):
                self.assertEqual(("13480", name), self.loss(begin, DATA_STORE_LOSS, **variables))

    def test_takes_the_first_end_and_reads_it_from_its_log_entry_too(self) -> None:
        later = DATA_STORE_LOSS.replace("13480", "2")

        self.assertEqual(("13480", ""), self.loss("Loading", utp_error(DATA_STORE_LOSS), utp_error(later), later))

    def test_names_nothing_when_only_a_failure_message_quotes_the_error_or_no_service_ended(self) -> None:
        ended = utp_status("End", "Fixture.Tests.Quotes", f"Expected the log '[Error] {DATA_STORE_LOSS}'")

        self.assertEqual(("", ""), self.loss(utp_status("Begin", "Fixture.Tests.Quotes"), ended))
        self.assertEqual(("", ""), self.loss("Unity Data Store running and initialised", UTP_BEGIN, UTP_END))
        self.assertEqual(("", ""), self.loss("Unity Data Store service (pid x) is no longer running."))


class LocaleTests(FixtureRepositoryTestCase):
    """test-unity.sh runs in the C locale when the locale's encoding is not UTF-8, so that bash, grep,
    and awk read the bytes of a UTF-8 test name as they are (#494)."""

    def locale_after_sourcing(self, locale: str) -> str:
        completed = self.run_functions(
            "test-unity.sh",
            'printf "%s|%s" "${LC_ALL-unset}" "$(locale charmap)"',
            environment={**self._environment(), "LC_ALL": locale},
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_a_locale_whose_encoding_is_not_utf_8_becomes_the_c_locale(self) -> None:
        for locale in (NOT_UTF_8_LOCALE, "ko_KR.EUC-KR", "en_US.ISO-8859-1"):
            with self.subTest(locale=locale):
                self.assertEqual("C|ANSI_X3.4-1968", self.locale_after_sourcing(locale))

    def test_a_utf_8_locale_stays(self) -> None:
        self.assertEqual("ko_KR.UTF-8|UTF-8", self.locale_after_sourcing("ko_KR.UTF-8"))


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
    """--editor-version runs another Editor, which must only ever open a project of its own set up for it."""

    TESTABLE_CONFIGS = frozenset({"leaf"})

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
        """Gives the leaf config Unity packages and a lock file until the test ends."""
        packages = self.root / "tools" / "unity-projects" / "leaf" / "Packages"
        manifest, lock = packages / "manifest.json", packages / "packages-lock.json"
        original = manifest.read_bytes()
        self.addCleanup(manifest.write_bytes, original)
        self.addCleanup(lock.unlink, missing_ok=True)

        content = json.loads(original)
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

    def retargeted_project(self) -> Path:
        return self.unit_folder(self.own_work_root()) / f"leaf-{FLOOR_VERSION}"

    def test_project_of_its_own_is_retargeted_to_the_requested_editor(self) -> None:
        manifest, lock = self.give_leaf_unity_packages()
        config_manifest = manifest.read_bytes()

        completed = self.run_entry_point(
            "compile-unity.sh",
            "leaf",
            "--editor-version",
            FLOOR_VERSION,
            environment=self.unity_environment(UNITY_EDITOR=str(self.fake_editor())),
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        project = self.retargeted_project()
        self.assertIn(f"Compiling leaf in {project.as_posix()}.", completed.stdout)
        self.assertIn(f"fake unity ran {project.as_posix()}", completed.stdout.splitlines())
        self.assertFalse(self.project_folder("leaf").exists())
        self.assertEqual(
            f"m_EditorVersion: {FLOOR_VERSION}\n",
            (project / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8"),
        )
        self.assertFalse((project / "Packages" / "packages-lock.json").exists())
        self.assertEqual(
            {
                "fixture.pkgshared": "file:" + (self.root / "PkgShared").as_posix(),
                "fixture.pkglayered": "file:" + (self.root / "PkgLayered").as_posix(),
                "fixture.pkgleaf": "file:" + (self.root / "PkgLeaf").as_posix(),
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
            f"warning: Unity {FLOOR_VERSION} does not bundle com.unity.terrain-tools, so the project keeps 5.3.3",
            completed.stderr,
        )
        # The config keeps its own version, manifest, and lock file.
        self.assertEqual(config_manifest, manifest.read_bytes())
        self.assertTrue(lock.is_file())

    def test_every_target_of_both_entry_points_gets_a_project_of_its_own(self) -> None:
        for entry_point in ("compile-unity.sh", "test-unity.sh"):
            with self.subTest(entry_point=entry_point):
                completed = self.run_entry_point(
                    entry_point, "all", "--editor-version", FLOOR_VERSION, "--dry-run", environment=self.unity_environment()
                )

                self.assertEqual(0, completed.returncode, completed.stderr)
                self.assertIn(f"  - leaf: generate {self.retargeted_project().as_posix()}", completed.stdout.splitlines())

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


# Where Unity Hub keeps an Editor's executable inside the folder of its version.
HUB_EXECUTABLE = {"Windows": "Editor/Unity.exe", "Darwin": "Unity.app/Contents/MacOS/Unity"}.get(
    platform.system(), "Editor/Unity"
)


class CrlfCheckoutTests(FixtureRepositoryTestCase):
    """A checkout with core.autocrlf holds each config's ProjectVersion.txt and Packages/manifest.json
    with CRLF, and every value the entry points read from them must drop the CR.

    A CR left on a line would change the Editor version the scripts look for. The tool that reads
    the configs prints its own lines with CRLF on Windows, which the scripts must drop too.
    EditorVersionTests retargets a project from a manifest written with CRLF.
    """

    TESTABLE_CONFIGS = frozenset({"leaf"})

    def setUp(self) -> None:
        super().setUp()
        version_file = self.root / "tools" / "unity-projects" / "leaf" / "ProjectSettings" / "ProjectVersion.txt"
        self.addCleanup(version_file.write_bytes, version_file.read_bytes())
        version_file.write_bytes(
            b"m_EditorVersion: 6000.6.7f1\r\nm_EditorVersionWithRevision: 6000.6.7f1 (0123456789ab)\r\n"
        )
        # As bytes, so that every manifest holds CRLF on every OS.
        for config in CONFIGS:
            manifest = self.root / "tools" / "unity-projects" / config / "Packages" / "manifest.json"
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
            "compile-unity.sh", 'load_project_configs\nfind_target leaf\nprintf "[%s]" "${CONFIG_EDITOR_VERSIONS[$TARGET_INDEX]}"'
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

    def test_each_package_compiles_in_the_configs_whose_manifest_names_it(self) -> None:
        completed = self.run_functions(
            "compile-unity.sh",
            'load_project_configs\nfor i in "${!PACKAGE_FOLDERS[@]}"; do printf "%s: %s\\n" "${PACKAGE_FOLDERS[$i]}" "${PACKAGE_CONSUMER_TARGETS[$i]}"; done',
        )

        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertEqual(
            ["PkgLayered: layered leaf", "PkgLeaf: leaf", "PkgShared: layered leaf shared", "PkgSharedExtra: extra"],
            completed.stdout.splitlines(),
        )

    def test_only_the_manifest_that_lists_testables_has_tests_to_run(self) -> None:
        completed = self.run_entry_point("test-unity.sh", "all", "--dry-run")

        self.assertEqual(0, completed.returncode, completed.stderr)
        plan = run_plan(completed.stdout)
        self.assertEqual(f"generate {self.unit_folder(self.class_work_root).as_posix()}/leaf", plan["leaf"])
        for key in CONFIGS:
            if key != "leaf":
                self.assertEqual(f"skipped, tools/unity-projects/{key}/Packages/manifest.json lists no testables", plan[key])

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

    TESTABLE_CONFIGS = frozenset({"shared", "leaf"})

    def test_each_target_is_warned_about_once_before_unity_starts(self) -> None:
        runs = (
            ("compile-unity.sh", "compile_main", [f"tools/unity-projects/{key}" for key in CONFIGS]),
            ("test-unity.sh", "test_main", ["tools/unity-projects/leaf", "tools/unity-projects/shared"]),
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

    TESTABLE_CONFIGS = frozenset({"leaf"})

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
                    self.assertEqual(f"fake unity ran {self.project_folder('leaf').as_posix()}", ran)
                    self.assertTrue(kept.is_file(), completed.stdout)
                    self.assertEqual([], list(self.outside.iterdir()))

    def test_sourced_entry_points_find_the_repository_from_outside_it(self) -> None:
        body = 'load_project_configs\nprintf "root %s\\ntargets %s\\n" "$REPO_ROOT" "${TARGET_KEYS[*]}"'
        for entry_point in ("compile-unity.sh", "test-unity.sh"):
            for spelling, script in self.spellings(entry_point).items():
                with self.subTest(entry_point=entry_point, spelling=spelling):
                    completed = self.run_functions(entry_point, body, cwd=self.outside, script=script)

                    self.assertEqual(0, completed.returncode, completed.stderr)
                    root, targets = completed.stdout.splitlines()
                    self.assertTrue(root.endswith("/repository"), root)
                    self.assertEqual(f"targets {' '.join(CONFIGS)}", targets)


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

    def warning(self, length: int, host: str = "windows") -> str:
        # On Windows /c/... becomes C:\... of the same length; elsewhere it stays as it is.
        folder = "/c/" + "p" * (length - 3)
        completed = self.run_functions(
            "compile-unity.sh",
            'HOST_PLATFORM="$2"\ncheck_project_path_length "$1"\nprintf %s "$PATH_LIMIT_WARNING"',
            folder,
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
                "set a shorter MU3_WORK_ROOT or pass a shorter --work-dir, so that the project path has at most "
                "97 characters (tools/README.md)"
            ),
            warning,
        )

    def test_another_host_is_not_warned(self) -> None:
        self.assertEqual("", self.warning(200, host="linux"))
        self.assertEqual("", self.warning(200, host="macos"))


@unittest.skipUnless(platform.system() == "Windows", "only Windows limits the paths Unity's asset import reads")
class UnreadableLongPathRunTests(FixtureRepositoryTestCase):
    """Both entry points warn when Unity's log names a file it could not read past the path limit."""

    TESTABLE_CONFIGS = frozenset({"shared", "leaf"})
    LOG = unreadable(package_cache_path(270, "Window.uss"))

    def test_compile_warns_after_each_target_and_again_at_the_end_of_a_run_of_several(self) -> None:
        for target, keys in (("leaf", ["leaf"]), ("all", list(CONFIGS))):
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
            "warning: extra: Unity could not read 1 file(s) whose paths have 260 characters or more; "
            "see log/unity-compile/extra.log"
        )
        self.assertEqual(
            [warning, "error: Unity compilation failed for extra (exit 3)"], lines[-2:], completed.stdout
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
class DeepWorkRootTests(FixtureRepositoryTestCase):
    """A work root deep enough that the longest package path measured below a project reaches the
    Windows limit is warned about before Unity starts."""

    TESTABLE_CONFIGS = frozenset({"leaf"})

    def test_both_entry_points_warn_before_unity_starts(self) -> None:
        # Long enough that the project folder passes 97 characters below any temporary directory.
        deep = self.own_work_root() / ("deep" * 22)
        for entry_point, main in (("compile-unity.sh", "compile_main"), ("test-unity.sh", "test_main")):
            with self.subTest(entry_point=entry_point):
                completed = self.run_functions(
                    entry_point,
                    f'{main} "$@" 2>&1',
                    "leaf",
                    environment=self.unity_environment(FAKE_UNITY_RESULTS=nunit_results(), MU3_WORK_ROOT=str(deep)),
                )

                self.assertEqual(0, completed.returncode, completed.stdout)
                lines = completed.stdout.splitlines()
                [warning] = [index for index, line in enumerate(lines) if line.startswith("warning: leaf: the project path ")]
                self.assertRegex(lines[warning], r"\\leaf is \d+ characters, so the longest package file path")
                self.assertIn("set a shorter MU3_WORK_ROOT or pass a shorter --work-dir", lines[warning])
                [ran] = [index for index, line in enumerate(lines) if line.startswith("fake unity ran ")]
                self.assertLess(warning, ran, completed.stdout)
                if entry_point == "test-unity.sh":
                    self.assertIn(f"    {lines[warning].replace('warning: leaf: ', 'warning: ', 1)}", lines)


if __name__ == "__main__":
    unittest.main()
