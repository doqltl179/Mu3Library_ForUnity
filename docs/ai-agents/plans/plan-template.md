# Task Plan Template

Use this template for non-trivial work that needs 3+ steps, architectural choices, or explicit verification.

```markdown
# <Task Title>

## Scope

- Goal:
- Out of scope:

## Relevant Files

- Must inspect:
- May inspect:

## Risks

- API / behavior:
- Assets / serialization:
- Verification gap:

## Steps

| # | Task | Status | Details |
|---|---|---|---|
| 1 | Analyze requirements | Not Started | Review the smallest relevant files first |
| 2 | Implement changes | Not Started | Keep edits scoped to the stated goal |
| 3 | Verify changes | Not Started | Run the smallest meaningful checks |
| 4 | Final report | Not Started | Summarize changed files, verification, and remaining risks |
```

Statuses: `Not Started`, `In Progress`, `Completed`, `Blocked`, `Failed`.

## Rules

- Keep the scope tight enough that one owner can complete it without nested subplans.
- Update status immediately after each step changes state.
- If the unit branches into unrelated work, start a new plan instead of expanding the current one.
- Link to supporting wiki pages instead of copying long procedures into the plan.
