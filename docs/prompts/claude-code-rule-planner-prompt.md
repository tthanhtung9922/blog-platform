# Claude Code Prompt: Rule Planning & Creation từ Tài Liệu Dự Án

---

## Cách dùng

Paste toàn bộ nội dung block `PROMPT` vào Claude Code.  
Đặt các file `.md` tài liệu vào cùng thư mục làm việc trước khi chạy.

> **Lưu ý về "rule" ở đây**: Là các chỉ thị luôn-luôn-áp-dụng mà  
> Claude Code phải tuân theo trong suốt session — không cần gọi tên  
> như agent, không cần trigger như skill. Rules được đặt trong  
> `CLAUDE.md` (global) hoặc `.claude/rules/[name].md` (scoped).  
> Chúng định hình **cách Claude làm việc**, không phải **việc gì  
> Claude làm**.

---

## PROMPT

```
You are a senior software architect and code standards enforcer.
Your mission is to analyze this project's documentation, identify
every rule needed to keep Claude Code aligned with the project's
conventions and constraints, then create those rules as persistent
instruction files.

Rules are always-on directives — not triggered by phrases, not
invoked by name. They are the law Claude Code follows automatically
in every interaction within this project.

Rule files go in:
- `CLAUDE.md` — top-level, applies globally across the whole project
- `.claude/rules/[name].md` — scoped rules, organized by concern

---

## PHASE 1 — Read & Understand the Project

Scan and read every .md documentation file:

```bash
find . -name "*.md" ! -path "./.claude/*" ! -name "CLAUDE.md" | sort
```

Read each file completely. Extract:
- Tech stack, languages, frameworks, versions
- Architectural patterns enforced (DDD, Clean Architecture, CQRS, etc.)
- Naming conventions: files, classes, methods, variables, database
- Folder/module structure and where things belong
- Coding standards: formatting, linting, test requirements
- Forbidden patterns: what must never be done in this codebase
- Workflow rules: PR process, commit format, branch naming, CI gates
- Security constraints: what must never be logged, exposed, or hardcoded
- Domain language: ubiquitous language terms that must be used consistently

After reading, output a **Project Standards Summary**:
1. Non-negotiable architectural constraints
2. Naming and structure conventions
3. Forbidden patterns explicitly mentioned
4. Domain/ubiquitous language glossary (if DDD project)

---

## PHASE 2 — Map Concerns → Rules

Rules are organized by concern, not by role (unlike agents/skills).
Every rule applies to anyone touching that concern.

For each concern identified, list the rules needed:

---
### Concern: [Concern Name]
**What it governs**: [1-line description]
**Why it matters for this project**: [grounded in the docs]

**Rules needed**:

| Rule Name | What it enforces | Severity |
|---|---|---|
| `rule-name` | One-line description | MUST / SHOULD / NEVER |

Severity levels:
- **MUST**: Non-negotiable. Claude must always do this.
- **SHOULD**: Strong default. Deviate only with explicit justification.
- **NEVER**: Hard prohibition. No exceptions.

---

Concerns to consider (include only those present in this project):
- **Architecture** — layer boundaries, dependency direction, pattern enforcement
- **Naming** — classes, files, methods, variables, database objects
- **Domain Language** — ubiquitous language, bounded context terms
- **File Structure** — where things live, what goes in which folder
- **Code Style** — formatting, imports, patterns to prefer/avoid
- **Testing** — what must be tested, test naming, coverage gates
- **Security** — what never to log/expose/hardcode, auth patterns
- **Database** — migration rules, query patterns, ORM conventions
- **API Design** — endpoint naming, response format, versioning
- **Git & CI** — commit format, branch naming, PR checklist
- **Error Handling** — exception patterns, logging conventions
- **Dependencies** — approved packages, version pinning, import rules

For each rule, ask before including:
- Is this explicitly stated or strongly implied by the docs?
- Would violating this cause real problems (bugs, security issues, inconsistency)?
- Is this specific to THIS project, not generic best practice?

Flag rules you're inferring (not explicitly stated) — mark them `[INFERRED]`.
Ask the user to confirm inferred rules before Phase 3.

---

## PHASE 3 — Prioritize Rules

Rank all rules:

**Tier 1 — Architectural / Security**: Violations break the system or
create vulnerabilities. Must be in `CLAUDE.md` or enforced globally.

**Tier 2 — Consistency**: Violations create drift and tech debt.
Scoped rule files in `.claude/rules/`.

**Tier 3 — Preferential**: Style and convention. Can be in scoped files
or as notes within Tier 1/2 files.

Present as a table:

| Tier | Rule Name | Concern | MUST/SHOULD/NEVER | File Location |
|---|---|---|---|---|

**Wait for user confirmation before proceeding to Phase 4.**
Ask: "Any rules to add, adjust severity, or remove before I start writing?"

---

## PHASE 4 — Create Rules (Structured by File)

Unlike agents (one file per agent), rules are grouped into files by concern.
Create files in this order: CLAUDE.md first, then scoped rule files.

---

### CLAUDE.md — Global Rules

This is the most important file. It must be concise and scannable.
Claude Code reads this on every interaction — keep it focused on
the highest-severity, cross-cutting rules only.

Structure:

```markdown
# [Project Name] — Claude Code Rules

## What This Project Is
[2-3 sentences: domain, tech stack, architectural style]

## Architecture Rules
[MUST/NEVER statements about layer boundaries, dependencies, patterns]

## Domain Language
[Key ubiquitous language terms Claude must always use]
[Terms that must NEVER be used (wrong-layer vocabulary, etc.)]

## Security Rules
[NEVER statements: what to never log, hardcode, expose]

## File Structure
[Where things belong — with actual paths from this project]

## Quick Reference: What Goes Where
[Table or list mapping concept → correct location]
```

Keep CLAUDE.md under 150 lines. If a concern needs more detail,
move it to a scoped file and add a one-line pointer in CLAUDE.md:
`> See .claude/rules/testing.md for full test conventions.`

---

### Scoped Rule Files — `.claude/rules/[concern].md`

For each concern with enough rules to warrant its own file:

```markdown
---
description: >
  [When these rules apply. Be specific. E.g.: "Apply these rules
  whenever writing, editing, or reviewing any test file. Triggers
  when the task involves unit tests, integration tests, test data,
  mocks, or test configuration."]
---

# [Concern] Rules

## MUST
- [Concrete rule with example from this project]
- [...]

## SHOULD
- [Default behavior with rationale]
- [...]

## NEVER
- [Hard prohibition]
- [Bad example] ❌ vs [Good example] ✅
- [...]

## Examples
### Correct ✅
[Code or structure example from this project's domain]

### Incorrect ❌
[What Claude must not generate — with explanation why]

## Edge Cases
[Known situations where rules interact or need clarification]
```

---

### Step-by-step for each file:

1. **Announce** which file you're creating and what concerns it covers
2. **Re-read** relevant doc sections for that concern
3. **Write** the rule file following the structure above
4. **Self-check**:
   - Every rule is grounded in THIS project's docs (not generic advice)?
   - NEVER rules have a concrete bad example?
   - MUST rules are actually non-negotiable (not just preferences)?
   - File is scannable — a developer can find the rule they need in < 30 seconds?
   - No contradiction between rules in this file and CLAUDE.md?
5. **Show** the file and ask: "Does this match how your team enforces standards?"

---

## PHASE 5 — Consistency Check

After all files are created, run a cross-file check:

1. **Contradiction scan**: Are there any rules that conflict across files?
2. **Coverage check**: Are there concerns from the docs not covered by any rule?
3. **Redundancy check**: Is anything stated in both CLAUDE.md and a scoped file?
   (If so, keep the detail in the scoped file, keep only a pointer in CLAUDE.md)
4. **Severity audit**: Any rule marked MUST that's actually a preference? Vice versa?

Output a brief consistency report, then ask if the user wants any adjustments.

---

## PHASE 6 — Generate Index

Create `.claude/rules/README.md`:

```markdown
# Project Rules Index

## File Map

| File | Concerns Covered | Key Rules |
|---|---|---|
| `CLAUDE.md` | Architecture, Security, Domain Language | ... |
| `.claude/rules/testing.md` | Test conventions, coverage | ... |
| ... | ... | ... |

## Severity Summary

### NEVER (Hard Prohibitions)
- [rule] — [file]

### Critical MUSTs
- [rule] — [file]

## How Rules Differ from Agents
Rules are always-on constraints. Agents are invoked for specific tasks.
When in doubt: if it's "always true no matter what", it's a rule.
If it's "do this specific workflow", it's an agent.
```

---

## HARD CONSTRAINTS

1. **Always-on, not triggered**: Rules must be written as standing
   constraints, not task instructions. "Always use PascalCase for
   domain entities" not "When creating an entity, use PascalCase."

2. **No generic rules**: "Write clean code" or "Follow SOLID" are not
   rules. Every rule must reference this project's actual structure,
   patterns, or domain.

3. **NEVER rules must have examples**: Any prohibition without a
   concrete bad example is too vague to enforce.

4. **CLAUDE.md stays lean**: Global file is for cross-cutting,
   highest-severity rules only. Detailed conventions go in scoped files.

5. **Inferred rules need confirmation**: If a rule isn't explicitly in
   the docs, flag it and ask before writing it into a file.

6. **No overlap with agents**: If something is a workflow ("here's
   how to add an entity"), it belongs in an agent, not a rule.
   Rules govern constraints; agents govern workflows.

---

## START

Begin with Phase 1.
List every .md doc file found, then output the Project Standards Summary
before moving to Phase 2.
```
