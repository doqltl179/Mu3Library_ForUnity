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
# layered on it, one leaf, and a package whose name only extends another one's.
MAPPING = (
    ("shared", "PkgShared", "ProjShared"),
    ("layered", "PkgLayered", "ProjLayered"),
    ("leaf", "PkgLeaf", "ProjLeaf"),
    ("extra", "PkgSharedExtra", "ProjExtra"),
)
PROJECT_PACKAGES = {
    "ProjShared": ("PkgShared",),
    "ProjLayered": ("PkgShared", "PkgLayered"),
    "ProjLeaf": ("PkgShared", "PkgLayered", "PkgLeaf"),
    "ProjExtra": ("PkgSharedExtra",),
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
        cls.root = Path(temporary_directory.name)
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
    def _git(cls, *arguments: str) -> None:
        identity = {
            "GIT_AUTHOR_NAME": "fixture",
            "GIT_AUTHOR_EMAIL": "fixture@example.invalid",
            "GIT_COMMITTER_NAME": "fixture",
            "GIT_COMMITTER_EMAIL": "fixture@example.invalid",
        }
        subprocess.run(
            ["git", *arguments],
            cwd=cls.root,
            env={**os.environ, **identity},
            check=True,
            capture_output=True,
        )

    def dry_run_after_adding(self, relative_path: str) -> str:
        added = self.root / relative_path
        added.parent.mkdir(parents=True, exist_ok=True)
        added.write_text("// change under test\n", encoding="utf-8")
        self.addCleanup(added.unlink)

        command = compile_script_command(self.root / self.script_name, ["changed", "--dry-run"])
        completed = subprocess.run(
            command, cwd=self.root, capture_output=True, text=True, encoding="utf-8", check=False
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

    def test_project_change_selects_only_that_project(self) -> None:
        output = self.dry_run_after_adding("ProjLayered/Assets/Added.cs")

        self.assertEqual(["layered"], selected_targets(output))

    def test_package_documentation_change_selects_nothing(self) -> None:
        output = self.dry_run_after_adding("PkgShared/README.md")

        self.assertEqual([], selected_targets(output))
        self.assertIn("Selected compile targets: none", output.splitlines())


if __name__ == "__main__":
    unittest.main()
