# Headings show focus outline box in Safari

- **Type**: bug
- **Priority**: low
- **Status**: open

## Description

In Safari, headings (and possibly other non-interactive elements) display a focus outline/border when clicked or interacted with. This appears to be an accessibility focus ring appearing on non-interactive elements, which is visually distracting.

## Steps to reproduce

1. Open the site in Safari
2. Click on or near heading elements
3. Observe a rectangular outline/border appearing around them

## Where to look

- Global CSS — focus styles, outline resets
- May need `:focus-visible` instead of `:focus` or `outline: none` on non-interactive elements

## Acceptance criteria

- [ ] Non-interactive headings do not show focus outlines in Safari
- [ ] Accessibility is preserved for keyboard navigation on interactive elements
