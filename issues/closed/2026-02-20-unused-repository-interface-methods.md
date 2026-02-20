# Several repository interface methods are never called

- **Type**: improvement
- **Priority**: low
- **Status**: resolved

## Description

The following repository interface methods are defined and implemented but never called from any service or command code:

1. **`IDogRepository.FindByNormalizedNameAndSizeAsync`** (singular) -- Only `FindAllByNormalizedNameAndSizeAsync` (plural) is used in `IdentityResolutionService`.
2. **`IRunRepository.GetByCompetitionAndRoundKeyAsync`** -- Not called anywhere.
3. **`IRunResultRepository.GetByRunIdAsync`** (singular) -- Only `GetByRunIdsAsync` (plural) is used.
4. **`IRunResultRepository.GetByTeamIdAsync`** -- Not called from any service code.

These may be intended for future API endpoints or services (`IRankingService`, `ITeamProfileService`, `ISearchService` -- which also have no implementations yet). If so, they should be documented as "reserved for Phase N" so they aren't mistaken for dead code.

## Where to look

- `src/AdwRating.Domain/Interfaces/IDogRepository.cs` line 9
- `src/AdwRating.Domain/Interfaces/IRunRepository.cs` line 8
- `src/AdwRating.Domain/Interfaces/IRunResultRepository.cs` lines 7, 9
- Corresponding implementations in `src/AdwRating.Data.Mssql/Repositories/`

## Acceptance criteria

- [x] Either remove unused methods, or add a comment indicating which phase/feature they are reserved for
- [x] Build passes

## Resolution

Removed the following unused methods from interfaces and their implementations:
- `IDogRepository.FindByNormalizedNameAndSizeAsync` (singular) - only the plural `FindAll` variant is used
- `IRunRepository.GetByCompetitionAndRoundKeyAsync` - no callers
- `IRunResultRepository.GetByRunIdAsync` (singular) - only the plural `GetByRunIdsAsync` is used
- `IRunResultRepository.GetByTeamIdAsync` - no callers

Also removed `Domain/Helpers/LevenshteinDistance.cs` which had no callers anywhere in the codebase.

These can be re-added when API endpoints or future services need them.

## Notes

The unimplemented service interfaces (`ISearchService`, `IRankingService`, `ITeamProfileService`) are clearly forward-looking and do not need immediate action, but the orphaned repository methods should be reconciled.
