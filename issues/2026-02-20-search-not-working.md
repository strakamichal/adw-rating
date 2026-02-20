# Search does not work (navbar + homepage)

- **Type**: bug
- **Priority**: high
- **Status**: resolved

## Description

Neither the search in the navigation menu nor the large search input on the homepage produces any results or navigates anywhere.

## Steps to reproduce

1. Navigate to the homepage
2. Type a competitor or team name into the large search field
3. Press Enter or click search
4. Nothing happens

Same behavior with the navbar search.

## Where to look

- `src/AdwRating.Web/Components/` — search-related components
- `src/AdwRating.Api/Controllers/` — search API endpoint
- Global search page implementation

## Acceptance criteria

- [x] Navbar search navigates to search results page with query
- [x] Homepage search navigates to search results page with query
- [x] Results are displayed correctly

## Resolution

The homepage search input was a raw `<input>` element not wrapped in the `.search-wrapper` div that `search.js` requires. Replaced with the `<SearchDropdown>` component (same as navbar) which provides the correct wrapper structure for the JS-based live search dropdown.
