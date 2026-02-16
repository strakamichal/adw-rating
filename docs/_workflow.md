# Documentation Workflow

Master workflow for creating project documentation with AI agent assistance. This file is the **entry point** — read it first, then follow the phases in order.

## Scenario routing

Before starting, identify your scenario and check which phases apply:

### Greenfield (new project)
- [ ] ~~Phase 1: AS-IS~~ — **SKIP** (nothing to analyze)
- [ ] Phase 2: Vision & scope
- [ ] Phase 3: Domain model
- [ ] Phase 4: Architecture
- [ ] Phase 5: UI structure
- [ ] Phase 6: Implementation plan
- [ ] Phase 7: AI guidelines

---

## Per-run rules

These rules apply to every AI agent run during documentation:

1. **One small step per run** — do not try to fill in an entire document at once. Focus on one section or one module.
2. **Do not regenerate completed sections** — only add to or refine what's already written.
3. **Ask when unclear** — if a requirement is ambiguous, stop and ask the human. Do not guess.
4. **Reference sources** — when analyzing existing code, reference file paths and line numbers.
5. **Mark progress** — check off completed items in the relevant phase checklist.

---

## Phase 1: AS-IS Analysis

**Goal**: Understand the current system before designing the replacement or extension.

**Template**: [`01-as-is.md`](01-as-is.md)

**When to use**: Rewrite (full), Major feature (scoped to affected areas).

**AI agent instructions**:
1. Read the existing codebase — solution structure, projects, dependencies
2. Identify business modules by scanning controllers, pages, database tables
3. For each module, fill in a module detail page using [`01-as-is/_module-template.md`](01-as-is/_module-template.md)
4. Document external integrations (APIs, file imports, email, etc.)
5. Note key observations: pain points, tech debt, missing tests, performance issues

**Done criteria**:
- [ ] Overview section filled (what the app does, who uses it)
- [ ] Architecture and projects section reflects actual solution structure
- [ ] All business modules listed with status (TODO/DONE)
- [ ] At least the core modules have detailed analysis
- [ ] External integrations documented
- [ ] Key observations noted

**Protection sections reminder**: Note any areas where the existing system has critical business logic that must be preserved exactly.

---

## Phase 2: Vision & Scope

**Goal**: Define what you're building and why. Set boundaries.

**Template**: [`02-to-be.md`](02-to-be.md)

**AI agent instructions**:
1. Start with the vision — what is the primary motivation? (modernization, new capability, performance, etc.)
2. Define scope clearly — what's in, what's out
3. List non-goals explicitly to prevent scope creep
4. Write key use cases at a high level (not implementation details)
5. Define NFRs (performance, security, availability, etc.)
6. For rewrites: create the change summary (KEEP/CHANGE/REMOVE/NEW)
7. **Fill in protection sections** — this is critical for AI safety

**Done criteria**:
- [ ] Vision explains the "why" clearly
- [ ] Scope lists all areas/modules covered
- [ ] Non-goals are explicit
- [ ] Key use cases are listed (5-15 items)
- [ ] NFRs are defined with measurable targets where possible
- [ ] Change summary vs AS-IS (rewrite only)
- [ ] Protection sections filled: Always / Ask first / Never

---

## Phase 3: Domain Model

**Goal**: Define entities, their fields, relationships, and business rules.

**Template**: [`03-domain-and-data.md`](03-domain-and-data.md)

**AI agent instructions**:
1. Start with the glossary — define all domain terms before writing entities
2. Define core entities with fields, types, and validation rules
3. Define enums with all allowed values
4. Describe relationships (1:N, M:N, ownership, cascading)
5. For rewrites: add mapping from AS-IS entities to new model
6. Add acceptance criteria per entity

**Done criteria**:
- [ ] Glossary covers all domain terms used in the project
- [ ] All entities from scope have field definitions
- [ ] Business rules / validation documented per entity
- [ ] Relationships between entities described
- [ ] Enums defined with all values
- [ ] Mapping from AS-IS (rewrite only)
- [ ] Acceptance criteria present for core entities

---

## Phase 4: Architecture & Interfaces

**Goal**: Define the technical stack, project structure, layers, APIs, and deployment.

**Template**: [`04-architecture-and-interfaces.md`](04-architecture-and-interfaces.md)

**AI agent instructions**:
1. Define the technical stack with specific versions
2. Design the project/layer structure with dependency rules
3. Describe how a request flows through the system (e.g., HTTP → Controller → Service → Repository → DB)
4. List external integrations with their protocols
5. Define API endpoints per module (REST/GraphQL/gRPC)
6. Document deployment topology and CI/CD pipeline

**Done criteria**:
- [ ] Technical stack fully specified
- [ ] Project structure with dependency rules (who can depend on whom)
- [ ] Internal flow described (request lifecycle)
- [ ] External integrations listed
- [ ] API outline with endpoints per module
- [ ] Deployment and runtime documented

---

## Phase 5: UI Structure

**Goal**: Define screens, navigation, flows, and access control for the user interface.

**Template**: [`05-ui-structure.md`](05-ui-structure.md)

**AI agent instructions**:
1. Define the navigation structure (main menu, sections)
2. List screens per module (list, detail, forms)
3. Define role-based access (who sees what)
4. Describe 2-4 key UI flows end-to-end
5. Define export/download UX patterns
6. Add acceptance criteria per screen
7. Add verification guides for key flows

**Done criteria**:
- [ ] Navigation structure defined
- [ ] All screens from scope listed with purpose
- [ ] Role-based access matrix present
- [ ] Key UI flows described step by step
- [ ] Export/download patterns defined
- [ ] Per-screen acceptance criteria present
- [ ] Verification guides for 2-4 key flows

---

## Phase 6: Implementation Plan

**Goal**: Break the work into phased, ordered tasks with dependencies and test expectations.

**Template**: [`06-implementation-plan.md`](06-implementation-plan.md)

**AI agent instructions**:
1. Define implementation phases (Foundation/MVP → Phase 2 → Phase 3 → ...)
2. Break each phase into small tasks (1-3 files, max 30-60 min each)
3. State dependencies explicitly (which tasks block which)
4. Define expected test types per task (unit / integration / E2E)
5. Add completion gates per task (build, tests, docs)
6. For rewrites: include data migration tasks

**Done criteria**:
- [ ] Phases defined with clear scope per phase
- [ ] All tasks have descriptions, dependencies, and test expectations
- [ ] Completion gates defined per task
- [ ] Task ordering makes sense (foundation first, then features)
- [ ] Data migration tasks included (rewrite only)
- [ ] Estimated task count feels manageable per phase (5-20 tasks)

---

## Phase 7: AI Guidelines

**Goal**: Define coding standards, testing requirements, and agent boundaries for implementation.

**Template**: [`07-ai-guidelines.md`](07-ai-guidelines.md)

**AI agent instructions**:
1. Define coding style with naming conventions and examples
2. Define test pyramid — what to test at which layer
3. Document the docs-code sync rules
4. Define agent boundaries: Always / Ask first / Never
5. Define output format expectations
6. Define observability and logging standards

**Done criteria**:
- [ ] Coding style defined with examples
- [ ] Test pyramid documented with layer-specific guidance
- [ ] Docs sync rules clear
- [ ] Boundaries section filled (Always / Ask first / Never)
- [ ] Output format defined
- [ ] Observability rules defined
- [ ] Guidelines are specific enough to be actionable, not generic platitudes

---

## After all phases

Once all documentation is complete:

1. **Cross-reference check**: entity names in domain model match API endpoints, UI screens reference correct entities, implementation plan covers all scope items
2. **Customize CLAUDE.md**: update with project-specific architecture rules, source of truth links, and test commands
3. **Start implementing**: follow the implementation plan, one task at a time
4. **Keep docs updated**: when implementation reveals needed changes, update docs in the same commit
