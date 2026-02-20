# Imported event should not be visible until calculated

- **Type**: improvement
- **Priority**: high
- **Status**: open

## Description

When an event is imported, it becomes immediately visible in the UI (listings, ratings). This is wrong — the event data needs to be cleaned up first (handler bindings resolved) and the rating engine needs to recalculate before the event should appear publicly.

Currently the flow is:
1. Import event → event is immediately visible with raw/unprocessed data

The desired flow is:
1. Import event → event is in a "staging" state (not visible in public UI)
2. Clean up data — resolve handler bindings/aliases
3. Run rating recalculation to incorporate the new event
4. Only then make the event visible

## Acceptance criteria

- [ ] Imported events are not shown in public listings or rating calculations until explicitly published
- [ ] There is a staging/draft state for events that distinguishes them from published ones
- [ ] Handler bindings can be reviewed and cleaned up before publishing
- [ ] Rating recalculation incorporates the event only after it is published
- [ ] Admin CLI provides commands to review staged events and publish them

## Notes

This likely requires a status field on the Event entity (e.g., `Draft` / `Published`) and filtering in queries and the rating engine.
