<!-- agentkit:generated from core/agents/. Do not edit: change the source, then run `python tools/agentkit.py sync`. Paths are relative to the project root. -->

# Agent Catalog

Every core agent card. Projects activate a subset in `.ai/project/profile.toml`.

## governance (governance)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`orchestrator`](governance/orchestrator.md) | deep | read-only | Decomposes a request into bounded units, selects the owner for each, orders them as a dependency graph, and sequences review gates through closeout. Use when a request spans several owners or surfaces, or when the right owner is unclear; not for implementing, writing plans, or approving quality. |
| [`role-governor`](governance/role-governor.md) | deep | read-only | Judges the structural fitness of the agent, skill, and stack catalog and its routing: overlap, missing ownership, routing ambiguity, and boundary violations. Returns `continue` or `rework` on framework changes and approves new agents and sections. Use when a catalog or routing change is proposed or no owner fits a task; not for writing the docs, content quality review, or task decomposition. |
| [`task-planner`](governance/task-planner.md) | standard | read-write | Records and updates plan files and progress in `.ai/tasks/` for units whose scope and owner are already decided. Use when a decomposed request needs a durable plan, or a unit's status, blocker, decision, or verification result changes; not for choosing owners or dependency order, structural verdicts, or implementing. |

## product (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`requirements-analyst`](product/requirements-analyst.md) | standard | read-write | Turns ambiguous requests into specifications in `docs.specs_dir`: problem statement, users, scope and out-of-scope, acceptance criteria, non-functional requirements, and open questions. Use when a request's intent, scope, or success criteria are unclear or disputed; not for technical design, UI flows, or splitting work into units and owners. |
| [`software-architect`](product/software-architect.md) | deep | read-write | Decides component and service boundaries, cross-component interface contracts, technology selection, and non-functional design (scalability, consistency, failure modes), recorded as ADRs in `docs.adr_dir`. Use when a change crosses components, alters a shared contract, or adopts or retires a technology; not for implementing, design internal to one surface, product scope, or provisioning. |
| [`ux-designer`](product/ux-designer.md) | standard | read-write | Specifies user experience as documents: user flows, screen and state specifications, interaction rules, UI copy, and design-token specifications. Use when a feature needs flows, screens, states, or wording defined before or during UI work; not for implementing UI, accessibility verdicts, or defining product requirements. |

## client (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`android-engineer`](client/android-engineer.md) | standard | read-write | Implements native Android apps: UI, navigation, lifecycle, platform APIs, permissions, background work, and on-device storage. Use when the dominant change is native Android code, including native modules behind a cross-platform bridge; not for shared cross-platform code, backend services, or build pipelines. |
| [`cross-platform-app-engineer`](client/cross-platform-app-engineer.md) | standard | read-write | Implements apps built from one shared codebase across platforms (for example Flutter, React Native, Kotlin Multiplatform, .NET MAUI): shared UI, shared logic, and the shared side of platform bridges. Use when the dominant change is in shared cross-platform code; not for native modules where platform code dominates, backend services, or build pipelines. |
| [`desktop-app-engineer`](client/desktop-app-engineer.md) | standard | read-write | Implements desktop apps (for example Electron, Tauri, WinUI, AppKit, Qt): windows, menus, OS integration, IPC, the auto-update client, installer runtime hooks, and native modules behind cross-platform bridges. Use when the dominant change is desktop-shell, native desktop UI, or OS-integration code; not for web UI inside a webview, packaging pipelines, update channels, or backend services. |
| [`embedded-engineer`](client/embedded-engineer.md) | standard | read-write | Implements firmware for microcontrollers and embedded Linux: drivers, hardware abstraction layers, RTOS tasks and interrupts, hardware interfaces and protocols (I2C, SPI, UART, CAN), bootloaders, and the OTA update client, within memory and power budgets. Use when the dominant change runs as device firmware; not for companion mobile or desktop apps, or cloud backends. |
| [`game-runtime-engineer`](client/game-runtime-engineer.md) | standard | read-write | Implements gameplay and runtime systems inside a game engine (for example Unity, Unreal, Godot): game logic, entity and component systems, input, physics usage, in-game UI, save/load, and runtime asset loading. Use when the dominant change runs in the shipped game build; not for editor tooling or asset pipelines, rendering pipelines or shaders, or multiplayer netcode. |
| [`game-tools-engineer`](client/game-tools-engineer.md) | standard | read-write | Implements game-engine editor tooling (for example Unity, Unreal, Godot): editor extensions, inspectors and editor windows, asset import and processing pipelines, content build tooling, and level or data authoring tools. Use when the dominant change runs inside the engine editor or the content build; not for runtime gameplay, shaders, or CI pipeline definitions. |
| [`graphics-engineer`](client/graphics-engineer.md) | standard | read-write | Implements rendering technology on any engine or graphics API: render pipelines and passes, shaders, material system code, GPU resource management, visual-effects technology, and GPU frame-time optimization. Use when the dominant change is how frames are produced on the GPU; not for gameplay logic, in-game UI behavior, or cross-system CPU and memory performance. |
| [`ios-engineer`](client/ios-engineer.md) | standard | read-write | Implements native Apple-platform apps (iOS, iPadOS, watchOS, visionOS): views, navigation, platform APIs, app lifecycle, permissions, and on-device storage. Use when the dominant change is native Apple-platform code, including native modules behind a cross-platform bridge; not for shared cross-platform code, backend services, or build pipelines. |
| [`web-frontend-engineer`](client/web-frontend-engineer.md) | standard | read-write | Implements browser-facing UI, including web UI inside desktop or mobile webviews: components, views, styling, client state, routing, forms, and client-side data fetching. Use when the dominant change is in a web app's UI layer, including server components that only render UI; not for API endpoints, persistence, realtime connection layers, or infrastructure. |

## server (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`backend-api-engineer`](server/backend-api-engineer.md) | standard | read-write | Implements request/response services: endpoints, business logic, ORM models and data-access code, input validation, server-side auth integration, API schema files, background jobs, and queue consumers. Use when the dominant change is server code behind an API; not for physical schema, migrations, or query tuning, persistent-connection realtime layers, shared contract decisions, or infrastructure. |
| [`database-engineer`](server/database-engineer.md) | standard | read-write | Designs and changes physical database schemas, migrations, indexes, query tuning, integrity constraints, engine configuration as code, and backup, restore, and retention procedures. Use when the dominant change is the shape, integrity, performance, or safety of stored data; not for ORM models or business logic, analytics pipelines or warehouse models, or provisioning managed databases. |
| [`realtime-engineer`](server/realtime-engineer.md) | standard | read-write | Implements persistent-connection systems on both ends of the wire: WebSocket, SSE, and gRPC streams, pub/sub fan-out, presence, multiplayer game servers and netcode (prediction, reconciliation, authority), and state-sync protocols. Use when the dominant change is a long-lived connection or its sync protocol; not for request/response endpoints, gameplay rules, or UI that renders streamed data. |

## data-ai (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`ai-application-engineer`](data-ai/ai-application-engineer.md) | standard | read-write | Builds LLM-powered product features: prompts and templates, tool calling, retrieval (RAG) and indexing, agent loops, LLM evaluation suites, provider SDK integration, guardrails, and cost and latency budgets. Use when the change shapes how the product calls, grounds, or evaluates language models; not for training models, this kit's agent docs, or generic backend endpoints. |
| [`data-analyst`](data-ai/data-analyst.md) | standard | read-write | Answers questions from data: analytical queries, metric definitions, business tracking plans, reports, dashboards, and A/B experiment analysis, delivered as committed files and read-only against production data. Use when the deliverable is a number, chart, readout, metric, or tracking plan; not for building pipelines or tables, operational alerting, or adding instrumentation to product code. |
| [`data-engineer`](data-ai/data-engineer.md) | standard | read-write | Builds batch and streaming data pipelines: ingestion, ETL/ELT transforms, warehouse and lakehouse models, orchestration DAGs, and data quality checks and contracts on its own pipeline outputs. Use when the dominant change moves or reshapes data between systems for analytical or downstream use; not for OLTP schemas and migrations, analysis and reports, or model training. |
| [`ml-engineer`](data-ai/ml-engineer.md) | standard | read-write | Builds and ships machine learning models: training and fine-tuning code, evaluation sets and benchmarks, model-specific feature pipelines, model packaging, and serving and inference optimization. Use when the change trains, evaluates, packages, or serves a model; not for LLM product features on hosted models, generic data pipelines, or infrastructure provisioning. |

## platform (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`ci-cd-engineer`](platform/ci-cd-engineer.md) | standard | read-write | Maintains CI/CD pipeline definitions: build, test, and deploy jobs, container image builds, signing, installer, and store-upload jobs, artifact publishing, environment promotion, caching, and runners. Use when the change is to how code is automatically built, verified, published, or deployed; not for provisioning infrastructure, local developer tooling, test suite contents, or deciding what ships. |
| [`cloud-infrastructure-engineer`](platform/cloud-infrastructure-engineer.md) | standard | read-write | Provisions and changes infrastructure as code: cloud resources, networking, container image definitions and orchestration manifests, IAM and secrets-management infrastructure, and cost controls. Use when the change creates, modifies, or removes runtime infrastructure; not for application code, CI/CD pipeline definitions, or telemetry and alerting. |
| [`devtools-engineer`](platform/devtools-engineer.md) | standard | read-write | Maintains developer tooling inside the repository: local scripts and CLIs, engine-independent tooling, dev containers and toolchain pins, cross-compilation and flashing tools, linter, formatter, build-tool, and monorepo configuration, and git hooks. Use when the change is to how developers build, lint, flash, or run the project locally; not for CI pipelines, test frameworks, or product code. |
| [`observability-engineer`](platform/observability-engineer.md) | standard | read-write | Owns telemetry: logging, metrics, and tracing pipelines and conventions, operational dashboards, alerts, SLOs, runbooks, and incident-review artifacts. Use when the change is to how systems are observed, alerted on, or reviewed after incidents; not for fixing product bugs, provisioning infrastructure, or business analytics. |

## specialty (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`performance-engineer`](specialty/performance-engineer.md) | standard | read-write | Measures and improves performance when it is the dominant concern: cross-system CPU and memory profiling, benchmarks, load and soak tests, and targeted optimizations proven with before-and-after numbers. Use when a latency, throughput, memory, startup, or size target is missed or needs a budget; not for feature work, GPU rendering, model inference, query tuning, or capacity provisioning. |
| [`test-automation-engineer`](specialty/test-automation-engineer.md) | standard | read-write | Builds test infrastructure: test frameworks and harnesses (hardware-in-the-loop rigs included), integration, end-to-end, and visual-regression suites, fixtures and test data, flaky-test triage, and coverage tooling. Use when the deliverable is test capability or suite health rather than a feature; not for a feature's own unit tests, load tests, model or LLM evals, or production fixes. |

## documentation (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`kit-librarian`](documentation/kit-librarian.md) | standard | read-write | Maintains agent-facing docs: kit core files in the kit repository and the project overlay `.ai/project/`; keeps links valid, runs `agentkit.py sync` and `check`, and updates `reviewed` stamps after verification. Use when a wiki page, agent card, skill, stack pack, or profile must be added, changed, or regenerated; not for structural verdicts, external research, human docs, or editing `.ai/kit/`. |
| [`localization-specialist`](documentation/localization-specialist.md) | standard | read-write | Translates and localizes text into target languages directly from the source language: localized docs, UI strings, store listings, product and game text, plus the glossary that keeps terms and tone consistent. Use when text needs a version in another language or translations must follow a changed source; not for writing source text or i18n code. |
| [`technical-writer`](documentation/technical-writer.md) | standard | read-write | Writes and maintains human-facing project docs: README, guides, tutorials, API reference prose, and changelog wording, in the source language. Use when user- or developer-facing documentation must be created, corrected, or synced with shipped behavior; not for translations, agent-facing kit or overlay docs, documenting unverified behavior, or executing releases. |

## quality (quality)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`accessibility-reviewer`](quality/accessibility-reviewer.md) | standard | read-only | Reviews UI changes on web, mobile, desktop, and game UI for accessibility: WCAG conformance, platform accessibility APIs, keyboard and screen-reader paths, contrast, motion, and text scaling; returns approve or rework. Use when a change adds or alters user-facing UI or UI specifications; not for fixing findings or designing UX flows. |
| [`code-reviewer`](quality/code-reviewer.md) | deep | read-only | Reviews review-ready changes to code, configuration, or docs for correctness, regressions, compatibility, maintainability, test adequacy, and doc sync, and returns approve or rework. Use when a unit is implemented and verified and needs a quality gate; not for fixing the findings or security-specific review. |
| [`security-reviewer`](quality/security-reviewer.md) | deep | read-only | Reviews review-ready changes through a security lens: new attack surface, input handling and injection, authentication and authorization, secrets, dependency and supply-chain risk, and data exposure or privacy; returns approve or rework. Use when a change touches a trust boundary, credentials, personal data, permissions, or dependencies; not for fixing findings or general correctness review. |

## release (execution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`release-manager`](release/release-manager.md) | standard | read-write | Cuts releases and integrates multi-owner work: version bumps, changelog finalization, tags, release notes, publication and update channels, and fan-in of approved branches in dependency order. Use when approved work is ready to ship or several owners' branches must be merged; not for implementing features, adding changelog entries, release pipelines, approving quality, or semantic conflicts. |

## evolution (evolution)

| Agent | Tier | Access | Use when |
|---|---|---|---|
| [`trend-scout`](evolution/trend-scout.md) | standard | read-write | Monitors the sources listed in the tech radar (AI coding tools, model families, agent standards, stack-pack languages and frameworks), records dated, sourced intake items with impact, and keeps the entries of `core/wiki/evolution/tech-radar.md`. Use when a scan is due or an external release, deprecation, or format change may affect the kit; not for editing other kit files or structural decisions. |
