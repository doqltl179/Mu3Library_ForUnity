---
id: kubernetes
title: Kubernetes
kind: infra
applies_to: ["**/k8s/**/*.y*ml", "**/charts/**", "**/Chart.yaml", "**/kustomization.y*ml"]
related: [docker, terraform, github-actions]
volatility: volatile
reviewed: 2026-10-09
sources: ["https://kubernetes.io/docs/home/", "https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/", "https://kubernetes.io/docs/concepts/security/pod-security-standards/", "https://kubernetes.io/docs/reference/using-api/deprecation-guide/", "https://kubernetes.io/releases/", "https://kubernetes.io/blog/2026/04/22/kubernetes-v1-36-release/", "https://kubernetes.io/blog/2026/08/26/kubernetes-v1-37-release/", "https://helm.sh/docs/", "https://kubectl.docs.kubernetes.io/references/kustomize/"]
---

# Kubernetes

## Detect
- Packaging: plain manifests, Helm (`Chart.yaml`, `values*.yaml`, `templates/`), or Kustomize (`kustomization.yaml`, often `base/` and `overlays/`). Edit the source the project renders from, never rendered output.
- GitOps controllers (for example Argo CD `Application` or Flux `Kustomization` resources): changes deploy by merging to Git, and live edits are reverted.
- Target cluster versions: `kubeVersion` in `Chart.yaml`, CI config, or `kubectl version` (read-only). Every API version used must exist in every target cluster.
- Current context and namespace: `kubectl config current-context`; confirm both before any command that talks to a cluster.
- Policy and validation: Pod Security Admission namespace labels, admission policy engines (for example Kyverno or Gatekeeper), schema tools (for example kubeconform).
- Secret management: Sealed Secrets, SOPS, External Secrets, or a cloud secret store.

## Conventions
- Set CPU and memory `requests` on every container and a memory `limit`; follow the project's policy on CPU limits.
- `readinessProbe` gates traffic; `livenessProbe` restarts the container and must check only the process itself, never downstream dependencies; use a `startupProbe` for slow starts instead of long liveness delays.
- Container `securityContext`: `runAsNonRoot: true`, `allowPrivilegeEscalation: false`, `readOnlyRootFilesystem: true`, `capabilities: {drop: ["ALL"]}`, `seccompProfile: {type: RuntimeDefault}` (the Pod Security `restricted` profile).
- Label with `app.kubernetes.io/name`, `instance`, `version`, `component`, `part-of`, `managed-by`; build selectors only from stable labels (`name`, `instance`).
- Use ConfigMaps for non-sensitive config and Secrets for credentials. Secret data is only base64-encoded: never commit plaintext Secret manifests; use the project's secret mechanism.
- Roll pods on config change: Kustomize `configMapGenerator`/`secretGenerator` hash suffixes, or a Helm checksum annotation on the pod template.
- Deployments: `RollingUpdate` with explicit `maxUnavailable`/`maxSurge`; a `PodDisruptionBudget` for multi-replica services; `terminationGracePeriodSeconds` longer than the app's graceful shutdown.
- Pin image tags or digests; never `latest`.
- Set the namespace explicitly (manifest `metadata.namespace`, Kustomize `namespace`, or Helm `--namespace`).

## Verify
- Prefer `commands.lint` and `commands.test`.
- Render: `helm lint <chart>`, `helm template <release> <chart> -f <values>`, `kubectl kustomize <dir>` (or `kustomize build <dir>`).
- Validate rendered output offline with the project's schema tool, for example `kubeconform -strict -summary`.
- Against a cluster, after confirming context: `kubectl diff -f <file>` (or `-k <dir>`) and `kubectl apply --dry-run=server -f <file>`; both run server-side validation without persisting.
- Never run `kubectl apply|create|delete|edit|patch|scale|rollout restart` or `helm install|upgrade|uninstall|rollback` against a cluster without explicit user confirmation.

## Pitfalls
- `kubectl` acts on the current context and namespace; pass `--context` and `-n` explicitly.
- Changing immutable fields (Deployment `spec.selector`, Job `spec.template`, StatefulSet `volumeClaimTemplates`) fails on apply; it needs delete and recreate, which needs confirmation.
- YAML typing: quote values that parse as booleans or numbers (`"yes"`, `"on"`, `"0123"`); env var values must be strings.
- Liveness probes that are too strict cause restart loops under load.
- Pods without requests are `BestEffort` and are evicted first.
- APIs removed in newer versions fail to apply; check the deprecation guide and `kubectl api-resources`.
- `kubectl edit` and live patches drift from Git and are overwritten by the next deploy.
- Helm: `toYaml` output needs `nindent` matching the surrounding indentation; wrong indentation can render valid but wrong YAML.

## Version Notes
- Kubernetes maintains release branches for the three most recent minor versions: 1.37 (latest), 1.36, and 1.35; 1.34 reaches end of life on 2026-10-27. Check target cluster versions before using new fields (as of 2026-10, per kubernetes.io/releases).
- From 1.36, `gitRepo` volumes are permanently disabled (use an init container or a git-sync style tool), and Service `.spec.externalIPs` is deprecated with removal planned for 1.43 (use `LoadBalancer`, `NodePort`, or Gateway API) (as of 2026-10, per kubernetes.io/blog v1.36 release).
- In 1.37, HorizontalPodAutoscaler scale to zero (`minReplicas: 0`) is beta and on by default, for `Object` or `External` metrics only; `metrics.k8s.io/v1` and `storagemigration.k8s.io/v1` `StorageVersionMigration` are stable; static Pods can no longer reference Secrets or ConfigMaps (as of 2026-10, per kubernetes.io/blog v1.37 release).
- In 1.37, kube-proxy `ipvs` mode is deprecated (default-off planned for 1.40); since 1.35 the kubelet refuses to start on cgroup v1 nodes unless `failCgroupV1: false`. Clients and controllers must handle HTTP 429 with `Retry-After` and backoff (as of 2026-10, per kubernetes.io/blog v1.37 release).
- PodSecurityPolicy was removed in 1.25; use Pod Security Admission or a policy engine (as of 2026-10, per Kubernetes deprecation guide).
- Native sidecars (`initContainers` with `restartPolicy: Always`) are enabled by default from 1.29 and stable from 1.33; check the cluster version first (as of 2026-10, per Kubernetes release notes).
