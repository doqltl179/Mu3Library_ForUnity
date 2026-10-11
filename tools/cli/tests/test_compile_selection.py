from __future__ import annotations

import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

from mu3_cli.repository import repo_root
from mu3_cli.unity import compile_script_command, compile_script_path

# Mirrors the shape of this repository: one package every project consumes, one
# layered on it, one leaf, a package whose name only extends another one's, and a
# project whose manifest does not reference the package the mapping pairs it with.
MAPPING = (
    ("shared", "PkgShared", "ProjShared"),
    ("layered", "PkgLayered", "ProjLayered"),
    ("leaf", "PkgLeaf", "ProjLeaf"),
    ("extra", "PkgSharedExtra", "ProjExtra"),
    ("orphan", "PkgOrphan", "ProjOrphan"),
)
PROJECT_PACKAGES = {
    "ProjShared": ("PkgShared",),
    "ProjLayered": ("PkgShared", "PkgLayered"),
    "ProjLeaf": ("PkgShared", "PkgLayered", "PkgLeaf"),
    "ProjExtra": ("PkgSharedExtra",),
    "ProjOrphan": (),
}


def selected_targets(output: str) -> list[str]:
    targets: list[str] = []
    in_selection = False
    for line in output.splitlines():
        if line.startswith("Selected compile targets:"):
            in_selection = True
        elif in_selection and line.startswith("  - "):
            targets.append(line[4:].split(":", 1)[0])
    return targets


class ChangedSelectionTests(unittest.TestCase):
    """Runs the real compile-unity.sh with --dry-run against a throwaway repository."""

    root: Path
    git_config: Path
    repository_environment: frozenset[str]
    script_name: str

    @classmethod
    def setUpClass(cls) -> None:
        source_script = compile_script_path(repo_root())
        try:
            compile_script_command(source_script, [])
        except ValueError as error:
            raise unittest.SkipTest(str(error)) from error

        temporary_directory = tempfile.TemporaryDirectory()
        cls.addClassCleanup(temporary_directory.cleanup)
        cls.root = Path(temporary_directory.name) / "repository"
        cls.root.mkdir()
        # Git may treat the system temp directory as owned by someone else.
        cls.git_config = Path(temporary_directory.name) / "gitconfig"
        cls.git_config.write_text("[safe]\n\tdirectory = *\n", encoding="utf-8")
        # Git's own list of the variables that tie a command to one repository. Git exports
        # them to hooks, where they would point the fixture's commands at the repository
        # the tests were started from.
        cls.repository_environment = frozenset(
            subprocess.run(
                ["git", "rev-parse", "--local-env-vars"], capture_output=True, text=True, check=True
            ).stdout.split()
        )
        cls.script_name = source_script.name

        shutil.copy2(source_script, cls.root / cls.script_name)
        (cls.root / "unity-cli-packages.tsv").write_text(
            "".join(f"{key}\t{package}\t{project}\n" for key, package, project in MAPPING),
            encoding="utf-8",
            newline="\n",
        )
        for _, package, project in MAPPING:
            runtime = cls.root / package / "Runtime"
            runtime.mkdir(parents=True)
            (runtime / "Code.cs").write_text("// fixture\n", encoding="utf-8")

            manifest = {
                "dependencies": {
                    f"fixture.{consumed.lower()}": f"file:../../{consumed}" for consumed in PROJECT_PACKAGES[project]
                }
            }
            packages = cls.root / project / "Packages"
            packages.mkdir(parents=True)
            (packages / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

        cls._git("init", "--quiet")
        cls._git("add", "--all")
        cls._git("-c", "commit.gpgsign=false", "commit", "--quiet", "--message", "fixture baseline")

    @classmethod
    def _environment(cls) -> dict[str, str]:
        # The user's and the machine's Git configuration stay out as well, so their hooks,
        # file-system monitors, and ignore rules do not apply to the fixture.
        environment = {key: value for key, value in os.environ.items() if key not in cls.repository_environment}
        environment.update(
            {
                "GIT_CONFIG_GLOBAL": str(cls.git_config),
                "GIT_CONFIG_NOSYSTEM": "1",
                "GIT_AUTHOR_NAME": "fixture",
                "GIT_AUTHOR_EMAIL": "fixture@example.invalid",
                "GIT_COMMITTER_NAME": "fixture",
                "GIT_COMMITTER_EMAIL": "fixture@example.invalid",
            }
        )
        return environment

    @classmethod
    def _git(cls, *arguments: str) -> None:
        completed = subprocess.run(
            ["git", *arguments],
            cwd=cls.root,
            env=cls._environment(),
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        if completed.returncode != 0:
            raise RuntimeError(f"git {' '.join(arguments)} failed in the fixture: {completed.stderr.strip()}")

    def dry_run_after_adding(self, relative_path: str) -> str:
        added = self.root / relative_path
        added.parent.mkdir(parents=True, exist_ok=True)
        added.write_text("// change under test\n", encoding="utf-8")
        self.addCleanup(added.unlink)

        command = compile_script_command(self.root / self.script_name, ["changed", "--dry-run"])
        completed = subprocess.run(
            command,
            cwd=self.root,
            env=self._environment(),
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr)
        return completed.stdout

    def test_shared_package_change_selects_every_consuming_project(self) -> None:
        output = self.dry_run_after_adding("PkgShared/Runtime/Added.cs")

        self.assertEqual(["shared", "layered", "leaf"], selected_targets(output))
        self.assertIn("  - PkgShared/Runtime/Added.cs -> shared, layered, leaf", output.splitlines())

    def test_layered_package_change_skips_projects_without_it(self) -> None:
        output = self.dry_run_after_adding("PkgLayered/Runtime/Added.cs")

        self.assertEqual(["layered", "leaf"], selected_targets(output))

    def test_leaf_package_change_selects_its_own_project(self) -> None:
        output = self.dry_run_after_adding("PkgLeaf/Runtime/Added.cs")

        self.assertEqual(["leaf"], selected_targets(output))

    def test_package_reference_must_match_the_whole_path(self) -> None:
        # ProjExtra references only PkgSharedExtra, whose path merely starts with PkgShared.
        output = self.dry_run_after_adding("PkgShared/Runtime/Added.cs")

        self.assertNotIn("extra", selected_targets(output))

    def test_package_with_longer_name_selects_its_own_project(self) -> None:
        output = self.dry_run_after_adding("PkgSharedExtra/Runtime/Added.cs")

        self.assertEqual(["extra"], selected_targets(output))

    def test_mapped_project_is_selected_without_a_manifest_reference(self) -> None:
        output = self.dry_run_after_adding("PkgOrphan/Runtime/Added.cs")

        self.assertEqual(["orphan"], selected_targets(output))

    def test_source_outside_packages_and_projects_selects_every_target(self) -> None:
        output = self.dry_run_after_adding("Loose.cs")

        self.assertEqual([key for key, _, _ in MAPPING], selected_targets(output))
        self.assertIn("  - Loose.cs -> all", output.splitlines())

    def test_project_change_selects_only_that_project(self) -> None:
        output = self.dry_run_after_adding("ProjLayered/Assets/Added.cs")

        self.assertEqual(["layered"], selected_targets(output))

    def test_package_documentation_change_selects_nothing(self) -> None:
        output = self.dry_run_after_adding("PkgShared/README.md")

        self.assertEqual([], selected_targets(output))
        self.assertIn("Selected compile targets: none", output.splitlines())


if __name__ == "__main__":
    unittest.main()
