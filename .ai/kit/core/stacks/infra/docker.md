---
id: docker
title: Docker
kind: infra
applies_to: ["**/Dockerfile*", "**/*.dockerfile", "**/.dockerignore", "**/compose*.y*ml", "**/docker-compose*.y*ml", "**/docker-bake.hcl"]
related: [kubernetes, github-actions, shell]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://docs.docker.com/build/building/best-practices/", "https://docs.docker.com/reference/dockerfile/", "https://docs.docker.com/build/building/secrets/", "https://docs.docker.com/build/checks/", "https://docs.docker.com/reference/compose-file/", "https://docs.docker.com/engine/release-notes/"]
---

# Docker

## Detect
- Dockerfiles (`Dockerfile*`, `*.dockerfile`), their stages (`FROM ... AS <name>`), and the `# syntax=docker/dockerfile:1` parser directive.
- `.dockerignore` at the build context root.
- Compose files: `compose.yaml` (canonical name) or `docker-compose.yml`, override files (`compose.override.yaml`), `profiles`, and `.env` interpolation.
- How images are built and tagged: `commands.build`, `docker-bake.hcl`, CI workflow steps, registry names.
- Base image update tooling (for example Dependabot or Renovate) and the pin format it expects.

## Conventions
- Use multi-stage builds: compile in a builder stage and `COPY --from=<stage>` only runtime artifacts into a minimal final image (for example `-slim` or distroless).
- Pin base images to an exact version tag, plus `@sha256:<digest>` when builds must be reproducible; never `latest`.
- Order layers by change frequency: copy dependency manifests and install dependencies before copying source, so code edits reuse the dependency layer.
- Install OS packages in one `RUN`: `apt-get update && apt-get install -y --no-install-recommends <pkgs> && rm -rf /var/lib/apt/lists/*`.
- Speed up package installs with BuildKit cache mounts (`RUN --mount=type=cache,target=<cache-dir> ...`).
- Pass build-time secrets with `RUN --mount=type=secret,id=<id>` and `docker build --secret id=<id>,src=<file>`; never through `ARG`, `ENV`, or `COPY`, which persist in image layers or metadata.
- Run as non-root: create a user, `COPY --chown`, and set `USER <uid>`; a numeric UID lets orchestrators verify non-root.
- Use exec form for `CMD`/`ENTRYPOINT` (`["app", "--flag"]`) so the process is PID 1 and receives signals; end entrypoint scripts with `exec "$@"`.
- Run one main process per container and log to stdout/stderr.
- Define `HEALTHCHECK` (or compose `healthcheck`) and gate dependents with `depends_on: {<svc>: {condition: service_healthy}}`.
- Prefer `COPY` over `ADD`, except to extract a local tar archive or fetch a remote file with a checksum.
- Keep `.dockerignore` excluding `.git`, dependency and build output folders, `.env`, and credential files.

## Verify
- Prefer `commands.build`; otherwise `docker build --target <stage> -t <name>:dev .`.
- Lint: `docker build --check .` (build checks without building), and `hadolint <Dockerfile>` when installed.
- Compose: `docker compose config` validates and prints the merged configuration.
- Smoke-run with `docker run --rm <image>`; read health with `docker inspect --format '{{.State.Health.Status}}' <container>`.
- Confirm the runtime user with `docker inspect --format '{{.Config.User}}' <image>`.
- Scan when a scanner is available: `docker scout cves <image>` or `trivy image <image>`.

## Pitfalls
- `COPY . .` before installing dependencies invalidates the cache on every code change.
- `apt-get update` in its own `RUN` caches a stale package index; combine it with `install`.
- Secrets in `ARG`/`ENV` appear in `docker history` and `docker inspect`; files copied in and deleted later remain in earlier layers.
- Shell-form `CMD` runs under `/bin/sh -c`, which does not forward `SIGTERM`; the container is killed after the stop timeout.
- `localhost` inside a container is the container itself; reach other compose services by service name.
- Alpine uses musl: prebuilt glibc binaries and some native packages fail or compile from source. Test before switching base images.
- Bind mounts in development compose files hide files the image placed at the same path (for example installed dependencies).
- `EXPOSE` only documents a port; `ports:` or `-p` publishes it.
- Kubernetes ignores Dockerfile `HEALTHCHECK`; define probes in the manifests.

## Version Notes
- Compose's top-level `version:` element is obsolete; omit it (as of 2026-10, per Compose file reference).
- Use `docker compose` (Compose V2); standalone `docker-compose` V1 is end of life (as of 2026-10, per Docker docs).
- BuildKit is the default builder from Docker Engine 23.0 (as of 2026-10, per Docker Engine release notes).
- Build checks (`docker build --check`) need Buildx 0.15.0 or later and Dockerfile frontend 1.8 or later (as of 2026-10, per Docker build checks docs).
