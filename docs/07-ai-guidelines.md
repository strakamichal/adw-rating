# AI Coding and Collaboration Guidelines

<!-- AI AGENT: This document defines how you should write code, test, and communicate.
Follow these rules strictly. When in doubt, stop and ask. -->

## 1. Coding style

### Naming conventions

<!-- Customize these for your language/framework. Examples below are for C#/.NET. -->

| Element | Convention | Example |
|---------|-----------|---------|
| Entity class | PascalCase, singular | `TrainingSession`, `Employee` |
| Interface | `I` prefix + PascalCase | `ISessionRepository`, `IEmailService` |
| Service class | PascalCase + `Service` suffix | `SessionService`, `ComplianceService` |
| Repository class | PascalCase + `Repository` suffix | `SessionRepository` |
| Controller | PascalCase + `Controller` suffix | `SessionsController` |
| DTO | PascalCase + purpose suffix | `SessionCreateDto`, `SessionListDto` |
| Enum | PascalCase, singular | `SessionStatus`, `UserRole` |
| Private field | `_camelCase` | `_sessionRepository` |
| Local variable | `camelCase` | `activeSession` |
| Constant | `PascalCase` | `MaxRetryCount` |
| Test method | `Method_Scenario_Expected` | `Create_ValidInput_ReturnsCreated` |

### Project structure pattern

```
src/[Project].[Layer]/
├── [Module/Feature]/
│   ├── [Entity].cs
│   ├── [Entity]Service.cs
│   └── [Entity]Dto.cs
└── ...
```

### Error handling pattern

<!-- Define your project's error handling approach. Example: -->

```
// Example: Use Result pattern or exceptions? Define here.
// Example for exceptions:
- Validation errors → throw ValidationException (400)
- Not found → throw NotFoundException (404)
- Business rule violation → throw BusinessRuleException (409/422)
- Unexpected errors → let global handler catch (500)
- Log all errors with correlation ID
```

### General rules

- Align naming with domain terms in `docs/03-domain-and-data.md`
- Do not invent new names without documenting rationale
- Keep methods short (< 30 lines preferred)
- Prefer explicit code over magic/framework tricks
- Comments only when logic is non-obvious

## 2. Tests

### Test pyramid

```
        /  E2E  \          — Few, slow, high confidence
       / Integr. \         — Medium, test DB + API flows
      /   Unit    \        — Many, fast, test logic in isolation
```

### What to test at which layer

| Layer | Test type | What to test | Example |
|-------|-----------|-------------|---------|
| Domain entities | Unit | Validation rules, state transitions | `Session_Close_WithNoAttendees_Fails` |
| Services | Unit (mocked deps) | Business logic, orchestration | `SessionService_Create_SetsStatusToPlanned` |
| Repositories | Integration (real DB) | Queries, persistence, filtering | `GetByFilter_ActiveOnly_ReturnsActive` |
| API Controllers | Integration (WebAppFactory) | HTTP status codes, serialization, auth | `POST_Sessions_ValidInput_Returns201` |
| API Client | Unit (mocked HTTP) | Serialization, error handling | `GetSession_NotFound_ThrowsException` |
| UI Pages | E2E (browser) | User flows, navigation, forms | `SessionList_FilterByStatus_ShowsFiltered` |

### Test naming convention

`[MethodName]_[Scenario]_[ExpectedResult]`

Examples:
- `Create_ValidInput_ReturnsCreatedEntity`
- `Create_MissingName_ThrowsValidationException`
- `GetList_WithStatusFilter_ReturnsFilteredResults`
- `Delete_NonExistent_ThrowsNotFoundException`

### Minimum coverage per task

- Happy path (success case)
- At least one error/edge case
- For UI: at least the main flow E2E test

### Test data

- Use seeded fixtures with realistic data
- Scope test data by tenant/organization if multi-tenant
- Clean up test data after each test (or use transactions)

## 3. Working with docs

- **Always read** relevant docs before coding: `02-to-be.md`, `03-domain-and-data.md`, `04-architecture-and-interfaces.md`, `05-ui-structure.md`, `06-implementation-plan.md`
- **Keep docs and code in sync**: if behavior changes, update the corresponding doc in the same commit
- **If something is unclear, stop and ask** — do not guess
- **Keep changes small** and incremental; do not regenerate whole files
- **Align naming** with the domain doc — entity, field, and API naming must match

## 4. Boundaries

### Always (do these every time)

- Build before commit — `dotnet build` (or equivalent) must pass
- Run relevant tests before commit
- Update docs if behavior changed
- Reference file paths in your output
- Follow the dependency rules from `04-architecture-and-interfaces.md`
- Validate user input at system boundaries

### Ask first (stop and ask before doing these)

- Architectural changes (new layers, new projects, major refactors)
- Adding new external dependencies / NuGet packages / npm packages
- Database schema changes or new migrations
- Changing authentication or authorization logic
- Modifying CI/CD pipeline or deployment configuration
- Changing the public API contract (breaking changes)

### Never (do not do these)

- Skip tests for the code you wrote
- Delete or modify existing migrations
- Commit directly to main/production branch
- Store secrets, passwords, or API keys in source code
- Bypass authentication or authorization checks
- Introduce circular dependencies between projects
- Use `// TODO` without creating an issue for it

## 5. Output format

When reporting what you did:

- **Explain what changed and why** — reference file paths and line numbers
- **List tests run** — or state explicitly why tests were not run
- **Keep diffs focused** — do not refactor unrelated code
- **Do not dump full files** — summarize and point to key sections
- **List any new issues** discovered during implementation

## 6. Observability

### Logging

- **Framework**: [e.g., Serilog structured JSON / Winston / Python logging]
- **Request context**: log tenant ID, user ID, correlation ID for all requests
- **Error logging**: log full exception with stack trace, request context, and input (sanitized)
- **Audit events**: log all create/update/delete operations on [sensitive entities]

### Audit trail

- **What to audit**: [e.g., "All mutations to sessions, user roles, permissions"]
- **Retention**: [e.g., "30 days in database, then archived"]
- **Format**: [e.g., "Structured log entry with: who, when, what entity, what changed"]

### Health checks

- [e.g., "GET /health returns 200 if API and database are reachable"]
- [e.g., "Include database connectivity, external service availability"]
