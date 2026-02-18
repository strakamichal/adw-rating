# Rating Rules

This document describes the rating calculation algorithm used for the leaderboard. It serves as the single source of truth for all rating parameters and formulas.

## 1. Input Data and Team Identity

### 1.1 Sources

Rating calculation uses run results from all competitions registered in the system. Each run result links to a Team (handler + dog combination) via the domain model (see `docs/03-domain-and-data.md`).

### 1.2 Team Identity

A team is identified by the unique (HandlerId, DogId) pair. Identity resolution (alias matching, diacritics normalization, name reordering) is applied during import to ensure the same real-world handler+dog pair maps to a single Team entity.

### 1.3 Name Normalization

During import, the following normalization rules are applied:

- Strip diacritics for internal matching
- Normalize typographic quotes (`\u201c\u201d`) to ASCII (`""`)
- Unify `Last, First` vs `First Last` name order
- Apply handler aliases (e.g., `Katka Tercova` → `Katerina Tercova`)
- Apply dog name aliases and registered name mappings
- Map registered name → call name (e.g., `A3Ch Libby Granting Pleasure` → `Psenik`)
- Fuzzy merge variants of the same dog for the same handler

Result: one dog/handler behaves as a single entity in ratings regardless of spelling variations.

## 2. Run Selection for Live Calculation

### 2.1 Time Window

- `latest_date` = most recent competition date in the dataset
- `cutoff_date = latest_date - LIVE_WINDOW_DAYS` (default: 730 days / 2 years)
- Only runs with `comp_date >= cutoff_date` are included in the live calculation

### 2.2 Minimum Field Size

A run counts for rating only if it has at least `MIN_FIELD_SIZE` (default: 6) teams after deduplication.

### 2.3 Deduplication Within a Run

Within a single run (`RoundKey`), each team is counted at most once.

### 2.4 Elimination Handling

- Non-eliminated teams with a valid `Rank` are ordered by placement
- Eliminated teams share a tied last place: `last_rank = count_of_non_eliminated + 1`

## 3. Core Rating Model (OpenSkill)

### 3.1 Model

- PlackettLuce (OpenSkill)
- Default initial values: `μ₀ = 25.0`, `σ₀ = μ₀/3 ≈ 8.333`

### 3.2 Major Event Weighting

- If a competition has `Tier == 1`, weight `MAJOR_EVENT_WEIGHT` (default: 1.2) is applied
- Otherwise weight `1.0`

### 3.3 Uncertainty Stabilization

After each update:

- `sigma = max(SIGMA_MIN, sigma * SIGMA_DECAY)`
- `SIGMA_DECAY` default: `0.99`
- `SIGMA_MIN` default: `1.5`

## 4. Display Rating

### 4.1 Base Rating

First, a base rating is computed per team:

- `rating_base = DISPLAY_BASE + DISPLAY_SCALE * (mu - RATING_SIGMA_MULTIPLIER * sigma)`
- Defaults: `DISPLAY_BASE = 1000`, `DISPLAY_SCALE = 40`, `RATING_SIGMA_MULTIPLIER = 1.0`

### 4.2 Podium Boost (Quality Correction)

A quality factor is applied to `rating_base` based on the team's TOP3 placement percentage:

- `quality_norm = clamp(top3_pct / PODIUM_BOOST_TARGET, 0, 1)`
- `quality_factor = PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE * quality_norm`
- `rating = rating_base * quality_factor`

Defaults: `PODIUM_BOOST_BASE = 0.85`, `PODIUM_BOOST_RANGE = 0.20`, `PODIUM_BOOST_TARGET = 50.0`

Interpretation:

- Teams with frequent TOP3 placements get a factor close to `1.05`
- Teams without TOP3 placements get a factor of `0.85`
- This rewards consistently strong results and penalizes "just many runs"

### 4.3 Cross-Size Normalization

After computing ratings within each size category (S, M, I, L), z-score normalization is applied to a common scale:

- For each category, compute mean and standard deviation (from qualified teams only)
- `normalized_rating = NORM_TARGET_MEAN + NORM_TARGET_STD * (rating - size_mean) / size_std`
- Defaults: `NORM_TARGET_MEAN = 1500`, `NORM_TARGET_STD = 150`

This ensures that a normalized rating of 1650 means "1 standard deviation above the mean" in any category. Ranking order within a category does not change, but values are comparable across categories (S, M, I, L).

Normalization is also applied to `prev_rating` (for trend arrows) so that rank changes are consistent.

### 4.4 Trend (Rank Change)

For each team, the previous rating state (mu/sigma before the last update) is tracked. The leaderboard displays rank change compared to the previous state (▲ moved up, ▼ moved down, NEW for new teams).

## 5. Team Statistics

For each team within the live window:

- `RunCount` — total runs
- `FinishedRunCount` — non-eliminated runs
- `FinishedPct` — `FinishedRunCount / RunCount` (computed, not stored)
- `Top3RunCount` — runs with rank 1–3
- `Top3Pct` — `Top3RunCount / RunCount` (computed, not stored)

## 6. Tier Labels and Provisional

### 6.1 Tier Labels (per size category)

After computing `rating`, percentile-based labels are assigned within each size category:

- `Elite`: top 2 % (`ELITE_TOP_PERCENT = 0.02`)
- `Champion`: top 10 % (`CHAMPION_TOP_PERCENT = 0.10`)
- `Expert`: top 30 % (`EXPERT_TOP_PERCENT = 0.30`)
- Otherwise: `Competitor`

Only teams with `RunCount >= MIN_RUNS_FOR_LIVE_RANKING` are included in the percentile calculation.

### 6.2 Provisional

- A team is marked as provisional ("FEW RUNS") if `sigma >= PROVISIONAL_SIGMA_THRESHOLD`
- Default: `PROVISIONAL_SIGMA_THRESHOLD = 7.8`

## 7. Output and Sorting

### 7.1 Leaderboard Sorting

Within each size category, teams are sorted by `NormalizedRating` descending. Only teams with `RunCount >= MIN_RUNS_FOR_LIVE_RANKING` (default: 5) are displayed.

### 7.2 Summary Cards

Global summary statistics displayed on the leaderboard:

- **Teams** — count of qualified teams (meeting min runs) across all categories
- **Competitions** — count of competitions in the dataset
- **Runs** — total number of runs

## 8. Configuration Parameters

All parameters are stored in the `RatingConfiguration` entity (see `docs/03-domain-and-data.md`). Default values:

| Parameter | Default |
|---|---:|
| `LiveWindowDays` | `730` |
| `MinRunsForLiveRanking` | `5` |
| `MinFieldSize` | `6` |
| `MajorEventWeight` | `1.20` |
| `SigmaDecay` | `0.99` |
| `SigmaMin` | `1.5` |
| `RatingSigmaMultiplier` | `1.0` |
| `DisplayBase` | `1000` |
| `DisplayScale` | `40` |
| `PodiumBoostBase` | `0.85` |
| `PodiumBoostRange` | `0.20` |
| `PodiumBoostTarget` | `50.0` |
| `ProvisionalSigmaThreshold` | `7.8` |
| `NormTargetMean` | `1500` |
| `NormTargetStd` | `150` |
| `EliteTopPercent` | `0.02` |
| `ChampionTopPercent` | `0.10` |
| `ExpertTopPercent` | `0.30` |
