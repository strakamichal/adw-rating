# CLAUDE – Development workflow

You are an AI coding agent with READ/WRITE access to this repository.

## Primary goal

<!-- ✏️ CUSTOMIZE: Replace with your project's goal. -->

Implement the [Project Name] system incrementally according to the target docs, starting with Phase 1 in `docs/06-implementation-plan.md`.

## Source of truth

- Functional scope and decisions: `docs/02-to-be.md`
- Domain model: `docs/03-domain-and-data.md`
- Architecture/API: `docs/04-architecture-and-interfaces.md`
- UI structure: `docs/05-ui-structure.md`
- Implementation phases: `docs/06-implementation-plan.md`
- Coding/test rules: `docs/07-ai-guidelines.md`

<!-- ✏️ CUSTOMIZE: Add project-specific doc references if needed: -->
<!-- - Component library docs: `docs/[library]/` (start with `quick-reference.md`) -->
<!-- - API specification: `docs/api/openapi.yaml` -->

## How to work

- Work **incrementally**: one small, valuable change per run.
- Follow the **phase order** in `docs/06-implementation-plan.md` unless explicitly told otherwise.
- Keep **code and docs in sync**. If behavior changes, update the relevant doc in the same change set.
- If requirements are unclear, **stop and ask** instead of guessing.
- Prefer explicit, simple implementations over clever abstractions.
- When a task from `docs/06-implementation-plan.md` is completed, **check it off** and add a brief note if needed.

## Quality bar

- Tests: follow `docs/07-ai-guidelines.md` (unit/integration/E2E expectations).
- Observability: log request context and audit sensitive mutations as specified.
<!-- ✏️ CUSTOMIZE: Add your critical security/quality requirements: -->
- Security: tenant isolation (`TenantId`) is mandatory for all queries.

## Architecture rules (MUST follow)

<!-- ✏️ CUSTOMIZE: Replace this example with your actual architecture. The key is to
     define explicit dependency rules so the agent never violates layer boundaries. -->

**Clean architecture – data layer must be replaceable:**

```
Api, Web, Worker, Service  ──►  Domain (interfaces)  ◄──  Data.PostgreSql (implements)
```

| Project | Can depend on | CANNOT depend on |
|---------|---------------|------------------|
| **Domain** | nothing | anything else |
| **Service** | Domain | Data.*, Api, Web |
| **Data.PostgreSql** | Domain | Service, Api, Web |
| **Api** | Domain, Service | — |
| **Web** | Domain, ApiClient | Service, Data.* |
| **Worker** | Domain, Service | — |
| **ApiClient** | Domain | Service, Data.* |

**Key rules:**
1. **Never use `AppDbContext` or any `Data.*` types outside of `Data.PostgreSql`** (except DI registration in `Program.cs`)
2. **All data access goes through repository interfaces** defined in `Domain/Interfaces/`
3. **Every entity that needs direct querying must have an `I*Repository`** interface in Domain and implementation in Data.PostgreSql
4. **Service layer depends only on interfaces**, never on concrete implementations

This allows swapping `Data.PostgreSql` for `Data.SqlServer` or `Data.InMemory` without changing any other code.

## Running tests selectively

Don't run all tests every time — run only what's relevant to your changes:

<!-- ✏️ CUSTOMIZE: Replace test project names and filter patterns with your actual projects. -->

| Changed layer | Run these tests |
|---------------|-----------------|
| Domain entities / DB config | `dotnet test tests/MyApp.IntegrationTests/ --filter "FullyQualifiedName~Entities"` |
| Repository | `dotnet test tests/MyApp.IntegrationTests/ --filter "FullyQualifiedName~Repositories"` |
| Service | `dotnet test tests/MyApp.Tests/` (unit tests) |
| API Controller | `dotnet test tests/MyApp.IntegrationTests/ --filter "FullyQualifiedName~Controllers"` |
| Web UI | `dotnet test tests/MyApp.E2ETests/` (E2E tests — **required** for any UI change) |
| Full regression | `dotnet test` (only before PR or when unsure) |

API integration tests start a full WebApplicationFactory — avoid running them when you only changed entities or services.

## Output expectations

- Explain what changed and why (reference file paths).
- List tests run (or state why not).
- Keep diffs focused; avoid refactors unrelated to the task.

## When starting a coding task

1. Read the relevant target docs.
2. Identify the smallest task in the current phase.
3. Implement it with tests if required.
4. Update docs if any behavior changed.
5. Report results clearly.
6. Always attempt a build before finishing a task, and report the result (success or failure).
7. After every successful build and any tests, commit the changes (if the change is a logical, self-contained step).

## Issue Tracking

Issues (bugs, feedback, small tasks) live in the `issues/` directory:
- One file per issue: `issues/YYYY-MM-DD-slug.md` (e.g., `issues/2026-02-10-broken-login.md`)
- Template: `issues/_template.md`

**Issue lifecycle (folder-based):**
- `issues/` — open issues
- `issues/resolved/` — agent resolved, waiting for user review
- `issues/closed/` — user verified and closed

**When assigned an issue:**
1. Read the issue file
2. Update status to `in-progress`
3. Create branch: `fix/YYYY-MM-DD-slug`
4. Fix, write tests, commit
5. **Write resolution into the issue file** — add a `## Resolution` section describing what was changed and why (files modified, approach taken).
6. Move issue file to `issues/resolved/` (NOT `issues/closed/` — that is done by the user after review)
7. Switch back to the ready branch (if using multi-agent workflow)

**When you discover a bug during other work:**
- Create a new issue file using today's date and a descriptive slug
- If multiple issues on the same day, append a suffix: `YYYY-MM-DD-slug-2.md`
- Continue with your original task
- Report the new issue to the user

**Naming:** Use the current date (YYYY-MM-DD) + descriptive slug. Check existing files to avoid duplicates.

**Branch naming:** `fix/YYYY-MM-DD-slug` matching the issue filename (without `.md`).

## Multi-Agent Workflow

<!-- ✏️ CUSTOMIZE: If you use a single agent, delete this entire section.
     If you use multiple agents, customize the paths and branch names below. -->

This project supports parallel development with multiple Claude Code agents.
Each agent runs in its own git worktree in a separate terminal.

### Worktree Location

<!-- ✏️ CUSTOMIZE: Replace paths with your actual repo and agent locations. -->

Worktrees are at `../agents/agent-N/` relative to the main repo:
- **Main repo**: `~/projects/my-app/` (main branch, for user review/merge)
- **Agents**: `~/projects/agents/agent-1/`, `agent-2/`, `agent-3/`

### Managing worktrees

```bash
# List all worktrees
git worktree list

# Add new agent worktree
git worktree add ../agents/agent-N -b agent-N/ready main

# Remove agent worktree (after cleanup)
git worktree remove ../agents/agent-N
```

### Agent behavioral rules

1. **One task = one feature branch.** Create from main: `git checkout -b feat/<task-id>-<slug> main`
2. **Never commit directly to main.** All changes via feature branches.
3. **Build and test before committing**: run build command, then relevant tests.
4. **Small, focused commits.** Each commit should be a logical unit.
5. **Report when done**: what changed, how to verify, any blockers.
6. **If blocked >15 min**: describe the issue, save work on branch, ask for instructions or move to next task.
7. **After finishing a task, switch back to the ready branch**: `git checkout agent-N/ready`. This frees the feature branch so it can be merged and deleted without worktree conflicts.

### Branch naming convention

- Features: `feat/<task-id>-<slug>` (e.g., `feat/1.10.2-groups-crud`)
- Fixes (issues): `fix/YYYY-MM-DD-slug` (e.g., `fix/2026-02-10-broken-login`)
- Fixes (ad-hoc): `fix/<description>` (e.g., `fix/navbar-link-order`)

### Shared files (coordinate!)

<!-- ✏️ CUSTOMIZE: List files that multiple agents commonly edit. -->

These files are commonly edited across tasks. Only one agent should modify at a time:
- `src/[Web]/Components/Layout/NavMenu.razor` (navigation links)
- `docs/06-implementation-plan.md` (task checkoff)

After another agent's branch is merged to main, rebase your branch: `git rebase main`

### Merge flow (done by orchestrator or user in main repo)

```bash
cd ~/projects/my-app
git merge --no-ff feat/<branch-name>
```

If there are **merge conflicts**: resolve them manually, verify the build passes, then commit the merge. If conflicts are complex, ask the agent who wrote the code for guidance.

### Cleanup after merge

When asked to clean up merged branches:
1. Run `git branch --merged main | grep feat/` to find merged feature branches
2. For each merged branch that has a worktree (`git worktree list`):
   - Remove the worktree: `git worktree remove ../agents/agent-N`
3. Delete the merged branches: `git branch -d feat/<branch-name>`
4. Report what was cleaned up and what remains

### Starting work as an agent

1. Check which branch you're on: `git branch`
2. **Starting a NEW task**: always create branch from main: `git checkout -b feat/<task-id>-<slug> main`
   - This automatically includes all changes merged to main since your last task
3. **Continuing an existing task**: stay on your current feature branch
4. Ask the user what task to work on if unclear

### Orchestrator Role (main repo agent)

The agent running in the main repo (on the `main` branch) acts as the **orchestrator**. It does not write code directly — it manages sub-agents.

#### How it works
1. **Pick task** — from `docs/06-implementation-plan.md` or user instruction
2. **Choose available agent** — one on `agent-N/ready` branch (not busy)
3. **Launch sub-agent** — one task at a time per agent, wait for completion
4. **Verify result** — check branch, build, tests in the worktree
5. **Merge into main** — `git merge --no-ff feat/<branch-name>`
6. **Update docs** — check off task in implementation plan if needed
7. **Assign next task** — to same or different agent

#### Launching a sub-agent

Default tool: **Claude Code** (`claude`).

The task description must be self-contained — the agent runs in print mode and cannot ask follow-up questions. Include:
- **What to implement** (specific acceptance criteria)
- **Which docs to read** (e.g., "Read `docs/03-domain-and-data.md` section X")
- **Which files to modify** (if known)

```bash
# Example: launching a sub-agent
claude -p "Implement CRUD for Products entity.
Read docs/03-domain-and-data.md for the domain model.
Read docs/04-architecture-and-interfaces.md for API contract.
Create: Domain entity, IProductRepository, EF config, repository impl, service, controller.
Write integration tests for the repository and controller.
Follow architecture rules in CLAUDE.md." \
  --cwd ~/projects/agents/agent-N/
```

Use Bash tool's `run_in_background` for long-running tasks.

#### When a sub-agent fails

- **Build failure**: check the error, decide if it's a simple fix (re-launch with fix instructions) or a design issue (adjust the task).
- **Test failure**: review which tests fail and why. Re-launch with specific fix instructions or fix manually.
- **Agent reports blocker**: read the agent's output, resolve the blocker (e.g., missing interface, unclear requirement), then re-launch.
- **Agent times out or produces no useful output**: check the branch state, salvage any partial work, re-launch with a simplified task.

#### Monitoring agents

```bash
# Check agent's current branch (feat/* = busy, agent-N/ready = idle/done)
git -C ~/projects/agents/agent-N/ branch

# Check recent commits
git -C ~/projects/agents/agent-N/ log --oneline -5
```

#### After merge
- Agent creates new branch from main for next task, so it automatically gets latest code
- Clean up merged feature branches when appropriate (see "Cleanup after merge")
