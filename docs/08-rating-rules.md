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

## 4. Rating Calculation

### 4.1 Base Rating

First, a base rating is computed per team (intermediate value, not stored):

- `rating_base = DISPLAY_BASE + DISPLAY_SCALE * (mu - RATING_SIGMA_MULTIPLIER * sigma)`
- Defaults: `DISPLAY_BASE = 1000`, `DISPLAY_SCALE = 40`, `RATING_SIGMA_MULTIPLIER = 1.0`

### 4.2 Podium Boost (Quality Correction)

A quality factor is applied to `rating_base` based on the team's TOP3 placement percentage (intermediate value, not stored):

- `quality_norm = clamp(top3_pct / PODIUM_BOOST_TARGET, 0, 1)`
- `quality_factor = PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE * quality_norm`
- `rating_raw = rating_base * quality_factor`

Defaults: `PODIUM_BOOST_BASE = 0.85`, `PODIUM_BOOST_RANGE = 0.20`, `PODIUM_BOOST_TARGET = 0.50`

Note: `top3_pct` is a fraction (0.0–1.0), matching `Top3Pct = Top3RunCount / RunCount` in the domain model. `PODIUM_BOOST_TARGET = 0.50` means 50 % podium rate → maximum boost.

Interpretation:

- Teams with frequent TOP3 placements get a factor close to `1.05`
- Teams without TOP3 placements get a factor of `0.85`
- This rewards consistently strong results and penalizes "just many runs"

### 4.3 Cross-Size Normalization

After computing `rating_raw` within each size category (S, M, I, L), z-score normalization is applied to produce the final `Rating` (the stored value):

- For each category, compute mean and standard deviation (from qualified teams only)
- `Rating = NORM_TARGET_MEAN + NORM_TARGET_STD * (rating_raw - size_mean) / size_std`
- Defaults: `NORM_TARGET_MEAN = 1500`, `NORM_TARGET_STD = 150`

This ensures that a Rating of 1650 means "1 standard deviation above the mean" in any category. Ranking order within a category does not change, but values are comparable across categories (S, M, I, L).

Normalization is also applied to `PrevRating` (for trend arrows) so that rank changes are consistent.

### 4.4 Category-Agnostic Run Processing

The rating engine processes **each run as a single group** — all participants in a run are compared against each other, regardless of their dog's assigned FCI size category. This is important for competitions where a single run may contain dogs from different FCI categories (e.g., WAO 500 includes both FCI-I and FCI-L sized dogs jumping the same course).

**How it works:**
1. A run is processed as-is: all teams in the run are ranked ordinally and fed into OpenSkill.
2. Each team's mu/sigma is updated based on who they beat and lost to **within that run**.
3. The team's `Dog.SizeCategory` (FCI S/M/I/L) determines which leaderboard they appear on and which normalization group they belong to.
4. Cross-size normalization (section 4.3) is applied per `Dog.SizeCategory` for display.

**Result:** A FCI-I dog competing in a WAO 500 run gets rated against all opponents in that run (including FCI-L dogs). Their rating then appears in the Intermediate leaderboard. This is fair — they actually ran the same course and the placement is a real comparison.

**Known limitation — partially connected rating pools:** Internal mu/sigma values are on a single universal scale (all dogs start at μ₀=25), but categories that rarely compete against each other develop essentially independent mu distributions. A category with more competitors and more runs (e.g., Large) will have a wider mu spread and better-calibrated values than a smaller category (e.g., Intermediate). When dogs from different categories meet in a cross-category run (e.g., WAO 500), OpenSkill compares their mu values directly — but these values may not be perfectly calibrated relative to each other due to the different pool sizes and competition volumes. This is an inherent property of all Elo-family systems with partially connected pools (analogous to rating inflation across chess federations). Mitigations:
- **Sigma (uncertainty)**: dogs with fewer runs have higher sigma, so their mu adjusts more per update — OpenSkill naturally adapts uncertain ratings faster.
- **Cross-category runs act as calibration**: each WAO encounter improves the alignment between category mu scales over time.
- **Normalization**: display ratings are z-score normalized per category (section 4.3), so the leaderboard within each category is unaffected by cross-category mu scale differences.
- **Per-category leaderboard**: rankings are always within a single category — cross-category mu comparisons only matter during the OpenSkill update step, not for final ranking order.
- In practice, the effect is small: cross-category runs are a minority of total runs, the impact on any single team's mu is marginal, and it averages out over multiple encounters.

### 4.5 Trend (Rank Change)

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

Within each size category, teams are sorted by `Rating` descending. Only teams with `RunCount >= MIN_RUNS_FOR_LIVE_RANKING` (default: 5) are displayed.

### 7.2 Summary Cards

Global summary statistics displayed on the leaderboard:

- **Teams** — count of qualified teams (meeting min runs) across all categories
- **Competitions** — count of competitions in the dataset
- **Runs** — total number of runs

## 8. Configuration Parameters

All parameters are stored in the `RatingConfiguration` entity (see `docs/03-domain-and-data.md`). Default values:

| Parameter | Default |
|---|---:|
| `Mu0` | `25.0` |
| `Sigma0` | `8.333` |
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
| `PodiumBoostTarget` | `0.50` |
| `ProvisionalSigmaThreshold` | `7.8` |
| `NormTargetMean` | `1500` |
| `NormTargetStd` | `150` |
| `EliteTopPercent` | `0.02` |
| `ChampionTopPercent` | `0.10` |
| `ExpertTopPercent` | `0.30` |
| `CountryTopN` | `10` |
| `MinTeamsForCountryRanking` | `3` |

## 9. Country Rankings

Country rankings aggregate individual team ratings into a per-country score. Since display ratings are z-score normalized (mean 1500, std 150) across all size categories, cross-category averaging is valid.

### 9.1 Country Score

Computed after team ratings and tier labels are assigned during recalculation:

1. Group all active, non-provisional teams by `Handler.Country`
2. For each country with ≥ `MIN_TEAMS_FOR_COUNTRY_RANKING` (default: 3) qualified teams:
   - Take the top `COUNTRY_TOP_N` (default: 10) teams by `Rating` (across all size categories)
   - If fewer than `COUNTRY_TOP_N` teams available, use all of them
   - `CountryScore = average(top_teams_ratings)`
3. Rank countries by `CountryScore` descending

### 9.2 Provisional Countries

Countries with `MIN_TEAMS_FOR_COUNTRY_RANKING ≤ teams < COUNTRY_TOP_N` are ranked but marked as **provisional** ("FEW TEAMS"). Their score is the average of all available teams — valid but based on a smaller sample.

Countries with fewer than `MIN_TEAMS_FOR_COUNTRY_RANKING` teams are excluded from the country ranking entirely.

### 9.3 Supplementary Metrics

Displayed alongside Country Score (not used for ranking order):

- **Medal table**: count of Elite / Champion / Expert / Competitor teams in this country
- **Total qualified teams**: all active non-provisional teams from this country
- **Best team**: highest-rated team (name + rating)
- **Category breakdown**: qualified teams per size category (S / M / I / L)

## 10. Judge Toughness Score *(Phase 2)*

The Toughness Score measures how challenging a judge's courses tend to be, based on aggregate statistics across all runs they've officiated. It is computed independently from team ratings.

### 10.1 Component Metrics

Four metrics are computed across all runs officiated by a judge:

| Component | Weight | Formula | Description |
|-----------|--------|---------|-------------|
| **Elimination Rate** (ER) | 0.35 | `sum(eliminated) / sum(total_participants)` across all runs | Fraction of teams eliminated |
| **Clean Run Rate** (CRR, inverted) | 0.25 | `1 - sum(clean_finishers) / sum(all_finishers)` | Fraction of finishers who incurred faults. Higher = harder. `clean_finisher` = non-eliminated with `TotalFaults == 0` |
| **Avg Faults per Finisher** (MFF) | 0.20 | `sum(TotalFaults of finishers) / count(finishers)` | Average penalty load on those who finish |
| **Time Fault Rate** (TFR) | 0.20 | `sum(finishers with TimeFaults > 0) / count(finishers)` | Fraction of finishers who exceeded SCT. Indicates tightness of standard course time |

"Finisher" = non-eliminated team (`Eliminated = false`).

### 10.2 Formula

Each component is normalized to a 0–10 partial score, then combined via weighted sum:

```
ER_score  = clamp(ER / 0.40, 0, 1) × 10          // 40% elimination rate → max score
CRR_score = clamp(CRR / 0.80, 0, 1) × 10           // CRR is already inverted (= fraction faulted); 80%+ → max score
MFF_score = clamp(MFF / 12.0, 0, 1) × 10          // 12+ avg faults → max score
TFR_score = clamp(TFR / 0.50, 0, 1) × 10          // 50% time faults → max score

ToughnessScore = 0.35 × ER_score + 0.25 × CRR_score + 0.20 × MFF_score + 0.20 × TFR_score
```

**Weight rationale:**
- Elimination rate (0.35): The most visible and dramatic measure — eliminations are what competitors talk about.
- Clean run rate (0.25): Captures fault tolerance — how many teams make it through without penalties.
- Avg faults per finisher (0.20): Captures average severity among those who do finish.
- Time fault rate (0.20): Specifically measures SCT design tightness, a distinct judge choice.

**Scale interpretation:**

| Score | Label |
|-------|-------|
| 1–3 | Friendly courses |
| 4–5 | Standard challenge |
| 6–7 | Demanding courses |
| 8–10 | Very challenging |

### 10.3 Minimum Threshold

A judge must have officiated at least **10 runs** (individual runs with results, not 10 competitions) to receive a Toughness Score. Below that threshold, `ToughnessScore` is null. The profile still shows individual component statistics.

### 10.4 Percentile

After computing Toughness Scores for all qualified judges (≥ 10 runs), a percentile rank is computed:

- `ToughnessPercentile = fraction of qualified judges with ToughnessScore ≤ this judge's score`
- Displayed as: "Tougher than {percentile × 100}% of judges"

Percentile is null for judges below the minimum threshold.

### 10.5 Time Window

Toughness Score uses **all-time data** — all runs ever officiated by the judge, regardless of date. Unlike team ratings (which use a 730-day live window), judge style does not "decay." More data = more accurate score.

### 10.6 Tier Adjustment

The Toughness Score formula does **not** adjust for competition tier. Tier 1 events attract stronger fields, which may affect elimination rates independently of course difficulty. Instead, `Tier1RunPercentage` is displayed on the judge profile as context, letting users interpret the score with this information.

### 10.7 Computation Trigger

Judge stats are recomputed:
1. As an additional step after team rating recalculation (`recalculate` CLI command)
2. Via explicit CLI command: `recalculate judge-stats`

The computation is a batch operation: load all runs with `JudgeId`, aggregate per judge, compute Toughness Scores, then compute percentiles across all qualified judges. On recalculation, all `JudgeStats` rows are replaced
