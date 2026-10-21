from __future__ import annotations

import unittest

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


if __name__ == "__main__":
    unittest.main()
