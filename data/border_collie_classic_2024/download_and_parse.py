#!/usr/bin/env python3
"""Download and parse Border Collie Classic 2024 finals from agilityplaza.com."""

import csv
import re
from pathlib import Path

import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
HTML_DIR = BASE_DIR / "html"
HTML_DIR.mkdir(exist_ok=True)

COMPETITION_NAME = "Border Collie Classic 2024"

CLASSES = {
    "1694309282": {"size": "Large", "discipline": "Final", "round_key": "ind_final_large_1"},
    "1446912462": {"size": "Intermediate", "discipline": "Final", "round_key": "ind_final_intermediate_1"},
}

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

COUNTRY_MAP = {
    "AT": "AUT", "CH": "CHE", "CZ": "CZE", "DE": "DEU", "ES": "ESP", "FR": "FRA",
    "GB": "GBR", "HR": "HRV", "IT": "ITA", "LT": "LTU", "NO": "NOR", "PL": "POL",
    "PT": "PRT", "SE": "SWE", "US": "USA",
}


def to_float(v: str):
    txt = (v or "").strip().replace(",", ".")
    if txt == "":
        return ""
    try:
        return float(txt)
    except Exception:
        return ""


def parse_country(raw: str) -> str:
    txt = (raw or "").strip().upper()
    if txt == "":
        return ""
    # Handles GB-E / GB-S style codes.
    txt = txt.split("-")[0]
    return COUNTRY_MAP.get(txt, "")


def parse_rank(token: str):
    t = (token or "").strip()
    m = re.match(r"^(\d+)", t)
    if not m:
        return ""
    return int(m.group(1))


def parse_handler_and_dog(raw: str):
    txt = (raw or "").strip()
    if " & " in txt:
        handler, dog = txt.split(" & ", 1)
        return handler.strip(), dog.strip()
    return txt, ""


def parse_run_data(run_data: str):
    parts = [p.strip() for p in (run_data or "").split(",") if p.strip() != ""]

    faults = 0.0
    refusals = 0
    time_faults = ""

    for p in parts:
        up = p.upper()
        if up == "R":
            refusals += 1
            continue
        if up.startswith("T"):
            time_faults = to_float(up[1:])
            continue
        val = to_float(p)
        if val != "":
            faults += float(val)

    return (faults if faults != 0 else 0), (refusals if refusals != 0 else 0), time_faults


def fetch_html(class_id: str) -> str:
    cache = HTML_DIR / f"class_{class_id}.html"
    if cache.exists():
        return cache.read_text(encoding="utf-8")

    url = f"https://www.agilityplaza.com/agilityClass/{class_id}/results"
    r = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    cache.write_text(r.text, encoding="utf-8")
    return r.text


def parse_eliminated_row(text: str):
    """Parse the 'Eliminated: Name1 & Dog1 (faults), Name2 & Dog2, ...' text."""
    # Remove the "Eliminated:" prefix
    text = re.sub(r"^Eliminated\s*:\s*", "", text, flags=re.IGNORECASE).strip()
    if not text:
        return []

    # Split by comma, but commas inside parentheses are fault data
    # Strategy: split on ", " followed by an uppercase letter (new entry)
    # Better: match "Name & Dog (faults)" or "Name & Dog" patterns
    parts = re.split(r",\s*(?=[A-ZÄÖÜÉÈÊËÀÁÂÃÅÆÇÐÑÒÓÔÕØÙÚÛÝÞ])", text)

    results = []
    for part in parts:
        part = part.strip()
        if not part:
            continue
        # Remove trailing fault info in parentheses
        name_part = re.sub(r'\s*\([^)]*\)\s*$', '', part).strip()
        # Remove HTML entities
        name_part = name_part.replace('"', '"').replace("&amp;", "&")
        handler, dog = parse_handler_and_dog(name_part)
        if handler:
            results.append({"handler": handler, "dog": dog})

    return results


def parse_class(class_id: str, cfg: dict):
    html = fetch_html(class_id)
    soup = BeautifulSoup(html, "html.parser")

    table = soup.find("table")
    if not table:
        return []

    entries = []
    elim_entries = []

    for tr in table.find_all("tr"):
        # Check for eliminated row (colspan=99)
        td_colspan = tr.find("td", attrs={"colspan": True})
        if td_colspan:
            raw_text = td_colspan.get_text(" ", strip=True)
            if "eliminated" in raw_text.lower():
                elim_entries = parse_eliminated_row(raw_text)
            continue

        cols = [" ".join(td.get_text(" ", strip=True).split()) for td in tr.find_all(["th", "td"])]
        if len(cols) < 8:
            continue
        if cols[0] == "Award":
            continue

        handler, dog = parse_handler_and_dog(cols[3])
        total_faults = to_float(cols[6])
        time_val = to_float(cols[7])

        entries.append({
            "handler": handler,
            "dog": dog,
            "country": parse_country(cols[4]),
            "total_faults": total_faults,
            "time": time_val,
        })

    # Assign ranks by sorting clean entries by (total_faults, time)
    entries.sort(key=lambda e: (
        float(e["total_faults"]) if e["total_faults"] != "" else 0,
        float(e["time"]) if e["time"] != "" else 999,
    ))

    rows = []
    for rank, e in enumerate(entries, 1):
        rows.append({
            "competition": COMPETITION_NAME,
            "round_key": cfg["round_key"],
            "size": cfg["size"],
            "discipline": cfg["discipline"],
            "is_team_round": "False",
            "rank": rank,
            "start_no": "",
            "handler": e["handler"],
            "dog": e["dog"],
            "breed": "",
            "country": e["country"],
            "faults": "",
            "refusals": "",
            "time_faults": "",
            "total_faults": e["total_faults"],
            "time": e["time"],
            "speed": "",
            "eliminated": "False",
            "judge": "",
            "sct": "",
            "mct": "",
            "course_length": "",
        })

    for e in elim_entries:
        rows.append({
            "competition": COMPETITION_NAME,
            "round_key": cfg["round_key"],
            "size": cfg["size"],
            "discipline": cfg["discipline"],
            "is_team_round": "False",
            "rank": "",
            "start_no": "",
            "handler": e["handler"],
            "dog": e["dog"],
            "breed": "",
            "country": "",
            "faults": "",
            "refusals": "",
            "time_faults": "",
            "total_faults": "",
            "time": "",
            "speed": "",
            "eliminated": "True",
            "judge": "",
            "sct": "",
            "mct": "",
            "course_length": "",
        })

    return rows


def main():
    all_rows = []

    for class_id, cfg in CLASSES.items():
        rows = parse_class(class_id, cfg)
        all_rows.extend(rows)
        print(f"class {class_id}: {len(rows)}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if str(r["rank"]).isdigit() else 999999,
            str(r["handler"]),
        )
    )

    out_csv = BASE_DIR / "border_collie_classic_2024_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
