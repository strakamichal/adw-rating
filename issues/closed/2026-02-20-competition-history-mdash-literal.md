# Competition history shows literal "&mdash;" instead of em dash

- **Type**: bug
- **Priority**: medium
- **Status**: open

## Description

In the "Competition history" section on competitor profiles, the columns for faults, time, and speed sometimes display the literal string `&mdash;` instead of rendering an actual em dash (—) character.

## Steps to reproduce

1. Navigate to a competitor's profile page
2. Scroll to Competition history section
3. Look at entries with missing fault/time/speed data
4. Observe literal `&mdash;` text instead of — character

**Expected**: An em dash character (—) or a dash is rendered.
**Actual**: The literal HTML entity `&mdash;` is shown as text.

## Where to look

- Competitor profile page component — competition history table rendering
- Check if Blazor is escaping the HTML entity; use the actual Unicode character (—) instead

## Acceptance criteria

- [ ] Missing values display a proper em dash or dash character, not the HTML entity string
