# "See all teams" link doesn't work for handlers with multiple dog sizes

- **Type**: bug
- **Priority**: medium
- **Status**: open

## Description

On a competitor's profile, the "See all teams by [Handler Name]" link navigates to a page with a filter by handler name. This approach doesn't work correctly when a handler has dogs of different sizes, because the filter likely matches by exact team or only works within one category.

## Steps to reproduce

1. Navigate to a competitor profile where the handler has multiple dogs of different sizes
2. Click "See all teams by [Handler Name]"
3. Observe that not all teams are shown, or the filter doesn't work across categories

**Expected**: All teams associated with the handler are shown regardless of dog size.
**Actual**: Filter doesn't properly handle multiple dog sizes.

## Where to look

- Profile page — "See all teams" link target and query parameters
- The target page — how it filters by handler
- Consider linking to a handler-specific page or adjusting the filter to work across categories

## Acceptance criteria

- [ ] Clicking "See all teams" shows all teams for that handler across all dog size categories
