# SmarterAgility — Results Import

> Public JSON API, no authentication required.
> Primarily designed for live results, but also returns completed (status: `ranked`) competitions.

## Base URL

```
https://www.smarteragility.com
```

---

## Endpoints

### 1. List competitions

```
GET /sal/competitions/?scope={scope}&_={timestamp}
```

| Parameter | Required | Values | Description |
|-----------|----------|--------|-------------|
| `scope` | yes | `recent`, `past`, `upcoming`, `all`, `find` | Competition filter |
| `_` | no | Unix timestamp | Cache buster |
| `year` | only for `find` | `2024` | Year |
| `month` | only for `find` | `1`–`12` | Month |
| `title` | only for `find` | text | Search by name |

**Response** (scope = `all`):

```json
{
  "recent": [ ... ],
  "upcoming": [ ... ],
  "past": [ ... ],
  "date1": "6 Feb 2026",
  "date2": "21 Feb 2026"
}
```

**Response** (scope = `find`):

```json
{
  "find": [ ... ]
}
```

Each competition object:

```json
{
  "hash_code": "e87289170d43bf2c26bf81e3aef47358b40eb58df3d46131ac477612c1f57e85",
  "title": "FCI Agility World Championship 2025",
  "country_code": "SWE",
  "date": "17 Sep - 21 Sep 2025",
  "flagged": 1,
  "rounds_format": "3LEVELS:LABEL_TYPE_CATEGORY"
}
```

### 2. Competition detail (list of rounds)

```
GET /sal/competition/{hash_code}?_={timestamp}
```

**Response:**

```json
{
  "trial": {
    "id": 530,
    "hash_code": "e872891...",
    "title": "FCI Agility World Championship 2025",
    "country_code": "SWE",
    "date": "17 Sep - 21 Sep 2025",
    "rounds_format": "3LEVELS:LABEL_TYPE_CATEGORY",
    "group_by": "course_type"
  },
  "rounds": [
    {
      "id": 13,
      "hash": "000530_000013_bf974e2d1459dc75f830a800337e14c3",
      "trial_id": 530,
      "type": "team-jumping",
      "label": "D1 - Wed",
      "category": "intermediate",
      "grade": "",
      "ring": 1,
      "ring_sequence": 1,
      "cnt_runs": 111,
      "status": "ranked",
      "participation_type": "c"
    }
  ]
}
```

**Round fields:**

| Field | Description |
|-------|-------------|
| `hash` | Round identifier for endpoint 3. Format: `{trial_id_6dig}_{round_id_6dig}_{md5}` |
| `type` | Type: `agility`, `jumping`, `agility1`, `agility2`, `agility3`, `team-jumping`, `team-agility`, `agility open 1`, `jumping open 2`, `final`, etc. |
| `category` | Size: `small`, `medium`, `intermediate`, `large`, sometimes combined e.g. `xs+small` |
| `grade` | Grade: `1`, `2`, `3`, `0` (open), `""` (not specified) |
| `cnt_runs` | Number of runs in the round |
| `status` | `ranked` = complete, `incomplete` = partial results |
| `participation_type` | `i` = individual, `c` = country/team |

### 3. Round results

```
GET /sal/round/{round_hash}?_={timestamp}
```

**Response** — array of run objects (sorted by ranking):

```json
[
  {
    "dorsal": "366",
    "handler": "Channie Elmestedt",
    "hide_handler": 0,
    "dog_short": "Fame",
    "dog": "Border star hall of fame",
    "breed": "Border Collie",
    "country_name": "DNK",
    "team_name": "",
    "is_eliminated": 0,
    "result_value": 33.11,
    "ranking": 1,
    "course_time": "33.11",
    "time_faults": "0.00",
    "course_faults": 0,
    "touch_faults": null,
    "refusals": 0,
    "total_faults": "0.00",
    "speed": 6.1,
    "ended_at": 1758096456
  }
]
```

**Run fields:**

| Field | Type | Description |
|-------|------|-------------|
| `dorsal` | string | Bib number |
| `handler` | string | Handler name |
| `hide_handler` | int | 1 = hidden handler (GDPR) |
| `dog` | string | Full registered dog name |
| `dog_short` | string | Call name |
| `breed` | string | Breed |
| `country_name` | string | Country code (3-letter ISO) |
| `team_name` | string | Team name (empty for individual runs) |
| `ranking` | int/string | Placement (empty for eliminated) |
| `course_time` | string | Course time in seconds |
| `time_faults` | string | Time faults |
| `course_faults` | int | Course faults (knocked bars etc.) |
| `touch_faults` | int/null | Touch faults (contact zones) |
| `refusals` | int | Refusals |
| `total_faults` | string | Total faults, or `"Elim."` for elimination |
| `speed` | float | Speed (m/s) |
| `is_eliminated` | int | 0/1 |
| `result_value` | float | Sorting value |
| `ended_at` | int | Unix timestamp of run completion |

---

## Downloading a full competition

```
1.  GET /sal/competition/{hash_code}
    -> trial.title, trial.date, trial.country_code
    -> rounds[] with hash, type, category, grade, cnt_runs

2.  For each round where status == "ranked":
      GET /sal/round/{round.hash}
      -> array of runs with results

3.  Merge data: identify unique dogs via (handler + dog) or (dorsal)
    within a single competition.
```

### Dog identification

The API does not return a global dog identifier. Unique identity within a single competition:
- **dorsal** (bib number) — unique per competition
- **(handler, dog)** — more reliable across competitions, but handler name spelling may vary

Across competitions, matching must be done via dog name + handler name (optionally + breed).

---

## Round metadata

The competition detail endpoint (2) does not include course parameters. To get course metadata, use the alternative live endpoint:

```
GET /api_sal.php?round_key={round_hash}&timestamp=
```

This returns an additional `data.round` object with:

| Field | Description |
|-------|-------------|
| `length` | Course length (m) |
| `sct` | Standard Course Time (s) |
| `mct` | Maximum Course Time (s) |
| `judge` | Judge |
| `judge_2` | Second judge |
| `scoring` | Scoring scheme (e.g. `"25\|22\|20\|18\|..."`) |

---

## Known hash codes

See the table in `data/competitions.md`, section "SmarterAgility API — Dostupne zavody a hash kody".

## Limitations

- No observed rate limiting, but use reasonable intervals (100–200 ms between requests).
- Not all competitions are available — the system is primarily used by clubs in Belgium, Poland, Croatia, Slovenia, and Greece.
- Some major competitions (AWC 2024, EO, Polish Open Feb 2025) are missing from the system.
- The `touch_faults` field is often `null` — not all competitions track it.
- `hide_handler: 1` means the handler name is hidden (GDPR). These records should be ignored or anonymized.
