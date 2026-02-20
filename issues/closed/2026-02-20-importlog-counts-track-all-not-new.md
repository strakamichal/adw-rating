# ImportLog entity counts track all resolved entities, not just newly created ones

- **Type**: bug
- **Priority**: medium
- **Status**: resolved

## Description

The spec (`docs/03-domain-and-data.md`) defines ImportLog fields as:
- `NewHandlersCount` -- Number of newly created handlers
- `NewDogsCount` -- Number of newly created dogs
- `NewTeamsCount` -- Number of newly created teams

The implementation in `ImportService.cs` tracks all *distinct* handlers/dogs/teams encountered during import, not just newly created ones:

```csharp
allHandlerIds.Add(handler.Id);   // Adds ID regardless of whether handler was new or existing
allDogIds.Add(dog.Id);
allTeamIds.Add(team.Id);
```

This means a re-import of data with all existing handlers/dogs/teams would report them all as "new" in the ImportLog, which is misleading.

## Where to look

- `src/AdwRating.Service/ImportService.cs` lines 165-183, 229-231

## Acceptance criteria

- [ ] ImportLog should accurately distinguish between newly created and existing entities
- [ ] Either track creation counts separately, or rename the fields to reflect total resolved counts

## Resolution

Changed `IIdentityResolutionService` resolve methods to return `(T entity, bool IsNew)` tuples instead of just the entity. Each resolve method now returns `IsNew = true` only when it creates a new entity (step 5 in handler/dog/team resolution). ImportService uses these flags to accurately count only newly created entities.

**Files changed:**
- `src/AdwRating.Domain/Interfaces/IIdentityResolutionService.cs` -- return types changed to tuples
- `src/AdwRating.Service/IdentityResolutionService.cs` -- all return statements updated with IsNew flag
- `src/AdwRating.Service/ImportService.cs` -- uses IsNew flags instead of counting all resolved IDs
- `tests/AdwRating.Tests/Services/IdentityResolutionServiceTests.cs` -- updated for tuple returns
- `tests/AdwRating.Tests/Services/ImportServiceTests.cs` -- updated mock returns for tuples

## Notes

The identity resolution service creates entities when they don't exist but returns existing ones when they do. The import service would need to compare IDs before/after resolution to determine which are actually new.
