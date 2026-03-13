# Claude Code Prompt: Agent Planning & Creation từ Tài Liệu Dự Án

---

## Cách dùng

Paste toàn bộ nội dung block `PROMPT` bên dưới vào Claude Code.  
Đặt các file `.md` tài liệu vào cùng thư mục làm việc trước khi chạy.

> **Lưu ý về "agent" ở đây**: Là các file `.md` đặt trong thư mục  
> `.claude/agents/` — Claude Code sẽ tự động load chúng như các  
> sub-agent chuyên biệt, mỗi agent có system prompt, tools, và  
> phạm vi trách nhiệm riêng.

---

## PROMPT

```
You are a senior software architect. Your mission is to analyze this
project's documentation, identify every agent needed for each role,
then create those agents one by one as Claude Code sub-agent files.

Agents here means: Markdown files placed in `.claude/agents/` — each
file defines a specialized sub-agent with its own system prompt, tool
access, and responsibility boundary. Claude Code automatically loads
them and can invoke them by name.

---

## PHASE 1 — Read & Understand the Project

Scan and read every .md documentation file:

```bash
find . -name "*.md" ! -path "./.claude/*" | sort
```

Read each file completely. Extract:
- What does this project do? What domain/industry?
- Tech stack: languages, frameworks, architectural patterns (DDD, hexagonal, CQRS, etc.)
- Key workflows: how features are built, tested, deployed
- Bounded contexts or modules (if DDD is used)
- Team structure hints: who works on what parts of the codebase
- Recurring tasks that are complex, error-prone, or multi-step

Do NOT skip files. Do NOT summarize prematurely — extract details.

After reading, output a **Project Summary** covering:
1. What the system does
2. Tech stack and patterns used
3. Key modules / bounded contexts
4. Identified roles (from docs or inferred from structure)

---

## PHASE 2 — Map Roles → Agents

For each role identified, list the agents that would serve them.

Output format for each role:

---
### Role: [Role Name]
**Who they are**: [1-line description]
**Their main responsibilities**: [2-3 sentences from the docs]

**Agents needed**:

| Agent Name | Responsibility | Key Trigger Phrases |
|---|---|---|
| `agent-name` | What it does in 1 sentence | "phrase 1", "phrase 2" |
| ... | ... | ... |

---

Roles to consider (include only those relevant to this project):
- **Backend Developer** — implements domain logic, application services, APIs
- **Frontend Developer** — builds UI, consumes APIs
- **DevOps / Platform Engineer** — CI/CD, infra, deployments, monitoring
- **QA / Test Engineer** — writes and runs tests, manages test data
- **Database Engineer** — schema design, migrations, query optimization
- **Architect / Tech Lead** — code review, design decisions, ADRs
- **API Consumer / Integrator** — external teams consuming the APIs

For each agent, evaluate before including:
- **Impact**: Does this agent prevent mistakes or save significant time?
- **Frequency**: Is this workflow done regularly?
- **Complexity**: Would a developer need to look things up or think carefully?

Only include agents scoring HIGH on at least 2 of 3 criteria above.
Flag any agents you're uncertain about — ask before Phase 3.

---

## PHASE 3 — Prioritize Agents

Rank all agents across all roles into tiers:

**Tier 1 — Critical**: Multi-role impact, daily use, high complexity  
**Tier 2 — Important**: Single-role but high value, weekly use  
**Tier 3 — Supplementary**: Infrequent or lower complexity  

Present the full prioritized list as a table:

| Tier | Agent Name | Roles Served | Rationale |
|---|---|---|---|

**Wait for user confirmation before proceeding to Phase 4.**
Ask: "Does this list look right? Any agents to add, remove, or reprioritize?"

---

## PHASE 4 — Create Agents (One at a Time)

For each agent (Tier 1 first, then 2, then 3):

### Step 1 — Announce
State which agent you're creating, what tier, and which roles it serves.

### Step 2 — Research
Re-read the relevant sections of the project docs for this agent's domain.
Note any project-specific conventions, patterns, naming rules, file structures.

### Step 3 — Write the agent file

Create the file at: `.claude/agents/[agent-name].md`

Use this structure:

---
```markdown
---
name: agent-name
description: >
  [CRITICAL — this is what triggers the agent. Be specific and slightly
  "pushy". List concrete situations, user phrases, and contexts where
  this agent should activate. Examples:
  "Use this agent when adding a new domain entity, aggregate root, or
  value object. Triggers on: 'add entity', 'create aggregate', 'new
  domain model', 'implement value object'."
  The more specific and complete, the better the triggering accuracy.]
tools:
  - [list only tools this agent actually needs]
  # Common tools: Read, Write, Edit, Bash, Grep, Glob, TodoWrite
  # Omit tools the agent doesn't need — principle of least privilege
---

# [Agent Name]

## Purpose
[1-2 sentences: what problem this agent solves for the developer]

## Scope & Boundaries
**In scope**: [what this agent handles]
**Out of scope**: [what to hand off to another agent — name it]

## Project Context
[Key facts extracted from THIS project's docs that shape how this
agent works. E.g.: "This project uses DDD with clean architecture.
Domain layer must not reference infrastructure. Aggregates live in
src/Domain/[BoundedContext]/"]

## Workflow

### 1. [First Step Name]
[Concrete instructions. Reference actual file paths, naming conventions,
base classes, interfaces from this project.]

### 2. [Second Step Name]
...

### N. Verification
[How to verify the work is correct. Commands to run, files to check.]

## Project-Specific Conventions
[Naming rules, folder structure, patterns extracted from the docs.
This section must be grounded in THIS project — no generic advice.]

## Output Checklist
Before finishing, verify:
- [ ] [Project-specific check 1]
- [ ] [Project-specific check 2]
- [ ] [Tests pass: command to run]

## Examples
[At least 1 concrete before/after or input/output example from this
project's domain. Use real names from the docs if available.]

## Related Agents
- `other-agent-name` — use when [handoff condition]
```
---

### Step 4 — Self-check before saving
- Does `description` clearly say WHEN to trigger (not just what it does)?
- Does the workflow reference actual paths/patterns from THIS project?
- Are tools minimal — only what's needed?
- Is the file under 300 lines? If longer, extract large references to
  `.claude/agents/references/[agent-name]/[topic].md` and link them.
- Would a junior developer on this project find this actionable?

### Step 5 — Show & confirm
Display the full agent file content.
Ask: "Does this look right for how your team works? Ready for the next agent?"

Do NOT proceed to the next agent without confirmation.

---

## PHASE 5 — Generate Registry

After all agents are created, generate `.claude/agents/README.md`:

```markdown
# Project Agents Registry

## Quick Reference

| Agent | Roles | Use When |
|---|---|---|
| [agent-name] | Backend Dev, Architect | ... |

## By Role

### Backend Developer
- `agent-name` — [one-line description]

### [Other Role]
...

## Agent Dependencies
[If agents hand off to each other, diagram it here]

## Setup
Agents are auto-loaded by Claude Code from `.claude/agents/`.
No configuration needed.
```

---

## HARD CONSTRAINTS

1. **No generic agents** — every agent must reference this project's
   actual tech stack, file structure, and patterns. "Write clean code"
   is not an agent.

2. **No overlapping scope** — if two agents would do similar things,
   merge them with role-specific sections, or define clear handoff points.

3. **Minimal tools** — only grant tools the agent genuinely needs.
   An agent that only reads and generates code doesn't need Bash.

4. **Grounded in docs** — if something isn't in the documentation,
   say so explicitly rather than inventing conventions.

5. **Agent names**: kebab-case, verb-noun format preferred:
   `add-domain-entity`, `run-integration-tests`, `generate-migration`,
   `review-pull-request`, `deploy-to-staging`.

6. **File location**: always `.claude/agents/[name].md`

---

## START

Begin with Phase 1.
List every .md doc file found, then output the Project Summary
before moving to Phase 2.
```
