from __future__ import annotations

import contextlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from importlib.metadata import EntryPoint
from pathlib import Path
from unittest import mock

from common.checkout import repo_root

SCRIPT_PATH = repo_root() / "tools" / "dev" / "ensure_tools.py"
_SPEC = importlib.util.spec_from_file_location("mu3_dev_ensure_tools", SCRIPT_PATH)
assert _SPEC is not None and _SPEC.loader is not None
ensure_tools = importlib.util.module_from_spec(_SPEC)
# dataclasses looks the module up by name while it runs.
sys.modules[_SPEC.name] = ensure_tools
_SPEC.loader.exec_module(ensure_tools)

TOOLS_DIR = repo_root() / "tools" / "cli"
SCRIPTS_DIR = Path("venv-scripts")


class FakeDistribution:
    """What check() reads of an installed distribution."""

    def __init__(
        self,
        name: str,
        version: str = "0.1.0",
        requires: list[str] | None = None,
        scripts: dict[str, str] | None = None,
        source: Path | None = TOOLS_DIR,
        editable: bool = True,
    ) -> None:
        self.metadata = {"Name": name, "Version": version}
        self.requires = requires
        self.entry_points = [EntryPoint(n, v, "console_scripts") for n, v in (scripts or {}).items()]
        self._direct_url = (
            None
            if source is None
            else json.dumps({"url": source.resolve().as_uri(), "dir_info": {"editable": editable}})
        )

    def read_text(self, filename: str) -> str | None:
        return self._direct_url if filename == "direct_url.json" else None


def current_project():
    return ensure_tools.read_project(TOOLS_DIR / "pyproject.toml")


def matching_install(project) -> FakeDistribution:
    return FakeDistribution(
        project.name,
        project.version,
        requires=list(project.dependencies),
        scripts=dict(project.scripts),
    )


def every_launcher(name: str, folder: Path) -> bool:
    return True


class ReadProjectTests(unittest.TestCase):
    def test_the_minimal_reader_reads_the_tools_pyproject_as_tomllib_does(self) -> None:
        # Python 3.10 has no tomllib and uses the minimal reader; both must agree on the real file.
        try:
            import tomllib
        except ModuleNotFoundError:
            self.skipTest("tomllib comes with Python 3.11")

        text = (TOOLS_DIR / "pyproject.toml").read_text(encoding="utf-8")
        project = tomllib.loads(text)["project"]
        minimal = ensure_tools.read_minimal_toml(text)["project"]

        for key in ("name", "version", "dependencies", "scripts"):
            self.assertEqual(project[key], minimal[key], key)
        self.assertIn("mu3-repo", minimal["scripts"])

    def test_the_minimal_reader_takes_arrays_over_several_lines_with_comments(self) -> None:
        text = "\n".join(
            [
                "[build-system]",
                'requires = ["setuptools>=68"]',
                "",
                "[project]",
                'name = "mu3-tools"  # a trailing comment',
                'version = "1.2.3"',
                "dependencies = [",
                '    # a comment with "quotes" in it',
                '    "typer>=0.12,<1.0",',
                "    \"tomli; python_version < '3.11'\",",
                "]",
                "",
                "[project.scripts]",
                'mu3-repo = "repo.cli:app"',
                '"mu3-unity" = "unity.cli:app"',
                "",
                "[tool.setuptools]",
                'package-dir = {"" = "src"}',
            ]
        )

        project = ensure_tools.read_minimal_toml(text)["project"]

        self.assertEqual("mu3-tools", project["name"])
        self.assertEqual("1.2.3", project["version"])
        self.assertEqual(["typer>=0.12,<1.0", "tomli; python_version < '3.11'"], project["dependencies"])
        self.assertEqual({"mu3-repo": "repo.cli:app", "mu3-unity": "unity.cli:app"}, project["scripts"])

    def test_the_minimal_reader_drops_comments_at_the_end_of_a_line_but_not_inside_a_string(self) -> None:
        text = "\n".join(
            [
                "[project]  # main",
                "name = 'mu3-tools'  # a literal string",
                'version = "1.0#not-a-comment"',
                'dependencies = ["typer>=0.12,<1.0"]  # pinned',
                "",
                "[project.scripts]  # commands",
                'mu3-repo = "repo.cli:app"',
                "",
                "[project.optional-dependencies]",
                "lint = [",
                '    "ruff",  # the "lint" tool',
                "]  # end",
            ]
        )

        project = ensure_tools.read_minimal_toml(text)["project"]

        self.assertEqual("mu3-tools", project["name"])
        self.assertEqual("1.0#not-a-comment", project["version"])
        self.assertEqual(["typer>=0.12,<1.0"], project["dependencies"])
        self.assertEqual({"mu3-repo": "repo.cli:app"}, project["scripts"])
        self.assertNotIn("mu3-repo", project)

    def test_the_minimal_reader_reads_what_tomllib_reads_with_end_of_line_comments(self) -> None:
        try:
            import tomllib
        except ModuleNotFoundError:
            self.skipTest("tomllib comes with Python 3.11")
        text = "\n".join(
            [
                "[project]  # main",
                'name = "mu3-tools"',
                'version = "0.1.0"',
                "dependencies = [",
                '    "typer>=0.12,<1.0",  # the "cli" lib',
                "    'tomli; python_version < \"3.11\"',",
                "]  # end",
                "[project.scripts]",
                'mu3-repo = "repo.cli:app"  # repo',
            ]
        )

        expected = tomllib.loads(text)["project"]
        minimal = ensure_tools.read_minimal_toml(text)["project"]

        self.assertEqual(expected, minimal)

    def test_the_minimal_reader_refuses_an_array_that_does_not_end(self) -> None:
        with self.assertRaises(ValueError):
            ensure_tools.read_minimal_toml('[project]\ndependencies = [\n    "typer",\n')

    def test_read_project_uses_the_minimal_reader_without_tomllib(self) -> None:
        with mock.patch.dict(sys.modules, {"tomllib": None}):
            project = current_project()

        self.assertEqual("mu3-tools", project.name)
        self.assertIn("mu3-verify-build", project.scripts)


class NormalizeRequirementTests(unittest.TestCase):
    def test_specifier_order_and_spaces_do_not_matter(self) -> None:
        # setuptools writes the pyproject's "typer>=0.12,<1.0" as "typer<1.0,>=0.12".
        self.assertEqual(
            ensure_tools.normalize_requirement("typer>=0.12,<1.0"),
            ensure_tools.normalize_requirement("typer<1.0,>=0.12"),
        )
        self.assertEqual(
            ensure_tools.normalize_requirement("Typer >= 0.12, < 1.0"),
            ensure_tools.normalize_requirement("typer<1.0,>=0.12"),
        )

    def test_markers_compare_without_spaces_and_quote_style(self) -> None:
        self.assertEqual(
            ensure_tools.normalize_requirement("tomli; python_version < '3.11'"),
            ensure_tools.normalize_requirement('tomli ; python_version<"3.11"'),
        )

    def test_another_bound_differs(self) -> None:
        self.assertNotEqual(
            ensure_tools.normalize_requirement("typer>=0.12,<1.0"),
            ensure_tools.normalize_requirement("typer>=0.13,<1.0"),
        )


class CheckTests(unittest.TestCase):
    def check(self, *distributions, launcher=every_launcher):
        return ensure_tools.check(current_project(), TOOLS_DIR, SCRIPTS_DIR, distributions, launcher)

    def test_a_matching_install_has_no_problem(self) -> None:
        result = self.check(matching_install(current_project()))

        self.assertEqual([], result.problems)
        self.assertEqual([], result.former)
        self.assertFalse(result.reinstall)

    def test_a_venv_from_before_the_split_holds_only_mu3_cli(self) -> None:
        # The .venv of #504: mu3-cli installed in editable mode from this tools/cli, no mu3-tools.
        result = self.check(FakeDistribution("mu3-cli", scripts={"mu3-cli": "mu3_cli.cli:app"}))

        self.assertEqual(["mu3-cli"], result.former)
        self.assertTrue(result.reinstall)
        self.assertEqual(
            ["the former distribution mu3-cli is still installed", "mu3-tools is not installed"],
            result.problems,
        )

    def test_a_former_distribution_beside_a_matching_install_is_removed_without_reinstalling(self) -> None:
        result = self.check(
            matching_install(current_project()),
            FakeDistribution("mu3_CLI", scripts={"mu3-cli": "mu3_cli.cli:app"}),
        )

        self.assertEqual(["mu3-cli"], result.former)
        self.assertFalse(result.reinstall)

    def test_a_venv_from_before_a_command_was_added_lacks_it(self) -> None:
        project = current_project()
        older = matching_install(project)
        older.entry_points = [e for e in older.entry_points if e.name != "mu3-verify-build"]

        result = self.check(older)

        self.assertTrue(result.reinstall)
        self.assertEqual(["it lacks the command(s) mu3-verify-build"], result.problems)

    def test_a_command_the_pyproject_no_longer_lists_or_that_runs_another_function_differs(self) -> None:
        project = current_project()
        installed = matching_install(project)
        installed.entry_points = [
            EntryPoint(e.name, "elsewhere:app" if e.name == "mu3-repo" else e.value, e.group)
            for e in installed.entry_points
        ] + [EntryPoint("mu3-gone", "mu3_gone.cli:app", "console_scripts")]

        result = self.check(installed)

        self.assertTrue(result.reinstall)
        self.assertEqual(
            [
                "it has the command(s) mu3-gone, which tools/cli/pyproject.toml no longer lists",
                "the command(s) mu3-repo run another function than tools/cli/pyproject.toml gives",
            ],
            result.problems,
        )

    def test_a_command_without_a_launcher_differs(self) -> None:
        result = self.check(
            matching_install(current_project()),
            launcher=lambda name, folder: name != "mu3-unity",
        )

        self.assertTrue(result.reinstall)
        self.assertEqual([f"the command(s) mu3-unity have no launcher in {SCRIPTS_DIR}"], result.problems)

    def test_another_version_or_other_dependencies_differ(self) -> None:
        project = current_project()
        installed = matching_install(project)
        installed.metadata["Version"] = "0.0.9"
        installed.requires = ["typer<0.12,>=0.9"]

        result = self.check(installed)

        self.assertTrue(result.reinstall)
        self.assertEqual(2, len(result.problems), result.problems)
        self.assertIn("0.0.9 is installed", result.problems[0])
        self.assertIn("dependencies", result.problems[1])

    def test_optional_dependencies_in_the_metadata_are_not_dependencies(self) -> None:
        # Requires-Dist lists [project.optional-dependencies] too, each with a marker on its extra.
        installed = matching_install(current_project())
        installed.requires = [*installed.requires, 'ruff; extra == "lint"', "mypy ; python_version >= '3.11' and extra=='lint'"]

        self.assertEqual([], self.check(installed).problems)

    def test_a_version_written_otherwise_than_in_its_canonical_form_is_not_compared(self) -> None:
        # setuptools writes 0.2.0-beta as 0.2.0b0, which this script does not compute.
        project = current_project()
        installed = matching_install(project)
        installed.metadata["Version"] = "0.2.0b0"

        for version, problems in (
            ("0.2.0-beta", 0),
            ("0.2.0b0", 0),
            ("02.0", 0),
            ("0!2.0", 0),
            ("0.2.0b00", 0),
            ("0.2.0", 1),
            ("1!2.0rc1.post2.dev3", 1),
        ):
            with self.subTest(version=version):
                written = ensure_tools.Project(project.name, version, project.dependencies, project.scripts)
                result = ensure_tools.check(written, TOOLS_DIR, SCRIPTS_DIR, [installed], every_launcher)
                self.assertEqual(problems, len(result.problems), result.problems)

    def test_an_install_that_is_not_editable_or_comes_from_another_checkout_differs(self) -> None:
        project = current_project()
        with tempfile.TemporaryDirectory() as other:
            for installed, expected in (
                (FakeDistribution(project.name, source=None), "not installed in editable mode"),
                (FakeDistribution(project.name, editable=False), "not installed in editable mode"),
                (FakeDistribution(project.name, source=Path(other)), "is installed from"),
            ):
                installed.requires = list(project.dependencies)
                installed.entry_points = matching_install(project).entry_points
                with self.subTest(expected=expected):
                    result = self.check(installed)
                    self.assertTrue(result.reinstall)
                    self.assertEqual(1, len(result.problems), result.problems)
                    self.assertIn(expected, result.problems[0])


class InstalledDistributionsTests(unittest.TestCase):
    def test_an_egg_info_in_a_source_folder_on_sys_path_is_not_installed(self) -> None:
        # An editable install puts its source folder on sys.path; the former mu3-cli's build left
        # src/mu3_cli.egg-info there, which read as still installed after it was removed.
        with tempfile.TemporaryDirectory() as site, tempfile.TemporaryDirectory() as source:
            installed = Path(site, "mu3_tools-0.1.0.dist-info")
            installed.mkdir()
            installed.joinpath("METADATA").write_text("Metadata-Version: 2.1\nName: mu3-tools\nVersion: 0.1.0\n", encoding="utf-8")
            left = Path(source, "mu3_cli.egg-info")
            left.mkdir()
            left.joinpath("PKG-INFO").write_text("Metadata-Version: 2.1\nName: mu3-cli\nVersion: 0.1.0\n", encoding="utf-8")

            with mock.patch.object(ensure_tools.sysconfig, "get_path", return_value=site), mock.patch.object(
                sys, "path", [*sys.path, source]
            ):
                names = sorted(d.metadata["Name"] for d in ensure_tools.installed_distributions())

        self.assertEqual(["mu3-tools"], names)


class InstallerCommandsTests(unittest.TestCase):
    def test_uv_is_preferred_since_a_venv_it_made_has_no_pip(self) -> None:
        commands = ensure_tools.installer_commands("py", TOOLS_DIR, ["mu3-cli"], True, "uv", True)

        self.assertEqual(
            [
                ["uv", "pip", "uninstall", "--python", "py", "--quiet", "mu3-cli"],
                ["uv", "pip", "install", "--python", "py", "--quiet", "-e", str(TOOLS_DIR)],
            ],
            commands,
        )

    def test_pip_runs_isolated_without_uv(self) -> None:
        commands = ensure_tools.installer_commands("py", TOOLS_DIR, [], True, None, True)

        self.assertEqual([["py", "-I", "-m", "pip", "install", "--quiet", "--disable-pip-version-check", "-e", str(TOOLS_DIR)]], commands)

    def test_only_the_former_distribution_is_removed_when_the_install_matches(self) -> None:
        commands = ensure_tools.installer_commands("py", TOOLS_DIR, ["mu3-cli"], False, None, True)

        self.assertEqual([["py", "-I", "-m", "pip", "uninstall", "--yes", "--quiet", "--disable-pip-version-check", "mu3-cli"]], commands)

    def test_nothing_can_install_without_uv_or_pip(self) -> None:
        self.assertIsNone(ensure_tools.installer_commands("py", TOOLS_DIR, [], True, None, False))


class MainTests(unittest.TestCase):
    def run_main(self, before, after, *, runner_status=0, uv="uv", has_pip=False):
        states = iter([before, after])
        calls = []

        def runner(command):
            calls.append(list(command))
            return runner_status

        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            status = ensure_tools.main(
                [],
                distributions=lambda: next(states),
                runner=runner,
                launcher=every_launcher,
                find_uv=lambda: uv,
                find_pip=lambda: has_pip,
            )
        return status, calls, out.getvalue(), err.getvalue()

    def test_a_matching_install_runs_nothing_and_prints_nothing(self) -> None:
        installed = [matching_install(current_project())]

        status, calls, out, err = self.run_main(installed, installed)

        self.assertEqual((0, [], "", ""), (status, calls, out, err))

    def test_an_old_venv_is_repaired_and_checked_again(self) -> None:
        before = [FakeDistribution("mu3-cli", scripts={"mu3-cli": "mu3_cli.cli:app"})]
        after = [matching_install(current_project())]

        status, calls, out, err = self.run_main(before, after)

        self.assertEqual(0, status, err)
        self.assertEqual(["uninstall", "install"], [c[2] for c in calls])
        self.assertIn("the former distribution mu3-cli is still installed", out)
        self.assertIn("match tools/cli/pyproject.toml again", out)
        self.assertEqual("", err)

    def test_an_install_that_still_differs_fails_and_says_what_to_do(self) -> None:
        before = [FakeDistribution("mu3-cli")]

        status, calls, out, err = self.run_main(before, before)

        self.assertEqual(1, status)
        self.assertEqual(2, len(calls))
        self.assertIn("still: mu3-tools is not installed", err)
        self.assertIn("delete", err)

    def test_a_failed_install_command_fails(self) -> None:
        before = [FakeDistribution("mu3-cli")]
        after = [matching_install(current_project())]

        status, _, _, err = self.run_main(before, after, runner_status=1)

        self.assertEqual(1, status)
        self.assertIn("failed", err)

    def test_without_uv_or_pip_it_says_to_delete_the_venv(self) -> None:
        status, calls, _, err = self.run_main([], [], uv=None, has_pip=False)

        self.assertEqual((1, []), (status, calls))
        self.assertIn("Neither uv nor this .venv's pip", err)

    def test_an_unreadable_pyproject_fails_with_two(self) -> None:
        err = io.StringIO()
        with mock.patch.object(ensure_tools, "read_project", side_effect=ValueError("bad")), contextlib.redirect_stderr(err):
            status = ensure_tools.main([], distributions=lambda: [], launcher=every_launcher)

        self.assertEqual(2, status)
        self.assertIn("bad", err.getvalue())


if __name__ == "__main__":
    unittest.main()
