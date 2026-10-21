# Tools

Repository-local support tooling lives here. These tools can help agents inspect or verify the repository, but they do not own Unity package behavior.

| Path | Owns |
|---|---|
| [cli/](cli/README.md) | Python CLI for repository, C# Dev Kit, and Unity automation workflows |
| [../compile-unity.sh](../compile-unity.sh) | Git-change-aware Unity batchmode compilation configured by `unity-cli-packages.tsv` |

Generated files under `bin/`, `obj/`, `.venv/`, and `*.egg-info/` are local artifacts and should not be tracked.

Run `mu3-cli repo check` before sharing repository-level documentation or agent-framework changes. It replaces the former remote repository-hygiene workflow with its useful checks at the local tooling boundary.

## Unity Compile Entrypoint

The default command compiles the development projects selected from staged, unstaged, and untracked Git changes:

```bash
./compile-unity.sh --dry-run
./compile-unity.sh
```

A changed package selects every configured project whose `Packages/manifest.json` references it, not only the project `unity-cli-packages.tsv` pairs it with. Each project installs different optional packages, so it compiles the shared sources with a different define set: a `Mu3Library_Base` change selects `built-in`, `urp`, and `watermelon`, and a `Mu3Library_URP` change selects `urp` and `watermelon`.

Package Markdown, licenses, `Documentation~`, and repository tooling changes do not trigger Unity compilation.

A target compiles in place while its project is closed, and in a temporary source-only mirror while an Editor has it open. A project counts as open while a process holds its `Temp/UnityLockfile`. An Editor that crashed leaves that file behind with no holder; the script reports it as stale and compiles in place, and `mu3-cli unity doctor` reports it apart from an open project. PowerShell checks the holder on Windows and `lsof` does elsewhere; on a host with neither, an existing lock file counts as open. `--dry-run` prints where each selected target would run.

Use a Git base when selecting committed changes in CI, or keep the explicit targets for manual verification:

```bash
./compile-unity.sh changed --base origin/develop --dry-run
./compile-unity.sh built-in
./compile-unity.sh urp
./compile-unity.sh watermelon
./compile-unity.sh all
```

Add future package-to-project mappings to `unity-cli-packages.tsv`; the shell entrypoint should not need a new selection branch for each package.

## Environment

The shell entrypoint and `mu3-cli unity doctor` read the same variables, so the Editor the doctor accepts is the one the script runs.

| Variable | Effect |
|---|---|
| `UNITY_EDITOR` or `UNITY` | Path to a Unity executable (or `Unity.app`) used for every target instead of the version in `ProjectVersion.txt`. |
| `UNITY_HUB_EDITOR_ROOT` | Folder that holds one folder per installed Editor version, for a Unity Hub set to install Editors outside its default location (`C:\Program Files\Unity\Hub\Editor`, `/Applications/Unity/Hub/Editor`, or `~/Unity/Hub/Editor`). |
| `TMPDIR` | Where the script creates isolated mirrors. Keep it short on Windows, where a long path pushes Unity's package cache past the path limit. |

On Windows the script runs in the bash of Git for Windows. `mu3-cli` finds it even when only `git` is on `PATH`; see [cli/README.md](cli/README.md#bash-on-windows).
