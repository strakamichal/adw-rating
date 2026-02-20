# Handler unique constraint on (NormalizedName, Country) may be too strict

- **Type**: improvement
- **Priority**: medium
- **Status**: resolved

## Description

The spec says: "Two handlers with different countries but same name are separate entities." The EF config enforces a unique index on `(NormalizedName, Country)`.

However, the identity resolution service includes a country-agnostic fallback (line 63-74 of `IdentityResolutionService.cs`) that matches a handler across countries if there is exactly one match. This means the system intentionally resolves handlers across country boundaries in some cases, but the unique constraint prevents two handlers with the same normalized name in the same country.

This is actually correct per spec, but worth noting: the country-agnostic fallback in identity resolution could silently fail the unique constraint if two handlers with the same name exist in different countries and a third import comes with yet another country. The current implementation handles this correctly (only matches when count == 1).

No action needed, but the interaction between the constraint and the fallback logic should be documented.

## Where to look

- `src/AdwRating.Data.Mssql/Configurations/HandlerConfiguration.cs` line 19
- `src/AdwRating.Service/IdentityResolutionService.cs` lines 60-74

## Acceptance criteria

- [x] Verify this edge case is covered by tests

## Resolution

The unique constraint on (NormalizedName, Country) is **correct per spec** -- handlers ARE unique per name+country. The country-agnostic fallback in `IdentityResolutionService` is already properly guarded (only matches when exactly 1 result across all countries).

The edge cases are well-covered by existing tests in `IdentityResolutionServiceTests.cs`:
- `ResolveHandlerAsync_CountryMismatch_SingleNameOnlyMatch_ReturnsFallback` (line 130) -- single cross-country match returns fallback
- `ResolveHandlerAsync_SingleTokenName_SkipsCountryFallback` (line 152) -- short names skip fallback
- `ResolveHandlerAsync_CountryFallback_MultipleMatches_CreatesNew` (line 175) -- multiple cross-country matches create new handler instead

No code changes needed.

## Notes

Low priority observation, not a blocking issue.
