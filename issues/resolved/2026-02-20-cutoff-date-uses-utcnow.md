# Rating cutoff date uses DateTime.UtcNow instead of latest competition date

- **Type**: bug
- **Priority**: high
- **Status**: resolved

## Description

The spec (`docs/08-rating-rules.md` section 2.1) defines the cutoff date as:
- `latest_date` = most recent competition date in the dataset
- `cutoff_date = latest_date - LIVE_WINDOW_DAYS`

The implementation in `Service/Rating/RatingService.cs:62` uses `DateTime.UtcNow` instead:
```csharp
var runs = await _runRepo.GetAllInWindowAsync(
    DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-config.LiveWindowDays));
```

This means if no competitions have been imported for 6 months, the window still slides forward from today, potentially dropping older valid runs from the calculation. The spec intends the window to be anchored to the latest data point.

## Where to look

- `src/AdwRating.Service/Rating/RatingService.cs` line 62

## Acceptance criteria

- [ ] Cutoff date is computed as `max(Run.Date) - LiveWindowDays` across all runs
- [ ] If no runs exist, the method handles it gracefully (already does)

## Resolution

Added `GetLatestDateAsync()` to `IRunRepository` and its MSSQL implementation. `RatingService.RecalculateAllAsync` now computes the cutoff as `max(Run.Date) - LiveWindowDays` instead of `DateTime.UtcNow - LiveWindowDays`. If no runs exist, the method handles it gracefully by saving reset teams and returning early.

**Files changed:**
- `src/AdwRating.Domain/Interfaces/IRunRepository.cs` -- added `GetLatestDateAsync()`
- `src/AdwRating.Data.Mssql/Repositories/RunRepository.cs` -- implemented `GetLatestDateAsync()`
- `src/AdwRating.Service/Rating/RatingService.cs` -- use latest date from DB for cutoff

## Notes

This affects rating accuracy when there is a gap between the latest competition and the current date.
