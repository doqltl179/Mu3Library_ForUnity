---
name: security-review-perform
description: "Review exact commits or an exact diff through the security lens: map the trust boundaries it touches, threat-model each one, walk a vulnerability checklist, and report findings and an approve or rework disposition without exploiting live systems. Use when a change touches external input, authentication, authorization, secrets, cryptography, outbound calls, file paths, serialization, dependencies, or sensitive data."
category: review
volatility: evolving
reviewed: 2026-10-09
---

# Perform Security Review

## Use When
- A change touches a trust boundary: external input, authentication or authorization, secrets, cryptography, outbound requests, file system paths, serialization, dependencies, or personal data.
- The plan or the orchestrator assigns the security gate, or the user asks for a security review.

## Do Not Use When
- General correctness and maintainability review → `code-review-perform`.
- Upgrading a vulnerable dependency → `dependency-upgrade`.
- Remediating a confirmed vulnerability → the owner of the affected code.

## Inputs
- The commits or pull request under review, plus the base it targets.
- The intent (issue, plan, or spec), the deployment context (who can reach the code, with what privileges), and the checks the author reported.

## Steps
1. Fix the exact commits under review (`git log --oneline <base>..<head>`) and follow the rest of «Review Protocol» in [review.md](../../wiki/workflows/review.md).
2. List every trust boundary the diff touches: where data enters from a less-trusted party (users, network, files, other services, environment), where privilege changes, and where data leaves (responses, logs, third parties). Read the code on both sides of each boundary.
3. Threat-model each boundary with STRIDE prompts: can an attacker spoof an identity, tamper with data, repudiate an action, disclose information, deny service, or elevate privilege? Record each plausible path as a candidate finding.
4. Walk «Checklist» below against the diff and the candidate paths.
5. Confirm or discard each candidate by tracing the data flow in code, or by running a local, non-destructive check the project already has (`commands.test`, `commands.lint`, a configured dependency audit or static analyzer). Record each check per «Evidence Format» in [verification.md](../../wiki/workflows/verification.md).
6. Never exploit live systems: run nothing against deployed, shared, or third-party environments. Show impact through the code path and reasoning instead.
7. When the diff contains a secret, report its location with the value redacted per [integrity.md](../../wiki/principles/integrity.md), and list rotation as a decision the user owns.
8. Rate each finding per «Severity», write the report per «Output Format», and choose the disposition per «Dispositions», all in review.md.

## Checklist
- Input validation and injection: untrusted input is validated at the boundary, with size limits; queries, shell commands, templates, and markup use parameterization or context-correct escaping.
- Authentication: every new entry point requires the intended authentication; tokens and sessions are verified, expire, and are invalidated on logout or rotation.
- Authorization: every object access checks that the caller may access that object; checks run server-side and deny by default.
- Secrets: no credentials, keys, or tokens in code, config, tests, fixtures, or logs; secrets come from the environment or a secret store.
- Cryptography: vetted libraries only; no custom algorithms, broken primitives (MD5 or SHA-1 for security, ECB, DES), static IVs or salts, or non-cryptographic randomness for security values; passwords use a password hashing function; TLS verification stays on.
- Deserialization: untrusted data never reaches a deserializer that constructs arbitrary objects; parsed data is schema-validated.
- SSRF and path traversal: outbound destinations derived from input are allow-listed; file paths from input are normalized and confined to an allowed root.
- Dependencies and supply chain: each new dependency meets «Dependencies» in [code-changes.md](../../wiki/workflows/code-changes.md), comes from the expected registry, and has no known advisories; build and CI steps do not fetch and run unpinned code.
- Sensitive data in logs: no secrets, tokens, passwords, or personal data in logs, metrics, traces, or analytics events.
- Error leakage: errors returned to callers omit stack traces, internal paths, queries, and versions; failures fail closed.

## Output
- The report per «Output Format»: trust boundaries reviewed, findings with their attack path, the disposition, and the reviewed commit IDs.
