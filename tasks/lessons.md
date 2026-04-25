## Lessons

- For large AI-agent framework work in this repository, execute one feature unit at a time, then run a suitability review before proceeding.
- If a new agent introduces role overlap or blurred ownership, pause expansion and rework the boundary before adding more agents.
- Prefer smaller governance agents over broad manager roles when the responsibility can be separated into orchestration, role audit, and domain execution.
- Keep one authoritative instruction per policy area. Companion docs may explain the policy, but final ownership and control terms must use the same actor names everywhere.
- Tooling convenience should discover policy from authoritative docs or instructions, not duplicate governance content into a second source of truth.
- When the user requests verification without tests, implement compile-only workflow assets and keep the next unit blocked until compile completion is explicitly known.
- Keep `tasks/todo.md` as a concise shared plan and review summary, not as a session transcript or temporary execution log.
- Keep hidden guidance comments in `tasks/todo.md` under `Task Plan` and `Review Summary`, and if future task-specific edits should stay local after the baseline commit, use `git update-index --skip-worktree tasks/todo.md` instead of `.gitignore` because the file remains tracked.
- For public concrete-type renames in package APIs, prefer moving the main implementation to the new name while leaving the old type as an `[Obsolete]` compatibility alias unless the user explicitly wants a breaking removal.
- Before expanding URP ScreenEffect features under a ban on deprecated compatibility APIs, confirm the sample project's RenderGraph mode first; if preview execution still depends on compatibility mode, align the project setting before adding more effects.
- When a Unity sample already treats the scene as the owner of handler wiring, UI hierarchy, and `UnityEvent` bindings, prefer direct YAML edits over runtime-generated workaround code, and keep imported sample scenes plus package sample source scenes synchronized.