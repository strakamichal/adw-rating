# Domain and Data Model

## 1. Glossary

**Core concepts**
- **Team**: A unique combination of one handler and one dog. The rated entity. A handler with two dogs forms two separate teams; two handlers running the same dog also form separate teams.
- **Handler**: A human competitor. Identified by name and country.
- **Dog**: A canine competitor. Identified by call name, optionally registered name, breed, and size category.
- **Rating**: A numerical score reflecting the team's competitive performance, calculated using the PlackettLuce (OpenSkill) model. Expressed on a display scale (base ~1000, normalized target mean 1500).

**Competitions**
- **Competition**: A single competition event (e.g., AWC 2025, Moravia Open 2024). Has a date, location, tier, and one or more runs.
- **Run**: A single scored round within a competition, defined by discipline and size category (e.g., individual agility Large run 1). The atomic unit for rating calculation.
- **Run result**: One team's scored attempt within a run — placement, time, faults, speed, eliminated flag.
- **Discipline**: The type of course: Agility, Jumping, or Final.
- **Competition tier**: Importance classification affecting rating weight (Tier 1 = weight 1.2, Tier 2 = weight 1.0).

**Size and classification**
- **Size category**: Dog height category using FCI classification: S (Small, <35 cm), M (Medium, 35–43 cm), I (Intermediate, 43–48 cm), L (Large, >48 cm). If a source uses XS, it is mapped to S during import. Ratings are calculated per category, then z-score normalized for cross-category comparability.
- **Tier label**: Percentile-based label within a size category: Elite (top 2 %), Champion (top 10 %), Expert (top 30 %), Competitor (rest).
- **Active team**: A team meeting minimum activity thresholds (≥5 runs in the last 730 days). Only active teams appear in the live rankings.

**Identity resolution**
- **Alias**: A confirmed mapping from one name variant to a canonical form (e.g., "Katka Tercova" → "Katerina Tercova"). Stored in an alias table and applied during import.
- **Identity resolution**: Automatic fuzzy matching of handlers and dogs across imports — diacritics normalization, name reordering, alias lookup, registered-to-call-name mapping.

## 2. Core entities

### Handler

**Description**: A human competitor in agility events.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `Name` | string(200) | yes | Display name (original diacritics preserved) |
| `NormalizedName` | string(200) | yes | Lowercased, diacritics-stripped form for matching |
| `Country` | string(3) | yes | ISO 3166-1 alpha-3 code (e.g., `CZE`, `GBR`). Mutable — updated if handler relocates |
| `Slug` | string(200) | yes | URL-friendly identifier for profile pages |

**Rules**:
- `NormalizedName` is derived automatically from `Name` (lowercase, strip diacritics, normalize whitespace).
- `Slug` is derived from `Name`, must be unique. On collision append numeric suffix.
- Country is taken from the most recent import data for this handler.

**Acceptance criteria**:
- [ ] Handler can be created with Name and Country
- [ ] NormalizedName is auto-generated on create/update
- [ ] Slug is unique and URL-safe
- [ ] Two handlers with different countries but same name are separate entities

---

### Dog

**Description**: A canine competitor identified by call name and optionally registered name.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `CallName` | string(100) | yes | Short everyday name (e.g., "Fame", "Ray") |
| `NormalizedCallName` | string(100) | yes | Lowercased, diacritics-stripped call name |
| `RegisteredName` | string(300) | no | Full registered/kennel name (e.g., "Border Star Hall of Fame") |
| `Breed` | string(100) | no | Dog breed (e.g., "Border Collie") |
| `SizeCategory` | SizeCategory | yes | FCI size category: S, M, I, L |

**Rules**:
- `NormalizedCallName` is derived automatically from `CallName`.
- A dog's size category is determined from import data. If a dog appears in different categories across imports, the most frequent category wins (log a warning).
- `RegisteredName` is stored only if it's meaningfully longer than the call name (>2 words).

**Acceptance criteria**:
- [ ] Dog can be created with CallName and SizeCategory
- [ ] NormalizedCallName is auto-generated
- [ ] RegisteredName is optional and can be null
- [ ] Breed is optional and can be null

---

### Team

**Description**: A unique handler+dog combination. The entity that receives a rating.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `HandlerId` | int | yes | FK → Handler |
| `DogId` | int | yes | FK → Dog |
| `Slug` | string(300) | yes | URL-friendly identifier for the team profile page |
| `Mu` | float | yes | OpenSkill mean (internal). Default: μ₀ (see `docs/08-rating-rules.md`) |
| `Sigma` | float | yes | OpenSkill uncertainty (internal). Default: σ₀ = μ₀/3 (see `docs/08-rating-rules.md`) |
| `Rating` | float | yes | Per-category rating after display scaling + podium boost (before cross-size normalization). Used for ranking within a size category |
| `NormalizedRating` | float | yes | Rating after cross-size z-score normalization (target mean 1500). The primary display value on the leaderboard and profiles |
| `PrevMu` | float | yes | Mu before the most recent rating update (for trend) |
| `PrevSigma` | float | yes | Sigma before the most recent rating update |
| `PrevRating` | float | yes | Previous per-category rating (before normalization) |
| `PrevNormalizedRating` | float | yes | Previous normalized rating (for trend arrows on the leaderboard) |
| `RunCount` | int | yes | Total runs in the active window |
| `FinishedRunCount` | int | yes | Non-eliminated runs in the active window |
| `Top3RunCount` | int | yes | Runs with rank 1–3 in the active window |
| `IsActive` | bool | yes | True if `RunCount >= MIN_RUNS_FOR_LIVE_RANKING` and has runs within the time window |
| `IsProvisional` | bool | yes | True if `Sigma >= LIVE_PROVISIONAL_SIGMA_THRESHOLD` |
| `TierLabel` | TierLabel | no | Percentile-based label: Elite, Champion, Expert, Competitor. Null if not active |
| `PeakNormalizedRating` | float | yes | Highest NormalizedRating ever achieved by this team. Updated during recalculation. Used on handler profile to show career peak |

**Computed (not stored)**: `FinishedPct = FinishedRunCount / RunCount`, `Top3Pct = Top3RunCount / RunCount`. Used in podium boost calculation (see `docs/08-rating-rules.md`).

**Rules**:
- (`HandlerId`, `DogId`) must be unique — one team per handler+dog pair.
- `Slug` is derived from handler name + dog call name. Must be unique.
- Rating fields (`Mu`, `Sigma`, `Rating`, etc.) are recalculated in batch, never set directly.
- `IsActive` is recomputed during each rating recalculation cycle.

**Acceptance criteria**:
- [ ] Team is created automatically when a new handler+dog pair appears in import
- [ ] Duplicate (HandlerId, DogId) pair is rejected
- [ ] Slug is unique and URL-safe
- [ ] Rating defaults are set correctly for new teams
- [ ] IsActive reflects current run count and time window

---

### Competition

**Description**: A single competition event with metadata and a collection of runs.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `Slug` | string(100) | yes | Unique machine identifier (e.g., `awc2024`, `moravia-open-2024`) |
| `Name` | string(200) | yes | Human-readable display name |
| `Date` | date | yes | Start date of the competition |
| `EndDate` | date | no | End date (if multi-day). Null if single-day |
| `Country` | string(3) | no | Host country (ISO 3166-1 alpha-3) |
| `Location` | string(200) | no | City or venue name |
| `Tier` | int | yes | Competition tier (1 = major, 2 = standard) |

**Rules**:
- `Slug` must be unique across all competitions.
- `Date` must be a valid past or present date.
- `Tier` determines the weight used in rating calculation (Tier 1 → 1.2, others → 1.0).
- Competition data is immutable after import — corrections go through deleting the competition (cascading to runs/results) and re-importing corrected data.

**Acceptance criteria**:
- [ ] Competition can be created with Slug, Name, Date, Tier
- [ ] Duplicate Slug is rejected
- [ ] Tier value is validated (1 or 2)
- [ ] Optional fields (EndDate, Country, Location) can be null

---

### Run

**Description**: A single scored round within a competition, defined by size category and discipline.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `CompetitionId` | int | yes | FK → Competition |
| `Date` | date | yes | Date this run took place (for multi-day competitions, determines the day grouping) |
| `RunNumber` | int | yes | Sequence number of this run within its discipline and size category (e.g., run 1, run 2) |
| `RoundKey` | string(100) | yes | Unique round identifier within the competition (e.g., `ind_jumping_large_1`) |
| `SizeCategory` | SizeCategory | yes | Size category for this run |
| `Discipline` | Discipline | yes | Discipline: Agility, Jumping, Final |
| `IsTeamRound` | bool | yes | True for team rounds, False for individual. Metadata only — team rounds still count for rating (individual placements are used) |
| `Judge` | string(200) | no | Judge name |
| `Sct` | float | no | Standard Course Time in seconds |
| `Mct` | float | no | Maximum Course Time in seconds |
| `CourseLength` | float | no | Course length in meters |

**Rules**:
- (`CompetitionId`, `RoundKey`) must be unique.
- Team rounds (`IsTeamRound = true`) use the same individual placements as any other run. The `IsTeamRound` flag is metadata only (for display grouping); team rounds are **included** in rating calculation.
- A run must have at least `MIN_FIELD_SIZE` (6) results to count for rating.

**Acceptance criteria**:
- [ ] Run can be created with CompetitionId, RoundKey, SizeCategory, Discipline, IsTeamRound
- [ ] Duplicate (CompetitionId, RoundKey) is rejected
- [ ] Team rounds use individual placements and are included in rating calculation
- [ ] Optional fields (Judge, Sct, Mct, CourseLength) can be null

---

### RunResult

**Description**: One team's performance in a single run — the atomic data point for rating calculation.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `RunId` | int | yes | FK → Run |
| `TeamId` | int | yes | FK → Team |
| `Rank` | int | no | Placement in the round. Null for eliminated teams |
| `Faults` | int | no | Obstacle faults. Null for eliminated |
| `Refusals` | int | no | Refusals. Null for eliminated |
| `TimeFaults` | float | no | Time faults. Null for eliminated |
| `TotalFaults` | float | no | Total faults. Null for eliminated |
| `Time` | float | no | Run time in seconds. Null for eliminated |
| `Speed` | float | no | Speed in m/s. Null for eliminated |
| `Eliminated` | bool | yes | True if eliminated (DIS/DSQ/NFC/RET/WD) |
| `StartNo` | int | no | Start number from the source data |

**Rules**:
- (`RunId`, `TeamId`) must be unique — one result per team per run (deduplication).
- If `Eliminated = true`, performance fields (`Rank`, `Faults`, `Time`, etc.) must be null.
- If `Eliminated = false`, `Rank` must be a positive integer.
- For rating: eliminated teams share a tied last place (`rank = count_of_non_eliminated + 1`).
- Results are immutable after import.

**Acceptance criteria**:
- [ ] RunResult can be created with RunId, TeamId, Eliminated
- [ ] Duplicate (RunId, TeamId) is rejected
- [ ] Eliminated results have null performance fields
- [ ] Non-eliminated results have a valid Rank

---

### HandlerAlias

**Description**: A confirmed mapping from a variant name to a canonical handler. Append-only.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `AliasName` | string(200) | yes | The variant name, stored in normalized form (lowercased, diacritics stripped, whitespace normalized — same rules as `Handler.NormalizedName`) |
| `CanonicalHandlerId` | int | yes | FK → Handler (the canonical record) |
| `Source` | AliasSource | yes | How this alias was confirmed: Manual, Import, FuzzyMatch |
| `CreatedAt` | datetime | yes | When the alias was created |

**Rules**:
- `AliasName` must be unique — one canonical mapping per variant.
- Alias table is append-only: confirmed matches are never silently removed.
- Applied during import to resolve handler identity.

**Acceptance criteria**:
- [ ] Alias can be created with AliasName, CanonicalHandlerId, Source
- [ ] Duplicate AliasName is rejected
- [ ] Alias cannot be deleted (only superseded by admin merge)

---

### DogAlias

**Description**: A confirmed mapping from a variant dog name to a canonical dog. Includes call name aliases and registered-to-call-name mappings. Append-only.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `AliasName` | string(300) | yes | The variant name, stored in normalized form (lowercased, diacritics stripped, whitespace normalized — same rules as `Dog.NormalizedCallName`) |
| `CanonicalDogId` | int | yes | FK → Dog (the canonical record) |
| `AliasType` | DogAliasType | yes | CallName, RegisteredName |
| `Source` | AliasSource | yes | How confirmed: Manual, Import, FuzzyMatch |
| `CreatedAt` | datetime | yes | When the alias was created |

**Rules**:
- (`AliasName`, `AliasType`) must be unique.
- Registered-to-call-name mappings allow resolving `"A3Ch Libby Granting Pleasure"` → dog with call name `"Psenik"`.
- Append-only like HandlerAlias.

**Acceptance criteria**:
- [ ] Alias can be created with AliasName, CanonicalDogId, AliasType, Source
- [ ] Duplicate (AliasName, AliasType) is rejected
- [ ] Both call name and registered name aliases work correctly

---

### ImportLog

**Description**: An audit record of a competition data import operation.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `CompetitionId` | int | no | FK → Competition (null if import was rejected) |
| `FileName` | string(500) | yes | Source file name |
| `ImportedAt` | datetime | yes | Timestamp of the import |
| `Status` | ImportStatus | yes | Success, Rejected, PartialWarning |
| `RowCount` | int | yes | Number of rows in the source file |
| `NewHandlersCount` | int | yes | Number of newly created handlers |
| `NewDogsCount` | int | yes | Number of newly created dogs |
| `NewTeamsCount` | int | yes | Number of newly created teams |
| `Errors` | text | no | Error details if rejected |
| `Warnings` | text | no | Warning details (e.g., potential duplicates) |

**Rules**:
- One ImportLog per import attempt (including rejected ones).
- Successful imports must reference a Competition.

**Acceptance criteria**:
- [ ] ImportLog is created for every import attempt
- [ ] Rejected imports have null CompetitionId and non-empty Errors
- [ ] Successful imports track entity creation counts

---

### RatingSnapshot

**Description**: A point-in-time record of a team's rating state after processing a specific run during rating recalculation. Used to render rating progression charts on team and handler profiles.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `TeamId` | int | yes | FK → Team |
| `RunResultId` | int | yes | FK → RunResult (the run that caused this snapshot) |
| `CompetitionId` | int | yes | FK → Competition (denormalized for efficient querying) |
| `Date` | date | yes | Date of the run (denormalized from Run.Date for chart x-axis) |
| `Mu` | float | yes | Team's mu after this run was processed |
| `Sigma` | float | yes | Team's sigma after this run was processed |
| `Rating` | float | yes | Per-category display rating at this point |
| `NormalizedRating` | float | yes | Cross-size normalized rating at this point (see note below) |

**Rules**:
- (`TeamId`, `RunResultId`) must be unique — one snapshot per team per run.
- Snapshots are created during batch rating recalculation, not during import.
- On full recalculation, all existing snapshots are deleted and regenerated.
- Ordered by `Date` (then by Run processing order within a competition) to form the progression timeline.
- **Normalization note**: `NormalizedRating` in all snapshots uses the normalization parameters (per-size mean and std) computed at the end of the current recalculation cycle. It does **not** reflect what the normalized rating actually was at that historical point in time. This is a deliberate simplification — recomputing normalization stats at every historical point would be prohibitively expensive and fragile. The chart shows relative progression within the current normalization frame.

**Acceptance criteria**:
- [ ] Snapshot is created for each run result processed during recalculation
- [ ] Duplicate (TeamId, RunResultId) is rejected
- [ ] Full recalculation replaces all snapshots
- [ ] Chart query returns snapshots ordered by date for a given team

---

### RatingConfiguration

**Description**: Stores the rating calculation parameters. A single active configuration row at any time. Changing parameters requires creating a new configuration and triggering a full rating recalculation.

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | int | yes | Primary key |
| `CreatedAt` | datetime | yes | When this configuration was created |
| `IsActive` | bool | yes | Only one configuration can be active at a time |
| `LiveWindowDays` | int | yes | Time window for active runs (default: 730) |
| `MinRunsForLiveRanking` | int | yes | Minimum runs to appear in live rankings (default: 5) |
| `MinFieldSize` | int | yes | Minimum participants for a run to count (default: 6) |
| `MajorEventWeight` | float | yes | Weight multiplier for Tier 1 competitions (default: 1.2) |
| `SigmaDecay` | float | yes | Sigma decay factor per update (default: 0.99) |
| `SigmaMin` | float | yes | Minimum sigma floor (default: 1.5) |
| `DisplayBase` | float | yes | Base offset for display rating (default: 1000) |
| `DisplayScale` | float | yes | Scale factor for display rating (default: 40) |
| `RatingSigmaMultiplier` | float | yes | Sigma penalty in display rating (default: 1.0) |
| `PodiumBoostBase` | float | yes | Minimum quality factor (default: 0.85) |
| `PodiumBoostRange` | float | yes | Quality factor range (default: 0.20) |
| `PodiumBoostTarget` | float | yes | Top3 percentage for max boost (default: 50.0) |
| `ProvisionalSigmaThreshold` | float | yes | Sigma threshold for "FEW RUNS" label (default: 7.8) |
| `NormTargetMean` | float | yes | Target mean for cross-size normalization (default: 1500) |
| `NormTargetStd` | float | yes | Target std dev for cross-size normalization (default: 150) |
| `EliteTopPercent` | float | yes | Percentile cutoff for Elite tier (default: 0.02) |
| `ChampionTopPercent` | float | yes | Percentile cutoff for Champion tier (default: 0.10) |
| `ExpertTopPercent` | float | yes | Percentile cutoff for Expert tier (default: 0.30) |

**Rules**:
- Exactly one row must have `IsActive = true` at any time.
- Changing any parameter requires a full rating recalculation for all teams.
- Previous configurations are kept for audit trail (never deleted).

**Acceptance criteria**:
- [ ] A default configuration is seeded on first run
- [ ] Only one configuration can be active
- [ ] Changing the active configuration triggers a full recalculation

---

## 3. Enums

| Enum | Values | Used by |
|------|--------|---------|
| `SizeCategory` | `S`, `M`, `I`, `L` | Dog.SizeCategory, Run.SizeCategory |
| `Discipline` | `Agility`, `Jumping`, `Final` | Run.Discipline |
| `TierLabel` | `Elite`, `Champion`, `Expert`, `Competitor` | Team.TierLabel |
| `AliasSource` | `Manual`, `Import`, `FuzzyMatch` | HandlerAlias.Source, DogAlias.Source |
| `DogAliasType` | `CallName`, `RegisteredName` | DogAlias.AliasType |
| `ImportStatus` | `Success`, `Rejected`, `PartialWarning` | ImportLog.Status |

**Note on size categories**: The CSV format may use verbose names (`Small`, `Medium`, `Intermediate`, `Large`) which are mapped to enum values (S, M, I, L) during import. If a source uses `XS` (Extra Small), it is mapped to `S`. All four FCI categories (S, M, I, L) are stored in the database.

## 4. Relationships

| Relationship | Type | Description |
|-------------|------|-------------|
| Handler → Team | 1:N | One handler can have multiple teams (one per dog) |
| Dog → Team | 1:N | One dog can have multiple teams (one per handler) |
| Handler ↔ Dog | M:N | Many-to-many via Team |
| Competition → Run | 1:N | One competition has many runs |
| Run → RunResult | 1:N | One run has many results (one per participating team) |
| Team → RunResult | 1:N | One team has many results across competitions |
| HandlerAlias → Handler | N:1 | Many aliases point to one canonical handler |
| DogAlias → Dog | N:1 | Many aliases point to one canonical dog |
| ImportLog → Competition | N:1 | Multiple imports can reference the same competition (re-imports) |
| RatingSnapshot → Team | N:1 | Many snapshots per team (one per run processed) |
| RatingSnapshot → RunResult | 1:1 | One snapshot per run result |
| RatingSnapshot → Competition | N:1 | Many snapshots reference the same competition |

**Ownership and cascading**:
- Deleting a Competition cascades to its Runs and their RunResults. This is the mechanism for corrections — delete the competition and re-import corrected data. Admin-only operation.
- Deleting a Handler is forbidden if Teams exist — merge into another handler instead.
- Deleting a Dog is forbidden if Teams exist — merge into another dog instead.
- Merging two handlers: all Teams of the source handler are reassigned to the target handler, aliases are updated, source handler is deleted.
- Merging two dogs: only dogs in the **same size category** can be merged. All Teams of the source dog are reassigned to the target dog, aliases are updated, source dog is deleted. If the merge creates a duplicate (HandlerId, DogId) team, those teams' run results are also merged.

## 5. Mapping from AS-IS

| AS-IS (Python scripts) | TO-BE entity | Notes |
|-------------------------|-------------|-------|
| `COMPETITIONS` dict in `calculate_rating.py` | `Competition` | Was a hardcoded dict with slug, date, tier, name. Now a DB table with additional fields (EndDate, Country, Location) |
| `team_id` string (`handler|||dog`) | `Team` entity | Was a concatenated string. Now a proper entity with FK to Handler and Dog, plus rating fields |
| Inline handler name in CSV | `Handler` entity | Was just a string. Now a normalized entity with aliases and slug |
| Inline dog name in CSV | `Dog` entity | Was just a string parsed into call/registered name. Now a normalized entity with breed and size |
| `HANDLER_ALIASES` dict | `HandlerAlias` table | Was hardcoded in Python. Now a DB table, append-only |
| `DOG_ALIASES` + `REGISTERED_TO_CALL` dicts | `DogAlias` table | Was two separate hardcoded dicts. Now unified in one table with `AliasType` discriminator |
| Rows in `*_results.csv` | `Run` + `RunResult` | Was flat CSV rows. Now split into Run (round metadata) and RunResult (team performance) |
| `mu`, `sigma` in rating calculation | `Team.Mu`, `Team.Sigma` | Was in-memory only (recalculated each time). Now persisted for incremental updates and profile display |
| Rating output CSV columns | `Team` rating fields | Was output-only CSV. Now persisted in DB for API/web serving |
| — (new) | `ImportLog` | Audit trail for imports — did not exist in AS-IS |
| — (new) | `RatingSnapshot` | Per-run rating history for progression charts — did not exist in AS-IS (ratings were recalculated from scratch each time) |
