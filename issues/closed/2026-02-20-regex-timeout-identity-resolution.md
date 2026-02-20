# Add Regex timeout in IdentityResolutionService.IsWordBoundaryContainment

- **Type**: improvement
- **Priority**: low
- **Status**: resolved

## Description

The `IsWordBoundaryContainment` method in `IdentityResolutionService.cs` (line ~486) uses `Regex.IsMatch` with lookahead/lookbehind patterns on data sourced from CSV imports. While `Regex.Escape` is used to prevent regex injection, the .NET regex engine can still exhibit degraded performance on certain pathological inputs with lookaround patterns.

The method is only called during CLI import (not web-facing), and the input is already normalized (lowercased, diacritics stripped), so the practical risk is very low. However, best practice for .NET is to always specify a `RegexOptions.None` with a `TimeSpan` timeout, or use `Regex` constructor with timeout, when processing external input.

## Where to look

- `src/AdwRating.Service/IdentityResolutionService.cs` line ~486 (`IsWordBoundaryContainment`)
- `src/AdwRating.Domain/Helpers/NameNormalizer.cs` — also uses `Regex.Replace` and `Regex.Match` but with static patterns only (no user-derived content in pattern), so these are safe.

## Acceptance criteria

- [ ] `Regex.IsMatch` in `IsWordBoundaryContainment` uses a timeout (e.g., `TimeSpan.FromMilliseconds(100)`)
- [ ] Consider using compiled Regex or `Regex.IsMatch` overload with `matchTimeout` parameter

## Notes

This is a defense-in-depth measure. The actual risk is very low because:
1. The method is only called during CLI import, not from web requests
2. Input is already normalized before reaching the regex
3. `Regex.Escape` prevents special characters from being interpreted as regex operators
4. The pattern structure (`(?<!\w)..escaped..(?!\w)`) is simple and not prone to catastrophic backtracking

## Resolution

Added `TimeSpan.FromMilliseconds(100)` timeout parameter to `Regex.IsMatch` call in `IsWordBoundaryContainment`. This is a defense-in-depth measure; the practical risk was already very low.

**Files changed:**
- `src/AdwRating.Service/IdentityResolutionService.cs` line 573 -- added `TimeSpan.FromMilliseconds(100)` to `Regex.IsMatch`
