# Results CSV Format

This document describes the structure of CSV files containing agility competition results. Each competition is stored in a single CSV file following the naming convention `<event_slug>_results.csv` (e.g., `awc2024_results.csv`), located in `data/<event_slug>/`.

## Columns

| # | Column | Type | Required | Description |
|---|--------|------|----------|-------------|
| 1 | `competition` | string | yes | Competition identifier / slug (e.g., `awc2024`, `prague-agility-party-2025`) |
| 2 | `round_key` | string | yes | Unique round identifier within the competition — see convention below |
| 3 | `size` | string | yes | Size category: `Small`, `Medium`, `Intermediate`, `Large` |
| 4 | `discipline` | string | yes | Discipline: `Jumping`, `Agility`, or `Final` |
| 5 | `is_team_round` | bool | yes | `True` for team rounds, `False` for individual |
| 6 | `rank` | int \| empty | no | Placement in the round. Empty for eliminated dogs |
| 7 | `start_no` | string | no | Start number (used for identity recovery when handler/dog fields are incomplete) |
| 8 | `handler` | string | yes | Handler name |
| 9 | `dog` | string | yes | Dog name — registered name with call name in parentheses or quotes, e.g., `Border Star Hall of Fame (Fame)` or `Flank "Ray"` |
| 10 | `breed` | string | no | Dog breed (e.g., `Border Collie`). May be empty |
| 11 | `country` | string | yes | Country — ISO 3166-1 alpha-3 code (e.g., `CZE`, `AUT`, `GBR`) |
| 12 | `faults` | int \| empty | no | Obstacle faults (each = 5 penalty points). Empty for eliminated |
| 13 | `refusals` | int \| empty | no | Refusals (each = 5 penalty points). Empty for eliminated |
| 14 | `time_faults` | float \| empty | no | Time faults (exceeding SCT). Empty for eliminated |
| 15 | `total_faults` | float \| empty | no | Total faults = `faults × 5 + refusals × 5 + time_faults`. Empty for eliminated |
| 16 | `time` | float \| empty | no | Run time in seconds. Empty for eliminated |
| 17 | `speed` | float \| empty | no | Speed in m/s (`course_length / time`). Empty for eliminated |
| 18 | `eliminated` | bool | yes | `True` if the dog was eliminated (DIS/DSQ/NFC/RET/WD), otherwise `False` |
| 19 | `judge` | string | no | Judge name |
| 20 | `sct` | float \| empty | no | Standard Course Time in seconds |
| 21 | `mct` | float \| empty | no | Maximum Course Time in seconds |
| 22 | `course_length` | float \| empty | no | Course length in meters |

## Round key convention

Format: `{type}_{discipline}_{size}[_{number}]`

- **type**: `team` or `ind` (individual)
- **discipline**: `jumping`, `agility`, or `final`
- **size**: `small`, `medium`, `intermediate`, `large`
- **number** (optional): run number when multiple rounds of the same type exist (e.g., `_1`, `_2`)

Examples:
- `ind_jumping_large` — individual jumping, Large category
- `ind_agility_intermediate_1` — first individual agility round, Intermediate category
- `team_agility_large_1` — team agility round 1, Large category
- `ind_final_large` — individual final, Large category

## Empty values

- For eliminated dogs (`eliminated = True`), columns `rank`, `faults`, `refusals`, `time_faults`, `total_faults`, `time`, `speed` are **empty** (not zero).
- The `breed` column may be empty if breed was not available in the source data.
- The `start_no` column may be empty for some competitions.

## Sorting

Rows are sorted primarily by `round_key`, secondarily by `rank` (non-eliminated first, eliminated at the end without ordering).

## Encoding and format

- Encoding: UTF-8
- Delimiter: comma (`,`)
- Standard CSV quoting — fields containing commas or quotes are quoted
- Boolean values: `True` / `False` (Python convention)
- Decimal separator: dot (`.`)
- No BOM
- Line endings: `\n` (LF)

## Example

```csv
competition,round_key,size,discipline,is_team_round,rank,start_no,handler,dog,breed,country,faults,refusals,time_faults,total_faults,time,speed,eliminated,judge,sct,mct,course_length
awc2024,ind_agility_intermediate,Intermediate,Agility,False,1,471,Dalton Meredith,Ag Ch Fandabidozi Eclipse Of Dust (Clippy),Border Collie,GBR,0,0,0.0,0.0,37.16,5.89,False,Thora Van Der Stock,435.0,73.0,219.0
awc2024,ind_agility_intermediate,Intermediate,Agility,False,,571,Dave Munnings,Ah Ch Comebyanaway Wot A Legacy,Border Collie,GBR,,,,,,,True,Thora Van Der Stock,435.0,73.0,219.0
```
