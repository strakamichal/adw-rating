# agigames.cz - výsledkový systém (zdroj dat)

## O systému

**URL:** http://new.agigames.cz (dříve a.dogco.cz, redirect 301)
**Typ:** Časomíra a výsledkový systém pro agility závody

Systém nemá veřejné API. Data jsou v HTML stránkách, nutný scraping.

## URL struktura

### Hlavní stránka závodu

```
http://new.agigames.cz/tv_home.php?zid={zid}
```

Obsahuje seznam všech běhů (runs) organizovaných po dnech a ringách, s linky na výsledky.

### Výsledky běhu

```
http://new.agigames.cz/tv_results.php?zid={zid}&bid={bid}
```

- `zid` = ID závodu (competition)
- `bid` = ID běhu (bracket/run)

### Profil závodníka

```
http://new.agigames.cz/tv_me.php?zid={zid}&tid={tid}
```

- `tid` = ID týmu (handler + pes)

## HTML struktura výsledkové tabulky

Tabulka má **4 sloupce** (bez hlavičky):

| Sloupec | Obsah | Jak parsovat |
|---------|-------|--------------|
| [0] Rank | Medaile (img `medal_1/2/3.png`) nebo číslo s tečkou ("5.") | Regex na `medal_(\d)` z img src, nebo text `.rstrip(".")` |
| [1] Handler + Pes | Kombinovaná buňka - viz detail níže | Složitější parsing |
| [2] Chyby | Chyby, odmítnutí, TB oddělené `<br/>` | Rozdělit text separátorem |
| [3] Čas | Čas (sec) + rychlost (m/s) oddělené `<br/>` | Regex `(\d+\.\d+)\s*sec` a `(\d+\.\d+)\s*m/s` |

### Detail sloupce [1] - Handler + Pes

```html
<td>
  <span style="font-weight:bold;">
    <a href="/tv_me.php?zid=25&tid=4715">
      <span title="skupina - závodní číslo">[14-460]</span> Casado Axular
    </a>
  </span>
  <span style="font-size:80%">L A3 | <span style="font-style:italic;">2two-2two</span></span>
  <img class="vlajka" src="/pic/flags/svg/4x3/es.svg"/>
  <br/>
  <span><img src="/pic/dogs/..."/>Eywa "Eywa"</span>
</td>
```

Extrahuje se:
- **handler** - text v bold `<a>` tagu (bez prefixu `[group-number]`)
- **start_num** - z `<span title="skupina...">` (např. "14-460")
- **team_id** - z URL parametru `tid=` v `<a>` href
- **size_class** - regex `(XS|S|M|L|I)\s+(A[123])` z textu
- **kennel** - z `<span style="font-style:italic">`
- **country** - z `<img class="vlajka">` src path (2-letter code)
- **dog** - text ve `<span>` po `<br/>`

## Závody relevantní pro ADW Rating

### Prague Agility Party 2024

| Parametr | Hodnota |
|----------|---------|
| zid | 25 |
| URL | http://new.agigames.cz/tv_home.php?zid=25 |
| Datum | 19.-21.7.2024 |
| Běhů | 40 |
| Scraper | `scripts/scrape_agigames.py` |

## Postup stahování

### Krok 1: Seznam běhů

Stáhnout `tv_home.php?zid={zid}`, najít všechny `<a>` s `href` obsahujícím `bid=` a `results`.

### Krok 2: Výsledky běhů

Pro každý `bid` stáhnout `tv_results.php?zid={zid}&bid={bid}` a parsovat tabulku dle struktury výše.

### Technické poznámky

- **Redirect:** `a.dogco.cz` → `new.agigames.cz` (301), použít `allow_redirects=True`
- **Žádné API** - pouze HTML scraping
- **Rate limiting** - přidat delay 1s mezi requesty
- **Registrace** - pro čtení výsledků není potřeba
- **Emoji** - v poli TB se používá 👌 místo 0 (čistý běh)
