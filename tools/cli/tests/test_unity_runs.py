from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

from fixture_repository import FAKE_UNITY, MAPPING, FixtureRepositoryTestCase, repository_ignore_line
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
