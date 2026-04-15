## Lessons

- For large AI-agent framework work in this repository, execute one feature unit at a time, then run a suitability review before proceeding.
- If a new agent introduces role overlap or blurred ownership, pause expansion and rework the boundary before adding more agents.
- Prefer smaller governance agents over broad manager roles when the responsibility can be separated into orchestration, role audit, and domain execution.
- Keep one authoritative instruction per policy area. Companion docs may explain the policy, but final ownership and control terms must use the same actor names everywhere.
- Tooling convenience should discover policy from authoritative docs or instructions, not duplicate governance content into a second source of truth.
- When the user requests verification without tests, implement compile-only workflow assets and keep the next unit blocked until compile completion is explicitly known.
- Keep `tasks/todo.md` as a concise shared plan and review summary, not as a session transcript or temporary execution log.