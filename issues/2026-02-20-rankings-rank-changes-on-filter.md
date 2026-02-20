# Rank number changes when filtering — should show global rank

- **Type**: bug
- **Priority**: high
- **Status**: open

## Description

When filtering competitors on the Rankings page, the rank number displayed resets (e.g., starts from "1" for the first filtered result). The rank number should always show the competitor's global rank, not their position in the filtered list.

## Steps to reproduce

1. Navigate to the Rankings page
2. Apply a filter (e.g., search for a specific competitor)
3. Observe the rank number starts from 1

**Expected**: The rank number reflects the competitor's actual global ranking position.
**Actual**: The rank number reflects position within the filtered results.

## Where to look

- Rankings page component — how rank number is displayed
- API endpoint — whether it returns global rank or just list position

## Acceptance criteria

- [ ] Rank number always shows the competitor's global rank regardless of active filters
