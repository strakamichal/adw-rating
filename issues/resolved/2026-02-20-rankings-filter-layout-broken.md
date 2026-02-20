# Rankings page filter is stacked vertically instead of horizontal

- **Type**: bug
- **Priority**: medium
- **Status**: open

## Description

The filter controls on the Rankings page are stacked vertically (one under another) instead of being laid out horizontally in a row.

## Steps to reproduce

1. Navigate to the Rankings page
2. Observe the filter controls are stacked vertically

**Expected**: Filters are aligned horizontally in a single row.
**Actual**: Filters are stacked vertically.

## Where to look

- `src/AdwRating.Web/Components/Pages/` — Rankings page component
- CSS/layout for the filter section

## Acceptance criteria

- [ ] Filter controls on Rankings page are laid out horizontally
