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

Use a Git base when selecting committed changes in CI, or keep the explicit targets for manual verification:

```bash
./compile-unity.sh changed --base origin/develop --dry-run
./compile-unity.sh built-in
./compile-unity.sh urp
./compile-unity.sh watermelon
./compile-unity.sh all
```

Add future package-to-project mappings to `unity-cli-packages.tsv`; the shell entrypoint should not need a new selection branch for each package.
