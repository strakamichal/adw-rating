#!/usr/bin/env python3
"""Download and parse FMBB World Championship 2025 finals from agilitynews.eu."""

import csv
from pathlib import Path

import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
COMPETITION_NAME = "FMBB World Championship 2025"

SOURCES = {
    "agility": "https://agilitynews.eu/?p=6256",
    "jumping": "https://agilitynews.eu/?p=6245",
}

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

COUNTRY_TO_ISO3 = {
    "Austria": "AUT",
    "Belgium": "BEL",
    "Czechia": "CZE",
    "Denmark": "DNK",
    "Finland": "FIN",
    "France": "FRA",
    "Germany": "DEU",
    "Greece": "GRC",
    "Hungary": "HUN",
    "Italy": "ITA",
    "Netherlands": "NLD",
    "Poland": "POL",
    "Romania": "ROU",
    "Slovakia": "SVK",
    "Spain": "ESP",
    "Sweden": "SWE",
    "Switzerland": "CHE",
    "United Kingdom": "GBR",
}


def to_float(v: str):
    v = (v or "").strip().replace(",", ".")
    if not v:
        return ""
    try:
        return float(v)
    except Exception:
        return ""


COUNTRY_NAMES = sorted(COUNTRY_TO_ISO3.keys(), key=len, reverse=True)


def split_handler_country(value: str):
    txt = (value or "").strip()
    for cname in COUNTRY_NAMES:
        if txt == cname:
            return "", COUNTRY_TO_ISO3[cname]
        suffix = f" {cname}"
        if txt.endswith(suffix):
            return txt[: -len(suffix)].strip(), COUNTRY_TO_ISO3[cname]
    return txt, ""


def parse_page(url: str, discipline: str):
    html = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"}).text
    soup = BeautifulSoup(html, "html.parser")
    table = soup.find("table")
    rows = []
    if not table:
        return rows

    for tr in table.find_all("tr"):
        cols = [c.get_text(" ", strip=True) for c in tr.find_all(["td", "th"])]
        if len(cols) < 6:
            continue
        rank_token = cols[0].strip()
        if not rank_token:
            continue

        if rank_token.isdigit():
            rank = int(rank_token)
            eliminated = "False"
        else:
            rank = ""
            eliminated = "True"

        start_no = cols[1].strip()

        # "Jennifer Stephenson Switzerland" -> handler + country
        handler, country = split_handler_country(cols[2].strip())

        dog = cols[3].strip()
        fault_time_raw = cols[4].strip()
        if "elim" in fault_time_raw.lower():
            total_faults = ""
            time_val = ""
        else:
            fault_time = fault_time_raw.split()
            total_faults = to_float(fault_time[0]) if len(fault_time) >= 1 else ""
            time_val = to_float(fault_time[1]) if len(fault_time) >= 2 else ""
        breed = cols[5].strip()

        rows.append({
            "competition": COMPETITION_NAME,
            "round_key": f"ind_{discipline.lower()}_large_1",
            "size": "Large",
            "discipline": discipline,
            "is_team_round": "False",
            "rank": rank,
            "start_no": start_no,
            "handler": handler,
            "dog": dog,
            "breed": breed,
            "country": country,
            "faults": "",
            "refusals": "",
            "time_faults": "",
            "total_faults": total_faults,
            "time": time_val,
            "speed": "",
            "eliminated": eliminated,
            "judge": "",
            "sct": "",
            "mct": "",
            "course_length": "",
        })

    return rows


def main():
    all_rows = []
    for discipline, url in SOURCES.items():
        rows = parse_page(url, discipline.title())
        print(f"{discipline}: {len(rows)}")
        all_rows.extend(rows)

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            r["start_no"],
        )
    )

    out_csv = BASE_DIR / "fmbb_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
