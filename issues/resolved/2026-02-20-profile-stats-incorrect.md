# Profile stats appear incorrect (finish rate, podium rate, AVG RANK)

- **Type**: bug
- **Priority**: high
- **Status**: resolved

## Description

On competitor profile pages, the percentage statistics seem incorrect:
- **Finish rate** — value doesn't look right
- **Podium rate** — value doesn't look right
- **AVG RANK** — unclear what this metric means; needs clarification or removal

## Steps to reproduce

1. Navigate to any competitor's profile page
2. Check the statistics section
3. Compare finish rate and podium rate against their actual competition history

## Where to look

- Competitor profile page component
- API endpoint that provides competitor statistics
- Service layer — how finish rate, podium rate, and avg rank are calculated

## Acceptance criteria

- [x] Finish rate is calculated correctly (finished runs / total runs)
- [x] Podium rate is calculated correctly (top-3 finishes / total competitions)
- [x] AVG RANK is clearly defined or removed if not meaningful

## Resolution

`TeamProfileService` was computing `finishedPct` and `top3Pct` as fractions (0.0-1.0) but the UI displays them with `ToString("0")%`, expecting 0-100 values. Multiplied by 100 in the service. AVG RANK is the average finishing position across all completed (non-eliminated) runs, which is a meaningful metric -- left as-is.
