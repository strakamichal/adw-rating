#!/usr/bin/env python3
"""Download and parse Norwegian Open 2025 results from devent public Firebase."""

import csv
import re
from pathlib import Path

import requests

BASE_DIR = Path(__file__).parent
JSON_PATH = BASE_DIR / "event_6f3ZZpeBoPE0l3akKKzd.json"

EVENT_ID = "6f3ZZpeBoPE0l3akKKzd"
COMPETITION_NAME = "Norwegian Open 2025"

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

SIZE_MAP = {
    "XS": "Small",
    "S": "Small",
    "M": "Medium",
    "L": "Intermediate",
    "XL": "Large",
}

COUNTRY_2_TO_3 = {
    "NO": "NOR", "SE": "SWE", "FI": "FIN", "DK": "DNK", "IS": "ISL", "FO": "FRO",
    "DE": "DEU", "NL": "NLD", "BE": "BEL", "FR": "FRA", "IT": "ITA", "ES": "ESP",
    "PT": "PRT", "GB": "GBR", "IE": "IRL", "CH": "CHE", "AT": "AUT", "CZ": "CZE",
    "SK": "SVK", "PL": "POL", "HU": "HUN", "SI": "SVN", "HR": "HRV", "LT": "LTU",
    "LV": "LVA", "EE": "EST", "US": "USA", "CA": "CAN", "AU": "AUS",
}

COUNTRY_NAME_TO_3 = {
    "norway": "NOR", "sweden": "SWE", "finland": "FIN", "denmark": "DNK", "iceland": "ISL",
    "germany": "DEU", "netherlands": "NLD", "belgium": "BEL", "france": "FRA", "italy": "ITA",
    "spain": "ESP", "portugal": "PRT", "united kingdom": "GBR", "ireland": "IRL", "switzerland": "CHE",
    "austria": "AUT", "czech republic": "CZE", "slovakia": "SVK", "poland": "POL", "hungary": "HUN",
    "slovenia": "SVN", "croatia": "HRV", "lithuania": "LTU", "latvia": "LVA", "estonia": "EST",
    "united states": "USA", "canada": "CAN", "australia": "AUS",
}


def to_float(v):
    if v is None:
        return ""
    if isinstance(v, (int, float)):
        return float(v)
    txt = str(v).strip().replace(",", ".")
    if txt == "":
        return ""
    try:
        return float(txt)
    except Exception:
        return ""


def to_int(v):
    if v is None:
        return ""
    if isinstance(v, int):
        return v
    txt = str(v).strip()
    if txt == "":
        return ""
    if re.fullmatch(r"\d+", txt):
        return int(txt)
    try:
        return int(float(txt.replace(",", ".")))
    except Exception:
        return ""


def flag_to_iso2(flag: str):
    flag = (flag or "").strip()
    if len(flag) < 2:
        return ""
    chars = [c for c in flag if ord(c) >= 127462 and ord(c) <= 127487]
    if len(chars) != 2:
        return ""
    return "".join(chr(ord(c) - 127397) for c in chars)


def country_from_run(run: dict):
    iso2 = flag_to_iso2(run.get("dogCountryFlag", ""))
    if iso2 in COUNTRY_2_TO_3:
        return COUNTRY_2_TO_3[iso2]

    country_name = (run.get("dogCountry") or "").strip().lower()
    return COUNTRY_NAME_TO_3.get(country_name, "")


def parse_discipline(comp_prefix: str) -> str:
    if comp_prefix == "H":
        return "Jumping"
    if comp_prefix == "FA":
        return "Final"
    return "Agility"


def parse_size(comp_key: str) -> str:
    parts = comp_key.split("-")
    if len(parts) < 4:
        return ""
    return SIZE_MAP.get(parts[3], "")


def fetch_event():
    if JSON_PATH.exists():
        return requests.models.complexjson.loads(JSON_PATH.read_text(encoding="utf-8"))

    url = f"https://devent-db.europe-west1.firebasedatabase.app/events/{EVENT_ID}.json"
    r = requests.get(url, timeout=60, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    JSON_PATH.write_text(r.text, encoding="utf-8")
    return r.json()


def main():
    event = fetch_event()
    all_rows = []

    runs_by_competition = event.get("runs", {})

    for comp_key, comp_runs in runs_by_competition.items():
        comp_prefix = comp_key.split("-")[0]
        discipline = parse_discipline(comp_prefix)
        size = parse_size(comp_key)
        round_key = re.sub(r"[^a-z0-9_]+", "_", comp_key.lower())

        count = 0
        for run in comp_runs.values():
            placement = to_int(run.get("placement"))
            dnf = bool(run.get("dnf"))
            dns = bool(run.get("dns"))
            eliminated = dnf or dns or (isinstance(placement, int) and placement >= 900)
            rank = "" if eliminated else placement

            all_rows.append({
                "competition": COMPETITION_NAME,
                "round_key": round_key,
                "size": size,
                "discipline": discipline,
                "is_team_round": "True" if bool(run.get("isTeamRun")) else "False",
                "rank": rank,
                "start_no": to_int(run.get("tri")),
                "handler": (run.get("handlerName") or "").strip(),
                "dog": (run.get("dogName") or "").strip(),
                "breed": (run.get("dogBreed") or "").strip(),
                "country": country_from_run(run),
                "faults": to_float(run.get("faults")),
                "refusals": to_float(run.get("halts")),
                "time_faults": to_float(run.get("timeFault")),
                "total_faults": to_float(run.get("sum")),
                "time": to_float(run.get("time")),
                "speed": "",
                "eliminated": "True" if eliminated else "False",
                "judge": "",
                "sct": "",
                "mct": "",
                "course_length": "",
            })
            count += 1

        print(f"{comp_key}: {count}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if str(r["rank"]).isdigit() else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "norwegian_open_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
