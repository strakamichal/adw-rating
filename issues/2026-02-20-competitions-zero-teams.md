# All competitions show "0 teams"

- **Type**: bug
- **Priority**: high
- **Status**: open

## Description

On the competition list page, every competition displays "0 teams" even though they should have associated teams/entries.

## Steps to reproduce

1. Navigate to the Competitions page
2. Observe that all competitions show "0 teams"

**Expected**: Competitions show the actual number of participating teams.
**Actual**: All show "0 teams".

## Where to look

- Competition list API endpoint — team count query
- `src/AdwRating.Api/Controllers/` — competition controller
- `src/AdwRating.Service/` — competition service, team count logic

## Acceptance criteria

- [ ] Each competition displays the correct number of teams
