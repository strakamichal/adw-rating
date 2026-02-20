# Profile stats appear incorrect (finish rate, podium rate, AVG RANK)

- **Type**: bug
- **Priority**: high
- **Status**: open

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

- [ ] Finish rate is calculated correctly (finished runs / total runs)
- [ ] Podium rate is calculated correctly (top-3 finishes / total competitions)
- [ ] AVG RANK is clearly defined or removed if not meaningful
