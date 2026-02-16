# Implementation Plan

<!-- AI AGENT: To fill this document:
1. Derive phases from the scope in 02-to-be.md
2. Phase 1 should always be foundation: project setup, auth, base entities
3. Break each phase into small tasks (1-3 files, 30-60 min each)
4. State dependencies explicitly — which tasks must complete before others
5. Define test expectations per task
6. Add completion gates: build, tests, docs
7. For rewrites: include data migration tasks in appropriate phases
-->

## Principles

- **Small tasks**: Each task = 1-3 files, max 30-60 min work
- **Correct ordering**: Foundation → Backend → API → Client → UI
- **Explicit dependencies**: Prerequisites clearly stated
- **Test-driven completion**: Every task must pass tests before moving on

## Task Completion Checklist

**Before marking any task as complete, you MUST:**

1. **Build passes**: `dotnet build` (or equivalent) succeeds with no errors
2. **Write tests**: Create appropriate tests for the new code
3. **Run tests**: All tests pass (not just new ones)
4. **Commit**: Commit the changes with a descriptive message

**If tests fail:**
- Fix the issue before proceeding
- Do NOT skip to the next task
- Re-run all tests after fix

## Testing Guidelines

| Task type | Required tests |
|-----------|----------------|
| Entity + DB mapping | Unit test for validation, integration test for persistence |
| Repository | Integration test with real DB |
| Service | Unit test with mocked dependencies |
| API Controller | Integration test for endpoints |
| API Client | Unit test with mocked HTTP |
| UI Component/Page | E2E test for key flows |

**Test naming convention**: `[MethodName]_[Scenario]_[ExpectedResult]`

---

## Phases

### Phase 1 — Foundation

**Goal**: Project setup, authentication, base infrastructure.

- [ ] **1.1** [Task title]
  - Description: [What to implement]
  - Files: [Expected files to create/modify]
  - Dependencies: none
  - Tests: [unit / integration / none]
  - **Completion gates**: build | tests | docs

- [ ] **1.2** [Task title]
  - Description: [What to implement]
  - Files: [Expected files]
  - Dependencies: 1.1
  - Tests: [type]
  - **Completion gates**: build | tests

<!-- Add more tasks as needed -->

---

### Phase 2 — [Phase name, e.g., "Core CRUD"]

**Goal**: [What this phase delivers]

- [ ] **2.1** [Task title]
  - Description: [What to implement]
  - Dependencies: Phase 1
  - Tests: [type]
  - **Completion gates**: build | tests | docs

<!-- Add more tasks -->

---

### Phase 3 — [Phase name, e.g., "Business Logic"]

**Goal**: [What this phase delivers]

- [ ] **3.1** [Task title]
  - Description: [What to implement]
  - Dependencies: [specific tasks]
  - Tests: [type]
  - **Completion gates**: build | tests

<!-- Add more phases as needed -->

---

### Data Migration Phase (rewrite only)

<!-- Delete this section for greenfield projects. -->

**Goal**: Migrate data from the legacy system to the new one.

- [ ] **M.1** Design migration scripts
  - Description: Map AS-IS tables to TO-BE entities, define transformation rules
  - Dependencies: Phase 1 complete, domain model stable
  - Tests: Dry-run migration on test data

- [ ] **M.2** Implement migration tooling
  - Description: [Scripts, ETL pipeline, or migration service]
  - Dependencies: M.1
  - Tests: Migration of sample dataset, data integrity checks

- [ ] **M.3** Validate migrated data
  - Description: Run integrity checks, compare counts, spot-check records
  - Dependencies: M.2
  - Tests: Automated comparison reports

---

## Notes

### Constraints
- [e.g., "Must maintain backwards compatibility with existing API consumers during transition"]
- [e.g., "Database must support concurrent access from old and new system during migration"]

### Risks
- [e.g., "Complex business rules in legacy module X may have undocumented behavior"]
- [e.g., "Third-party API Y has rate limits that affect migration speed"]

### Dependencies
- [e.g., "External team must provide API credentials for integration Z"]
