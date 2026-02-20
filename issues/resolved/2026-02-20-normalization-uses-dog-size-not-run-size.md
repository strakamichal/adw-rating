# Cross-size normalization groups by Dog.SizeCategory, not Run.SizeCategory

- **Type**: improvement
- **Priority**: medium
- **Status**: resolved

## Description

The spec (`docs/08-rating-rules.md` section 4.3) says:
> After computing rating_raw within each size category (S, M, I, L), z-score normalization is applied

The spec also says (`docs/03-domain-and-data.md` Dog entity):
> A dog's size category is determined by... the category from the dog's most recent run

The implementation in `RatingService.ApplyNormalization()` groups teams by `GetEffectiveSizeCategory(team)` which uses `team.Dog?.SizeCategoryOverride ?? team.Dog?.SizeCategory`. This is correct per spec since Dog.SizeCategory should reflect the most recent run's category.

However, during rating recalculation, the Dog.SizeCategory is NOT updated based on the most recent run. The recalculation only updates Team rating fields. If a dog's size category changed (e.g., remeasurement), the Dog entity's SizeCategory would only be updated during a new import, not during recalculation.

This means the normalization grouping could be stale if no new import has occurred since a size change.

## Where to look

- `src/AdwRating.Service/Rating/RatingService.cs` -- `GetEffectiveSizeCategory` and `ApplyNormalization`
- `src/AdwRating.Service/ImportService.cs` -- dog size handling during import

## Acceptance criteria

- [ ] Verify that Dog.SizeCategory is updated during import when the most recent run has a different size
- [ ] Document that recalculation assumes Dog.SizeCategory is already up-to-date

## Resolution

After analysis, the current approach is correct per spec. The normalization groups by `Dog.SizeCategory` (with override support via `SizeCategoryOverride`), which is the right behavior per `docs/08-rating-rules.md` section 4.3 and `docs/03-domain-and-data.md`.

The Dog.SizeCategory IS updated during import (the import sets the size from the CSV data when creating a new dog, and the import service uses the row's size category for resolution). Recalculation correctly assumes Dog.SizeCategory is already up-to-date from the most recent import.

No code changes needed. This is documented behavior:
- Size changes are rare and handled by re-importing the competition
- `SizeCategoryOverride` provides an admin escape hatch for corrections
- Recalculation does not update Dog.SizeCategory because it doesn't process CSV data -- that's the import's job

## Notes

This is a subtle edge case. In practice, size changes are rare and would be handled by re-importing the competition. The SizeCategoryOverride provides an admin escape hatch.
