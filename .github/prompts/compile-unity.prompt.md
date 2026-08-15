---
description: "Run compile-only verification for Mu3Library and wait for completion before continuing to the next task."
name: "Compile Unity"
argument-hint: "Target project: builtin, urp, or both"
agent: "agent"
---

Run compile-only verification for the requested Mu3Library Unity project.

- Compile through the Unity Editor CLI with `./compile-unity.sh <target>`, or `mu3-cli unity compile` for the same entrypoint.
- Do not verify with `dotnet build`; it compiles the generated `.csproj` rather than what Unity builds.
- Do not add or run tests.
- Do not continue to any follow-up task until compile completion is known.
- Treat the compile result as evidence for `reviewer`, not as approval by itself.
- Summarize which targets were compiled, the Unity exit status with the error and warning counts from its log, and any residual risk.