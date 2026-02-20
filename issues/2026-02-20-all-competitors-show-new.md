# All competitors show "NEW" badge

- **Type**: bug
- **Priority**: medium
- **Status**: open

## Description

Every competitor in the rankings displays a "NEW" badge/label. This should only appear for competitors who recently entered the ranking system.

## Where to look

- Rankings page component — NEW badge logic
- API — how "new" status is determined
- Rating engine — when a competitor is considered "new" vs established

## Acceptance criteria

- [ ] "NEW" badge only appears for genuinely new competitors (e.g., those with very few rated runs or recently added)
