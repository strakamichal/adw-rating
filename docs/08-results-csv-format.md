# Formát CSV s výsledky závodů

Tento dokument popisuje cílovou strukturu CSV souboru s výsledky agility závodů. Každý závod by měl být uložen v jednom CSV souboru, pojmenovaném podle konvence `<event_slug>_results.csv` (např. `awc2025_results.csv`).

## Sloupce

| # | Sloupec | Typ | Povinný | Popis |
|---|---------|-----|---------|-------|
| 1 | `competition` | string | ano | Název závodu (např. `AWC 2025`) |
| 2 | `round_key` | string | ano | Unikátní identifikátor běhu v rámci závodu. Konvence: `{typ}_{disciplína}_{kategorie}` — viz sekce níže |
| 3 | `size` | string | ano | Velikostní kategorie: `Small`, `Medium`, `Intermediate`, `Large` |
| 4 | `discipline` | string | ano | Disciplína: `Jumping` nebo `Agility` |
| 5 | `is_team_round` | bool | ano | `True` pokud jde o týmový běh, `False` pro individuální |
| 6 | `rank` | int \| prázdný | ne | Pořadí v běhu. Prázdné pro eliminované psy (eliminated = True) |
| 7 | `start_no` | int | ano | Startovní číslo |
| 8 | `handler` | string | ano | Jméno psovoda |
| 9 | `dog` | string | ano | Jméno psa (plemenný název + volací jméno v závorce, např. `Border star hall of fame (Fame)`) |
| 10 | `breed` | string | ne | Plemeno psa (např. `Border Collie`, `Shetland Sheepdog`). Může být prázdné |
| 11 | `country` | string | ano | Země — ISO 3166-1 alpha-3 kód (např. `CZE`, `DEU`, `GBR`) |
| 12 | `faults` | int \| prázdný | ne | Počet chyb na překážkách (každá = 5 trestných bodů). Prázdné u eliminovaných |
| 13 | `refusals` | int \| prázdný | ne | Počet odmítnutí (každé = 5 trestných bodů). Prázdné u eliminovaných |
| 14 | `time_faults` | float \| prázdný | ne | Časové trestné body (překročení SCT). Prázdné u eliminovaných |
| 15 | `total_faults` | float \| prázdný | ne | Celkové trestné body = `faults × 5 + refusals × 5 + time_faults`. Prázdné u eliminovaných |
| 16 | `time` | float \| prázdný | ne | Dosažený čas v sekundách. Prázdné u eliminovaných |
| 17 | `speed` | float \| prázdný | ne | Rychlost v m/s (`course_length / time`). Prázdné u eliminovaných |
| 18 | `eliminated` | bool | ano | `True` pokud byl pes diskvalifikován (DIS), jinak `False` |
| 19 | `judge` | string | ne | Jméno rozhodčího |
| 20 | `sct` | float \| prázdný | ne | Standardní čas v sekundách (Standard Course Time) |
| 21 | `mct` | float \| prázdný | ne | Maximální čas v sekundách (Maximum Course Time) |
| 22 | `course_length` | float \| prázdný | ne | Délka trati v metrech |

## Konvence pro `round_key`

Formát: `{typ}_{disciplína}_{velikost}[_ind]`

- **typ**: `team` (týmový) nebo `ind` (individuální)
- **disciplína**: `jumping` nebo `agility`
- **velikost**: `small`, `medium`, `intermediate`, `large`
- Týmové běhy mají suffix `_ind` — označuje individuální výsledky v rámci týmového běhu

Příklady:
- `ind_jumping_large` — individuální jumping, kategorie Large
- `team_agility_intermediate_ind` — týmová agility, kategorie Intermediate (individuální výsledky)

## Prázdné hodnoty

- U eliminovaných psů (`eliminated = True`) jsou sloupce `rank`, `faults`, `refusals`, `time_faults`, `total_faults`, `time`, `speed` **prázdné** (ne nulové).
- Sloupec `breed` může být prázdný, pokud plemeno nebylo uvedeno ve zdroji.

## Řazení

Řádky jsou řazeny primárně podle `round_key`, sekundárně podle `rank` (neeliminovaní první, eliminovaní na konci bez pořadí).

## Kódování a formát

- Kódování: UTF-8
- Oddělovač: čárka (`,`)
- Bez uvozovek kolem polí, pokud pole neobsahuje čárku — v tom případě standardní CSV quoting
- Booleovské hodnoty: `True` / `False` (Python konvence)
- Desetinný oddělovač: tečka (`.`)
- Bez BOM
- Řádky ukončeny `\n` (LF)

## Příklad

```csv
competition,round_key,size,discipline,is_team_round,rank,start_no,handler,dog,breed,country,faults,refusals,time_faults,total_faults,time,speed,eliminated,judge,sct,mct,course_length
AWC 2025,ind_jumping_large,Large,Jumping,False,1,574,Lisa Frick,Crazyland Say the Word (Siri),Border Collie,AUT,0,0,0.0,0.0,28.21,6.38,False,Mark Fonteijn,37.0,67.0,180.0
AWC 2025,ind_jumping_large,Large,Jumping,False,,571,Dave Munnings,Ah Ch Comebyanaway Wot A Legacy,Border Collie,GBR,,,,,,,True,Mark Fonteijn,37.0,67.0,180.0
```
