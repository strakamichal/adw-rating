# All competitors show "NEW" badge

- **Type**: bug
- **Priority**: medium
- **Status**: resolved

## Description

Every competitor in the rankings displays a "NEW" badge/label. This should only appear for competitors who recently entered the ranking system.

## Where to look

- Rankings page component — NEW badge logic
- API — how "new" status is determined
- Rating engine — when a competitor is considered "new" vs established

## Acceptance criteria

- [x] "NEW" badge only appears for genuinely new competitors (e.g., those with very few rated runs or recently added)

## Resolution

`RankingsController` was hardcoding `PrevRank: null` for every team, causing `TrendMarkup` to always show "NEW" (it shows NEW when PrevRank is null). Fixed as part of the global ranks fix: `GetGlobalRanksAsync` now computes previous ranks by ordering teams by `PrevRating`. Teams with a valid PrevRating get a PrevRank; only truly new teams (no PrevRating) show "NEW".
