# Remote import is extremely slow due to per-row DB roundtrips

- **Type**: improvement
- **Priority**: medium
- **Status**: open

## Description

Importing competitions via CLI to a remote SQL Server is orders of magnitude slower than localhost. Each CSV row triggers ~10-15 individual DB queries (handler alias lookup, handler lookup, fuzzy match, dog alias lookup, dog lookup, team lookup, rating config lookup, plus inserts). With ~40ms network latency per query, a single competition with 2000 rows takes ~20 minutes vs. seconds locally.

## Current state

- 14 of ~44 competitions imported to remote DB (`46.225.216.96`)
- Import was stopped because it would take ~1-2 hours total
- Connection string: `Server=46.225.216.96,1433;Database=AdwRating;User Id=adwrating;Password=Up437LATMm22;TrustServerCertificate=True`
- Import script with all remaining competitions is at `/tmp/import-remaining.sh`

### Already imported (14):
proseccup-2024, polish-open-2024-inl, polish-open-2024-xsm, hungarian-open-2024, fmbb-2024, alpine-agility-open-2024, midsummer-2024, croatian-open-2024, slovenian-open-2024, moravia-open-2024, joawc-soawc-2024, prague-agility-party-2024, bcc-2024, eo-2024

### Remaining (~30):
nac-2024, awc-2024, norwegian-open-2024, polish-open-soft-2024-inl/xsm, all 2025 competitions, lotw-i/ii/iii-2025-2026, polish-open-2026-inl/xsm

## Possible solutions

1. **Import locally, then bulk transfer** — import all into local SQL Server (fast), then use `bcp` or `.bacpac` to transfer to remote
2. **Batch DB operations in ImportService** — reduce roundtrips by batching handler/dog/team lookups (e.g., pre-load all handlers into memory before import)
3. **Just let it run** — accept the ~1-2 hour import time, run the script and wait

## Where to look

- `src/AdwRating.Service/ImportService.cs` — per-row entity resolution logic
- `src/AdwRating.Cli/Commands/ImportCommand.cs` — CLI command
- `/tmp/import-remaining.sh` — script with all remaining import commands

## Acceptance criteria

- [ ] All ~44 competitions imported to remote DB
- [ ] Rating config seeded

## Notes

- Rating config already seeded on remote
- EF Core console logger adds noise to stdout (not just stderr), making it hard to track progress
