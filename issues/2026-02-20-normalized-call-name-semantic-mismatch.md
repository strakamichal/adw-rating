# NormalizedCallName contains registered names — semantic mismatch

- **Type**: bug
- **Priority**: high
- **Status**: open

## Description

`Dog.NormalizedCallName` is supposed to be the normalized version of `Dog.CallName`, but in practice it contains three different things depending on the import path:

1. **Normalized call name** — when `ExtractCallName` succeeds (e.g. `"Daylight Neverending Force (Day)"` → `NormalizedCallName = "day"`). Correct.
2. **Normalized full name** — when extraction fails and name has < 3 words (e.g. `"Buddy"` → `NormalizedCallName = "buddy"`). Acceptable.
3. **Normalized registered name** — when extraction fails and name has 3+ words (e.g. `"Daylight Neverending Force"` → `CallName = ""`, `NormalizedCallName = "daylight neverending force"`). **Wrong** — `NormalizedCallName` holds the registered name, not the call name.

Case 3 is intentional for matching purposes (line 410: "NormalizedCallName keeps full name for matching"), but it breaks the semantic contract of the field. Queries and UI code that read `NormalizedCallName` expecting a call name will get a registered name instead.

Additionally, `RegisteredName` has no normalized counterpart. During fuzzy matching it's normalized on-the-fly (line 322 of `IdentityResolutionService.cs`), which is both wasteful and inconsistent.

## Where to look

- `src/AdwRating.Service/IdentityResolutionService.cs` lines 405-427 — new dog creation with the 3-word heuristic
- `src/AdwRating.Service/IdentityResolutionService.cs` lines 320-322 — on-the-fly normalization of RegisteredName during fuzzy match
- `src/AdwRating.Service/IdentityResolutionService.cs` lines 535-577 — `BackfillDogNames` updates CallName/NormalizedCallName but not RegisteredName normalization
- `src/AdwRating.Domain/Entities/Dog.cs` — entity definition
- `src/AdwRating.Domain/Helpers/NameNormalizer.cs` — `ExtractCallName` logic

## Proposed fix

1. Add `NormalizedRegisteredName` column to `Dog` entity
2. Ensure `NormalizedCallName` always reflects `CallName` (empty when CallName is empty)
3. Use `NormalizedRegisteredName` for matching when call name is unknown
4. Update identity resolution to populate both normalized fields consistently
5. Update `BackfillDogNames` to maintain both normalized fields
6. Backfill existing data (migration or recalc step)

## Acceptance criteria

- [ ] `NormalizedCallName` is always `NameNormalizer.Normalize(CallName)` (or empty when CallName is empty)
- [ ] `NormalizedRegisteredName` exists and is populated when `RegisteredName` is set
- [ ] Identity resolution uses both normalized fields for matching
- [ ] Fuzzy matching no longer normalizes RegisteredName on-the-fly
- [ ] Existing data is migrated/backfilled
- [ ] All existing tests pass, new tests cover the normalized field consistency

## Notes

Discovered during manual DB inspection: `NormalizedCallName` column visibly contains registered names for many dogs (long multi-word names where CallName is empty).
