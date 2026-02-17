# KACR.info - Databáze Agility (zdroj dat)

## O systému

**URL:** https://kacr.info
**Provozovatel:** KAČR (Klub Agility České Republiky)
**Kontakt:** kacr.palata@email.cz

Systém nemá veřejné API ani export do CSV. Data jsou dostupná pouze jako HTML stránky a je nutné je scrapovat.

## URL struktura

### Vyhledávání závodů

```
https://kacr.info/search/{query}?type=competition
```

Příklad: `https://kacr.info/search/prague%20agility?type=competition`

### Detail závodu (competition)

```
https://kacr.info/competitions/{competition_id}
```

Obsahuje:
- Metadata závodu (číslo závodu, datum, místo, terén, GPS)
- Seznam rozhodčích (s linky na `/judges/{id}`)
- Organizátor (link na `/handlers/{id}`)
- **Seznam všech běhů** - linky na `/runs/{run_id}` s popisem (den, typ, kategorie)

### Výsledky běhu (run)

```
https://kacr.info/runs/{run_id}
```

Obsahuje:
- **Parametry parkuru:** standardní čas, maximální čas, délka, počet překážek, požadovaná rychlost, rozhodčí
- **Tabulka výsledků** se sloupci:

| Sloupec | Popis | Příklad |
|---------|-------|---------|
| # | Pořadí | 1 |
| Průkaz | Číslo výkonnostního průkazu | 12345 |
| Psovod | Jméno psovoda (link na `/handlers/{id}`) | Anna Muszyńska |
| Pes | Jméno psa (link na `/dogs/{id}`) | NNL Ice Tea |
| Chb | Chyby (shozené překážky) | 0 |
| Odm | Odmítnutí | 0 |
| TB za čas | Trestné body za čas | 0.0 |
| TB | Celkové trestné body | 0.0 |
| Čas | Čas v sekundách | 33.49 |
| m/s | Rychlost | 6.42 |
| Status | V, VD, D, BO, DIS | V |

**Statusy výsledků:**
- **V** = Výborně (Excellent)
- **VD** = Velmi dobře (Very Good)
- **D** = Dobře (Good)
- **BO** = Bez ohodnocení (No classification)
- **DIS** = Diskvalifikace (Disqualified)

### Další entity

```
https://kacr.info/handlers/{handler_id}   - profil psovoda
https://kacr.info/dogs/{dog_id}           - profil psa
https://kacr.info/judges/{judge_id}       - profil rozhodčího
```

## Závody relevantní pro ADW Rating

### Prague Agility Party

| Ročník | Competition ID | URL | Datum |
|--------|---------------|-----|-------|
| 2022 | 3707 | https://kacr.info/competitions/3707 | 8.-10.7.2022 |
| 2023 | 4331 | https://kacr.info/competitions/4331 | 14.-16.7.2023 |
| 2024 | 4730 | https://kacr.info/competitions/4730 | 19.-21.7.2024 |
| 2025 | 5211 | https://kacr.info/competitions/5211 | 8.-10.8.2025 |

### Moravia Open

| Ročník | Competition ID | URL | Datum |
|--------|---------------|-----|-------|
| 2015 | 1628 | https://kacr.info/competitions/1628 | 2015 |
| 2016 | 1918 | https://kacr.info/competitions/1918 | 2016 |
| 2017 | 2203 | https://kacr.info/competitions/2203 | 2017 |
| 2018 | 2476 | https://kacr.info/competitions/2476 | 2018 |
| 2021 | 3589 | https://kacr.info/competitions/3589 | 2.-4.7.2021 |
| 2023 | 4352 | https://kacr.info/competitions/4352 | 2023 |
| 2024 | 4738 | https://kacr.info/competitions/4738 | 5.-7.7.2024 |
| 2025 | 4925 | https://kacr.info/competitions/4925 | 4.-6.7.2025 |

## Postup stahování dat

### Krok 1: Získat seznam běhů ze závodu

Stáhnout HTML stránku `https://kacr.info/competitions/{id}` a extrahovat všechny linky ve formátu `/runs/{run_id}` spolu s popisem (den, typ běhu, velikostní kategorie).

### Krok 2: Stáhnout výsledky jednotlivých běhů

Pro každý `/runs/{run_id}`:
1. Stáhnout HTML stránku
2. Z hlavičky extrahovat parametry parkuru (čas, délka, překážky, rozhodčí)
3. Z tabulky extrahovat řádky výsledků

### Krok 3: Parsování HTML tabulky

Data jsou server-side renderovaná (žádný AJAX/API). Stačí parsovat standardní HTML `<table>` s `<thead>` a `<tbody>`. Linky v buňkách obsahují ID psovodů a psů.

### Technické poznámky

- **Žádné API** - pouze HTML scraping
- **Žádný export** - žádné CSV/JSON/XML
- **Rate limiting** - není známo, ale doporučuji přidat delay mezi requesty (1-2s)
- **Registrace** - pro čtení výsledků není potřeba registrace
- **Jazyky** - stránky jsou v češtině, lze přepnout do angličtiny (toggle CZ/EN na stránce)
- Velikostní kategorie: **XS** (extra small), **S** (small), **M** (medium), **L** (large), **I** (intermediate)
