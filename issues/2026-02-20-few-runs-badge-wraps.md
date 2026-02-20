# "FEW RUNS" badge wraps to next line under dog class

- **Type**: bug
- **Priority**: low
- **Status**: open

## Description

When a competitor has the "FEW RUNS" indicator, the text wraps to a new line below their dog class instead of staying inline.

## Steps to reproduce

1. Navigate to Rankings page
2. Find a competitor with "FEW RUNS" label
3. Observe the label wraps below the dog class column

**Expected**: "FEW RUNS" stays inline or is properly contained.
**Actual**: Text wraps to next line.

## Where to look

- Rankings page component — badge/label styling
- CSS for the competitor row layout

## Acceptance criteria

- [ ] "FEW RUNS" badge does not wrap to next line; layout stays clean
