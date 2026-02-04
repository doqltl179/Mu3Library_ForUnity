# Mu3Library For Unity

## ⚠️ CRITICAL: Task Execution Protocol

**MANDATORY**: You WILL follow this protocol BEFORE starting ANY work.

### Step 1: Read Required Instructions (MANDATORY)

You MUST read these files in order:

1. **FIRST**: You WILL read `.github/agents/task-planner.agent.md`
   - Contains: TODO list creation, progress tracking, and task management requirements
   - **CRITICAL**: This defines HOW you WILL work and communicate progress

2. **THEN**: For Unity tasks, you WILL read `.github/agents/unity.agent.md`
   - Contains: Project architecture, coding conventions, and best practices
   - **CRITICAL**: This defines WHAT patterns and standards you WILL follow

### Step 2: Execute Task Planning Process

**MANDATORY**: You WILL follow the workflow defined in `task-planner.agent.md`:

1. You WILL create a detailed TODO list as a Markdown table BEFORE starting work
2. You WILL update task status throughout the work (⏳ → 🔄 → ✅)
3. You WILL provide progress updates after completing EACH task (no exceptions)
4. You WILL mark tasks completed IMMEDIATELY when finished
5. You WILL NOT batch multiple completions into one update

**DO NOT proceed with implementation until you have:**
- ✅ Read the required instruction files
- ✅ Created and presented the TODO list to the user
- ✅ Received confirmation or begun systematic execution

### Pre-Work Verification Checklist

Before starting ANY implementation work, you MUST verify:

1. ✅ **Instructions Read**: Both task-planner.agent.md and unity.agent.md (if Unity work)
2. ✅ **TODO List Created**: Markdown table with all tasks listed
3. ✅ **TODO List Displayed**: User can see the initial plan
4. ✅ **Context Gathered**: All necessary files and information reviewed
5. ✅ **Ready to Track**: Prepared to update progress after EACH task

**CRITICAL**: If ANY of these items are incomplete, STOP and complete them first.

---

## Project Philosophy

Mu3Library is designed as a **third-party package** intended for integration into external Unity projects. This fundamental design principle shapes our development approach:

### Independence & Self-Sufficiency
- **Package Completeness First**: We prioritize the standalone quality and completeness of this package above all else
- **Compatibility is Secondary**: While we aim for broad compatibility, external project requirements do not dictate our design decisions
- **Self-Contained Standards**: Our coding rules and conventions are optimized for this package's internal quality and maintainability, not external conformity

### Development Priorities
1. **Code Quality**: Clean, maintainable, well-documented code
2. **Internal Consistency**: Uniform patterns and conventions throughout the package
3. **Unity Best Practices**: Adherence to official Unity guidelines and modern C# standards
4. **Independent Evolution**: Freedom to adopt new patterns and technologies without external constraints

### Design Goals
- Build a robust, self-contained framework that can stand on its own
- Avoid coupling to specific external project requirements or coding styles
- Maintain flexibility to evolve architecture and patterns independently
- Provide clear, well-defined public APIs for external integration
- Ensure the package remains modular and loosely coupled internally

### Integration Model
External projects integrate **with** Mu3Library, not the other way around. We provide:
- Clean public interfaces
- Comprehensive documentation
- Clear integration guidelines
- Stable, versioned releases

But we do **not** compromise internal design to match external project conventions.

## Workflow Guidelines

### Required Reading Before Starting
- **MUST read** `.github/agents/task-planner.agent.md` - Contains task management and TODO list requirements
- **For Unity work**, MUST also read `.github/agents/unity.agent.md` - Contains architecture and coding standards
- **For general Unity guidelines**, see `.github/instructions/unity.instructions.md`

### Tool Usage
- Use **ai-game-developer** agent when MCP is connected
- Use **unityMCP** tools when working with Unity Editor (when MCP is connected)

### Development Workflow
1. **READ the required agent files above** (this is not optional)
2. Follow the task planning process described in `task-planner.agent.md`
3. Apply coding conventions from `unity.agent.md` for Unity work
4. Execute and track as instructed in those files

## Progress Tracking Requirements

**MANDATORY**: You WILL track and communicate progress throughout ALL multi-step work.

### Core Requirements

You WILL follow these requirements from `.github/agents/task-planner.agent.md`:

1. **Initial Plan**: You WILL present a TODO list as a Markdown table at the start
2. **Status Updates**: You WILL update the table after completing each task
3. **Progress Calculation**: You WILL show completed/total tasks and percentage
4. **Visual Indicators**: You WILL use emoji status indicators (⏳🔄✅⚠️❌🔍)
5. **Continuous Updates**: You WILL NOT wait until all tasks are done to show progress

**READ `.github/agents/task-planner.agent.md` for:**
- Complete TODO list format specification
- Status indicator definitions
- Progress update format
- Practical examples with before/after tables

### Communication Protocol

You WILL communicate with the user as follows:

**Initial Response** (Before any work):
1. Acknowledge the request
2. Display the TODO list table
3. State: "Starting work..."

**During Work** (After each task completion):
1. Redisplay the updated TODO list table
2. Show progress: "Progress: X/Y tasks (Z%)"
3. Brief note: "Completed: [task description]"

**Final Response** (After all tasks):
1. Show final table (all ✅)
2. "Progress: Y/Y tasks (100%)"
3. Summary of all work completed

**CRITICAL**: The user MUST see multiple table updates throughout your work, not just at the start and end.

### What You MUST NOT Do

You WILL NEVER:
- ❌ Remain silent between showing initial plan and final result
- ❌ Say "working on it" without showing updated TODO table
- ❌ Complete multiple tasks without intermediate updates
- ❌ Provide only text descriptions of progress without the table
- ❌ Skip the TODO list for "small" or "quick" multi-step work
- ❌ Batch task completions (e.g., "Completed tasks 1, 2, and 3")

**Remember**: If you're doing multiple distinct steps, you MUST show progress after EACH step.

## Exception: When Task Tracking May Be Skipped

Task tracking may be omitted **ONLY** for:
- Single-line code fixes (e.g., fixing a typo, changing one variable)
- Simple file reads without any changes
- Pure information queries with no implementation
- Quick documentation typo fixes

**If you're unsure whether to create a task list, CREATE IT. It's better to over-communicate than under-communicate.**