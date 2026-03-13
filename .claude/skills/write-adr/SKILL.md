# write-adr

Create an Architecture Decision Record (ADR) following the project's established format and numbering convention.

## Arguments

- `title` (required) — ADR title (e.g., "Use Meilisearch for Full-Text Search")
- `status` (optional) — `proposed`, `accepted`, `deprecated`, `superseded` (default: `proposed`)

## Instructions

You are creating an ADR for the blog-platform. The project already has 9 ADRs (ADR-001 through ADR-009) documented in `docs/blog-platform/03-architecture-decisions.md`.

### Step 1 — Determine the Next ADR Number

Read `docs/blog-platform/03-architecture-decisions.md` and find the highest existing ADR number. The new ADR should be the next sequential number (e.g., if ADR-009 exists, create ADR-010).

### Step 2 — Write the ADR

Append the new ADR to `docs/blog-platform/03-architecture-decisions.md` following this exact format:

```markdown
## ADR-{NNN}: {Title}

**Status:** {Proposed | Accepted | Deprecated | Superseded by ADR-XXX}
**Date:** {YYYY-MM-DD}
**Deciders:** {Who made this decision}

### Context

{What is the issue or problem that motivates this decision?}
{What constraints exist?}
{What options were considered?}

### Decision

{What is the change being proposed or made?}
{Be specific about technologies, patterns, and implementation details.}

### Options Considered

| Option | Pros | Cons |
|--------|------|------|
| {Option A} | {Pros} | {Cons} |
| {Option B} | {Pros} | {Cons} |
| {Option C} | {Pros} | {Cons} |

### Consequences

**Positive:**
- {Benefit 1}
- {Benefit 2}

**Negative:**
- {Tradeoff 1}
- {Tradeoff 2}

**Risks:**
- {Risk 1 and mitigation}

### References

- {Link to documentation, RFC, library, etc.}
```

### Existing ADRs Reference

| ADR | Title | Status |
|-----|-------|--------|
| ADR-001 | Monorepo with Nx | Accepted |
| ADR-002 | Clean Architecture + DDD | Accepted |
| ADR-003 | Two Separate Frontend Apps | Accepted |
| ADR-004 | RBAC Strategy (3-Layer) | Accepted |
| ADR-005 | Caching Strategy (Cache-Aside) | Accepted |
| ADR-006 | IdentityUser vs Domain User Separation | Accepted |
| ADR-007 | Cross-Context Transaction Strategy | Accepted |
| ADR-008 | Cache Opt-in via ICacheableQuery | Accepted |
| ADR-009 | PostgreSQL FTS + Vietnamese Config | Accepted |

### Key Rules

1. **Sequential numbering** — Never skip numbers or reuse deprecated numbers
2. **Immutable once accepted** — Accepted ADRs are never edited (only superseded by new ones)
3. **Include all options considered** — Even rejected options, with clear reasoning
4. **Concrete consequences** — Both positive and negative, with risk mitigations
5. **Cross-reference related ADRs** — If this decision relates to an existing ADR, reference it
6. **Date is mandatory** — Use ISO 8601 format (YYYY-MM-DD)
