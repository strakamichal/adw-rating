# IHandlerRepository and IDogRepository have extra methods not in spec

- **Type**: task
- **Priority**: low
- **Status**: resolved

## Description

The implementation added methods to repository interfaces that are not in the spec:

**IHandlerRepository** extras (vs `docs/04-architecture-and-interfaces.md`):
- `FindByNormalizedNameAsync(string normalizedName)` -- country-agnostic lookup
- `FindByNormalizedNameContainingAsync(string normalizedName, string country)` -- containment/substring match

**IDogRepository** extras:
- `FindAllByNormalizedNameAndSizeAsync(string normalizedCallName, SizeCategory size)` -- returns list instead of single

**ITeamRepository** extras:
- `GetByDogIdAsync(int dogId)` -- needed for merge operations

These are all legitimate additions needed by the identity resolution and merge services. The spec should be updated to reflect them.

## Where to look

- `src/AdwRating.Domain/Interfaces/IHandlerRepository.cs`
- `src/AdwRating.Domain/Interfaces/IDogRepository.cs`
- `src/AdwRating.Domain/Interfaces/ITeamRepository.cs`
- `docs/04-architecture-and-interfaces.md` section 3

## Acceptance criteria

- [x] Update spec to include the extra methods

## Resolution

Updated `docs/04-architecture-and-interfaces.md` section 3 to add the following methods that existed in code but were missing from the spec:

- **IHandlerRepository**: Added `FindByNormalizedNameAsync(string normalizedName)` (country-agnostic lookup) and `FindByNormalizedNameContainingAsync(string normalizedName, string country)` (substring match). Both are used by identity resolution for fuzzy handler matching.
- **IDogRepository**: Added `FindAllByNormalizedNameAndSizeAsync(string normalizedCallName, SizeCategory size)` (returns list instead of single). Used when multiple dogs match the same normalized name and size, allowing identity resolution to disambiguate.
- **ITeamRepository**: Added `GetByDogIdAsync(int dogId)` (returns teams for a dog). Needed by merge operations to reassign teams when merging dog records.

## Notes

These are docs-vs-code sync issues, not bugs.
