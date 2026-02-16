# Project Documentation Template

A reusable documentation framework for software projects developed with AI coding agents (Claude Code, etc.). Born from real-world experience building a .NET enterprise application, refined with best practices from [GitHub Spec Kit](https://github.com/nicholasgriffintn/github-spec-kit) and Addy Osmani's AI-assisted development workflows.

## What this is

A structured set of document templates that guide you (and your AI agent) through analyzing, designing, and implementing a software project. The templates enforce a proven phased approach:

1. **AS-IS analysis** — understand what exists today
2. **Vision & scope** — define where you're going
3. **Domain model** — nail down entities, rules, relationships
4. **Architecture** — layers, APIs, deployment
5. **UI structure** — screens, flows, access control
6. **Implementation plan** — phased tasks with dependencies
7. **AI guidelines** — coding style, tests, boundaries

## Three scenarios

| Scenario | Description | Phases to use |
|----------|-------------|---------------|
| **Greenfield** | New project from scratch | Skip Phase 1 (AS-IS). Start at Phase 2. |
| **Rewrite** | Replacing an existing application | Full workflow, Phase 1-7. |
| **Major feature** | Adding significant functionality to an existing project | Phase 1 scoped to affected areas only. Phase 2-7 scoped to the feature. |

See [`docs/_workflow.md`](docs/_workflow.md) for detailed routing and per-phase instructions.

## Quick start

1. **Fork or copy** this repository into your project
2. **Read** [`docs/_workflow.md`](docs/_workflow.md) — identify your scenario and which phases apply
3. **Customize** `CLAUDE.md` with your project-specific architecture rules and conventions
4. **Start your AI agent** on Phase 1 (or Phase 2 for greenfield) — the workflow file guides it through each phase
5. **Review each phase output** before moving to the next — the agent will stop and ask when unclear

## File structure

```
├── README.md                              # This file
├── CLAUDE.md                              # AI agent instructions (customize per project)
├── docs/
│   ├── _workflow.md                       # Master workflow with routing per scenario
│   ├── 01-as-is.md                        # AS-IS analysis template
│   ├── 01-as-is/
│   │   └── _module-template.md            # Per-module AS-IS detail template
│   ├── 02-to-be.md                        # Vision & scope template
│   ├── 03-domain-and-data.md              # Domain model template
│   ├── 04-architecture-and-interfaces.md  # Architecture & API template
│   ├── 05-ui-structure.md                 # UI structure template
│   ├── 06-implementation-plan.md          # Implementation plan template
│   └── 07-ai-guidelines.md               # AI coding guidelines template
└── issues/
    └── _template.md                       # Issue tracking template
```

## How to use the templates

Each template file contains:
- **Section headers** with descriptions of what goes there
- **Placeholder text** in `[brackets]` to replace with your content
- **AI agent instructions** in `<!-- comments -->` guiding the agent on what to analyze and how to fill in the section
- **Done criteria** so you know when a phase is complete
- **Acceptance criteria** templates for entities and screens

### Tips

- **Work incrementally**: one phase at a time, review before moving on
- **Protection sections** in `02-to-be.md` define what the agent must never do — fill these in early
- **Keep docs and code in sync**: if behavior changes during implementation, update the docs in the same commit
- **The agent should stop and ask** when requirements are unclear, not guess

## Inspirations and references

- [GitHub Spec Kit](https://github.com/nicholasgriffintn/github-spec-kit) — structured specification templates for AI-assisted development
- [Addy Osmani: AI-Assisted Development](https://addyosmani.com/blog/ai-assisted-development/) — practical patterns for working with AI coding agents
- Real-world experience from building TrainingManagement v2 (.NET 10, Blazor, PostgreSQL)

## License

Use freely. Adapt to your needs. No attribution required.
