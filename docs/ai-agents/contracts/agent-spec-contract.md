# Agent Spec Contract

## When

- you are adding or revising multiple agent spec files and want to keep their structure consistent,
- you need to decide what belongs inside an individual agent file versus a shared wiki page,
- you want to reduce repeated agent-doc boilerplate without losing routing clarity.

## Route Away When

- the question is about approved owner inventory or rollout status: use [agent-catalog.md](../routing/agent-catalog.md),
- the question is about cross-agent packet format or persistence rules: use [handoff-contract.md](handoff-contract.md),
- the question is about choosing the current owner: use [routing/README.md](../routing/README.md).

## Owns

- the shared contract for `.github/agents/*.agent.md` documents.

## Shared Rule

- Keep repository-wide rules and same-surface procedures out of individual agent specs.
- If two or more agent docs need the same rule, move that rule to one shared wiki page and link to it.
- Use links for routing, not duplicate prose.

## Standard Sections

| Section | Purpose | Keep local when | Move out when |
|---|---|---|---|
| `Use This Agent When` | Fast owner selection for the current doc | the decision is specific to this agent | the same sibling-owner comparison appears in several agent docs |
| `Do Not Use This Agent When` | Fast disqualification for the current doc | the exclusion is unique to this agent | the exclusion belongs to a shared owner-selection matrix |
| `Related References` | Route the reader to nearby owning docs | the references are specific to this agent | every sibling doc repeats the same peer list |
| `Mission` | Explain the agent's durable ownership intent | the wording is unique to the owner | the rule is really a shared architecture or policy rule |
| `Primary Responsibilities` | List durable owner-specific duties | the responsibility is unique to the owner | the same responsibility is shared by multiple siblings |
| `Non-Goals` | Protect boundaries and prevent overlap | the non-goal is unique to the owner | the same boundary belongs in a shared routing page |
| `Inputs` / `Required Inputs` | Describe agent-specific intake | the inputs differ by owner | the input contract is the same across several siblings |
| `Outputs` / `Expected Outputs` | Describe agent-specific artifacts | the outputs differ by owner | the output contract is shared enough to live in a common contract page |
| `Coordination Dependencies` | Describe owner-specific collaboration needs | the dependency is unique to the owner | the same sibling-routing or handoff rule is repeated across a group |
| `Review Triggers` | Mark owner-specific review cases | the trigger is specific to this owner | the trigger is a repository-wide review rule |
| `Escalation Triggers` | Mark when ownership should move | the escalation is unique to this owner | the same routing split is shared by sibling owners |

## Minimal Spec Principle

- Each agent spec should explain only what is unique to that owner.
- Shared sibling-routing rules should live in one wiki page for that owner family.
- Shared procedural or repository rules should live in the owning wiki page or instruction file, not be recopied into every agent spec.