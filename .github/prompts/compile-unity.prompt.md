---
description: "Run compile-only verification for Mu3Library and wait for completion before continuing to the next task."
name: "Compile Unity"
argument-hint: "Target project: builtin, urp, or both"
agent: "agent"
---

Run compile-only verification for the requested Mu3Library Unity project.

- Use editor-safe `dotnet build` on the affected generated Unity `.csproj` files.
- Do not add or run tests.
- Do not continue to any follow-up task until compile completion is known.
- Treat the compile result as evidence for `reviewer`, not as approval by itself.
- Summarize which `.csproj` files were built, the compile result, and any residual risk.