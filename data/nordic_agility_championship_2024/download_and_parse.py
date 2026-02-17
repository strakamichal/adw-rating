#!/usr/bin/env python3
"""Download and parse Nordic Agility Championship 2024 from agilityevents.dk."""

import csv
import json
from collections import defaultdict
from pathlib import Path

import requests

BASE_DIR = Path(__file__).parent
JSON_DIR = BASE_DIR / "json"
JSON_DIR.mkdir(exist_ok=True)

COMPETITION_ID = "8c84b158-30f9-4d98-d89a-08dbdb93c275"
COMPETITION_NAME = "Nordic Agility Championship 2024"

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

DOG_SIZE_MAP = {
    2: "Small",
    4: "Medium",
    8: "Intermediate",
    16: "Large",
}

TRIAL_TYPE_MAP = {
    2: "Agility",
    128: "Jumping",
    100663296: "Final",
}

COUNTRY_2_TO_3 = {
    "NO": "NOR",
    "SE": "SWE",
    "FI": "FIN",
    "DK": "DNK",
    "IS": "ISL",
    "FO": "FRO",
}

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
    "Referer": f"https://agilityevents.dk/event/{COMPETITION_ID}",
    "Accept": "application/json,text/plain,*/*",
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
    if txt.isdigit():
        return int(txt)
    try:
        return int(float(txt.replace(",", ".")))
    except Exception:
        return ""


def fetch_json(url: str, cache_path: Path):
    if cache_path.exists():
        return json.loads(cache_path.read_text(encoding="utf-8"))

    r = requests.get(url, headers=HEADERS, timeout=40)
    r.raise_for_status()
    cache_path.write_text(r.text, encoding="utf-8")
    return r.json()


def main():
    switchers = fetch_json(
        f"https://agilityevents.dk/Results/Competition/{COMPETITION_ID}/Trials/Switcher",
        JSON_DIR / "trials_switcher.json",
    )

    counters = defaultdict(int)
    all_rows = []

    for sw in switchers:
        trial_type = sw.get("trialType")
        dog_size = sw.get("dogSize")
        trial_id = sw.get("trialId")

        if trial_type not in TRIAL_TYPE_MAP:
            continue
        if dog_size not in DOG_SIZE_MAP:
            continue

        discipline = TRIAL_TYPE_MAP[trial_type]
        size = DOG_SIZE_MAP[dog_size]

        counters[(discipline, size)] += 1
        seq = counters[(discipline, size)]
        round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"

        trial_results = fetch_json(
            f"https://agilityevents.dk/Results/Trial/{trial_id}/Results",
            JSON_DIR / f"trial_{trial_id}_results.json",
        )

        for row in trial_results:
            eliminated = bool(row.get("disqualified"))
            rank_raw = to_int(row.get("placement"))
            rank = "" if eliminated else rank_raw

            country2 = str(row.get("handlerCountry") or "").strip().upper()
            country = COUNTRY_2_TO_3.get(country2, "")

            all_rows.append({
                "competition": COMPETITION_NAME,
                "round_key": round_key,
                "size": size,
                "discipline": discipline,
                "is_team_round": "False",
                "rank": rank,
                "start_no": to_int(row.get("startNo")),
                "handler": str(row.get("handlerFullName") or "").strip(),
                "dog": str(row.get("dogCallingName") or row.get("dogPedigreeName") or "").strip(),
                "breed": str(row.get("dogBreedFull") or row.get("dogBreed") or "").strip(),
                "country": country,
                "faults": to_float(row.get("fault")),
                "refusals": to_float(row.get("refusal")),
                "time_faults": "",
                "total_faults": to_float(row.get("faultTotal")),
                "time": to_float(row.get("time_Seconds")),
                "speed": to_float(row.get("speed")),
                "eliminated": "True" if eliminated else "False",
                "judge": str(row.get("judge") or "").strip(),
                "sct": to_float(row.get("sct")),
                "mct": to_float(row.get("mct")),
                "course_length": to_float(row.get("lengthMeters")),
            })

        print(f"{trial_id} {discipline} {size}: {len(trial_results)}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if str(r["rank"]).isdigit() else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "nordic_agility_championship_2024_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
