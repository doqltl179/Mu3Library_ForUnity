---
id: terraform
title: Terraform / OpenTofu
kind: infra
applies_to: ["**/*.tf", "**/*.tfvars", "**/*.tofu", "**/*.tftest.hcl", "**/.terraform.lock.hcl"]
related: [kubernetes, docker, github-actions]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://developer.hashicorp.com/terraform/docs", "https://developer.hashicorp.com/terraform/language", "https://developer.hashicorp.com/terraform/cli/commands", "https://github.com/hashicorp/terraform/releases", "https://opentofu.org/docs/", "https://github.com/opentofu/opentofu/releases"]
---

# Terraform / OpenTofu

## Detect
- Binary: `terraform` or `tofu`, from `commands.*`, CI workflows, version files (`.terraform-version`, `.opentofu-version`, `.tool-versions`, `mise.toml`), or `*.tofu` files (OpenTofu only). Use one binary per state; never mix them.
- Version constraints: `required_version` and `required_providers` (`source`, `version`) in the `terraform {}` block; exact provider versions and hashes in `.terraform.lock.hcl`.
- State: the `backend` or `cloud` block (remote backend, locking, workspaces) and how environments are split (directories, workspaces, or per-environment `*.tfvars`).
- Orchestration that owns plan and apply, for example Terragrunt (`terragrunt.hcl`), Atlantis (`atlantis.yaml`), or HCP Terraform; follow its workflow.
- Modules: local `modules/`, registry sources with `version`, git sources with `?ref=`.
- Linter config, for example `.tflint.hcl`.

## Conventions
- Pin `required_version` and every provider with a version constraint; commit `.terraform.lock.hcl`; pin module versions (registry `version`, git `?ref=<tag>`).
- Keep state in a remote backend with locking; never commit `*.tfstate*`, `.terraform/`, or tfvars files containing secrets.
- Pass secrets through the environment (`TF_VAR_<name>`) or secret-manager data sources and mark variables and outputs `sensitive = true`. State still stores values in plaintext, so state access must stay restricted.
- Refactor without recreating: move addresses with `moved {}` blocks, adopt existing infrastructure with `import {}` blocks, and stop managing a resource without destroying it with `removed {}` blocks.
- Prefer `for_each` keyed by stable names over `count` for collections; removing a `count` element shifts indexes and recreates resources.
- Give variables `type` and `description`, plus `validation` for constrained inputs; give outputs `description`.
- Add `lifecycle { prevent_destroy = true }` to stateful resources (databases, storage holding data).
- Look up existing infrastructure with data sources instead of hard-coding IDs.
- Use provisioners (`local-exec`, `remote-exec`) only when no provider resource exists.

## Verify
1. `terraform fmt -check -recursive`
2. `terraform init -backend=false`, then `terraform validate` (no state or credentials needed).
3. `tflint` when configured; `terraform test` when `*.tftest.hcl` files exist.
4. `terraform plan -out=<file>` when credentials and backend access are available. Report every destroy, every replacement (`-/+`, "forces replacement"), and every change outside the intended scope.
5. Never run `apply`, `destroy`, CLI `import`, `state mv|rm|push`, `force-unlock`, or `init -migrate-state` without explicit user confirmation.
- Substitute `tofu` for `terraform` in every command when the project uses OpenTofu.

## Pitfalls
- Renaming a resource or module, or switching `count` to `for_each`, plans destroy and create; add `moved {}` blocks.
- Routine `-target` use leaves partial applies and drift; reserve it for recovery.
- `init -upgrade` rewrites `.terraform.lock.hcl` to newer provider versions; run it only when upgrading providers.
- Broad `ignore_changes` hides drift; limit it to attributes changed outside Terraform by design.
- Credentials in `provider` blocks or committed tfvars; use environment variables or the provider's credential chain.
- `plan -refresh=false` hides drift; use it only when deliberately skipping refresh.
- Interpolation-only strings (`"${var.name}"`); write `var.name`.

## Version Notes
- `moved` blocks need Terraform 1.1+, `import` blocks 1.5+, `terraform test` 1.6+, `removed` blocks 1.7+ (as of 2026-10, per Terraform docs). Check OpenTofu's docs for its own version of each feature.
- Ephemeral resources (1.10+) and write-only arguments (1.11+) keep secret values out of Terraform state; check the project's version and provider support first (as of 2026-10, per Terraform release notes).
- OpenTofu forked from Terraform 1.5 and features diverge after that; OpenTofu 1.8+ reads `*.tofu` files, which take precedence over `.tf` files of the same name (as of 2026-10, per OpenTofu docs).
