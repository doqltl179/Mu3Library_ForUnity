---
id: github-actions
title: GitHub Actions
kind: infra
applies_to: [".github/workflows/*.y*ml", ".github/actions/**"]
related: [docker, terraform, shell]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.github.com/en/actions", "https://docs.github.com/en/actions/reference/security/secure-use", "https://securitylab.github.com/resources/github-actions-preventing-pwn-requests/", "https://github.blog/changelog/", "https://github.com/rhysd/actionlint"]
---

# GitHub Actions

## Detect
- Workflows in `.github/workflows/`, their triggers (`on:`), and reusable workflows (`on: workflow_call`); local actions in `.github/actions/<name>/action.yml`.
- Required status checks match job names; renaming a job or workflow can block merges until branch protection is updated.
- Runner labels in `runs-on` (GitHub-hosted such as `ubuntu-24.04`, or self-hosted labels) and steps that depend on the runner OS.
- Existing `uses:` pin style (full SHA with version comment, or tags) and update tooling (`.github/dependabot.yml` with `package-ecosystem: github-actions`, or Renovate).
- Referenced `secrets.*`, `vars.*`, and `environment:` names; their values live in repository or organization settings, not in the repo.

## Conventions
- Declare `permissions:` at workflow level with the minimum (for example `contents: read`) and widen per job only where needed; the default `GITHUB_TOKEN` scope depends on repository settings.
- Pin third-party actions to a full 40-character commit SHA with a version comment (`uses: owner/action@<sha> # v1.2.3`); a SHA is the only immutable reference. First-party `actions/*` may use a major tag when the project does.
- Never interpolate untrusted context (`github.event.pull_request.title`, `github.head_ref`, issue or comment bodies) into `run:`; pass it through `env:` and quote it (`"$TITLE"`).
- Reference secrets only in `env:` or `with:` and never print them; mask derived sensitive values with `echo "::add-mask::$VALUE"`.
- Authenticate to cloud providers with OIDC (`permissions: id-token: write`) instead of long-lived keys.
- `pull_request_target` and `workflow_run` run with base-repository secrets and a write-capable token: never check out or execute pull request code in them.
- Cancel superseded runs with `concurrency` (`group: ${{ github.workflow }}-${{ github.ref }}`, `cancel-in-progress: true`); never cancel in-progress deployments.
- Cache with the `cache` input of `actions/setup-*` or with `actions/cache` keyed on lockfile hashes (`hashFiles('**/<lockfile>')`).
- Set `timeout-minutes` on jobs; the default is 360.
- Fan out versions and OSes with `strategy.matrix`; set `fail-fast` deliberately.
- Share logic through reusable workflows (`workflow_call` with typed `inputs` and declared `secrets`) or composite actions instead of copying steps.
- Write step outputs to `$GITHUB_OUTPUT` and environment to `$GITHUB_ENV`; `::set-output` is deprecated.
- Pin a versioned runner label (`ubuntu-24.04`) when the toolchain depends on the OS version; `*-latest` labels move to new images.

## Verify
- `actionlint` checks syntax, expressions, and embedded shell (with `shellcheck` when installed); run `zizmor` for security findings when available.
- `act` runs workflows locally with limited fidelity (runner images, services, and OIDC differ); confirm with a real run.
- Inspect real runs with `gh run list` and `gh run view <id> --log-failed`. Triggering runs (`gh workflow run`) has side effects such as deploys; get confirmation first.

## Pitfalls
- A `run:` step without explicit `shell` on Linux uses `bash -e {0}` (no `pipefail`); set `shell: bash` to get `-eo pipefail`. Windows runners default to `pwsh`.
- Events created with `GITHUB_TOKEN` do not start new workflow runs (except `workflow_dispatch` and `repository_dispatch`); use a GitHub App token when chaining is required.
- A workflow skipped by path, branch, or commit-message filters leaves its required checks pending; make required jobs always report.
- `if:` without a status function implies `success()`; cleanup and reporting steps need `always()` or `failure()`.
- `schedule` runs only on the default branch, and scheduled workflows in public repositories are disabled after 60 days without repository activity.
- Secrets are not passed to `pull_request` runs from forks; skip jobs that need them explicitly instead of letting them fail.

## Version Notes
- `actions/upload-artifact` and `actions/download-artifact` v3 no longer work; use v4 or later, where uploading an existing artifact name in the same run fails unless overwriting is enabled (as of 2026-10, per GitHub changelog).
- JavaScript actions declare their Node.js runtime in `runs.using`; update local actions when GitHub deprecates a runtime, which run annotations announce (as of 2026-10, per GitHub changelog).
