from __future__ import annotations

import shutil
import unittest
from pathlib import Path

from fixture_repository import MAPPING, FixtureRepositoryTestCase
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


class TestEntryPointTests(FixtureRepositoryTestCase):
    """Runs the real test-unity.sh with a fake Unity that writes the results it is given."""

    TESTABLE_PROJECTS = frozenset({"ProjShared", "ProjLeaf"})

    def run_tests(self, *arguments: str, results: str = "", unity_status: int = 0):
        return self.run_entry_point(
            "test-unity.sh",
            *arguments,
            environment=self.unity_environment(FAKE_UNITY_RESULTS=results, FAKE_UNITY_STATUS=str(unity_status)),
        )

    def results_file(self, target: str) -> Path:
        return self.root / "log" / "unity-tests" / f"{target}-editmode.xml"

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


if __name__ == "__main__":
    unittest.main()
