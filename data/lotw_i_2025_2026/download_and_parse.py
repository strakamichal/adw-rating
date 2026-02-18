#!/usr/bin/env python3
"""Download and parse Lord of the Winter 2025/2026 from AgilityManager API.

Each LOTW round (I, II, III, IV) is treated as a separate competition,
producing its own CSV file.
"""

import csv
import json
import re
import urllib.request
from collections import Counter, defaultdict
from pathlib import Path

BASE_DIR = Path(__file__).parent
API_BASE = "https://api.agilitymanager.com/api/v1"

# Each round is a separate competition — IV not yet available
EVENTS = [
    {
        "id": "45c7ce5a-62aa-42d3-95aa-4e325285d1d5",
        "name": "Lord of the Winter I. 2025/2026",
        "csv": "lotw_i_2025_2026_results.csv",
        "json": "lotw_i_2025_2026_raw.json",
    },
    {
        "id": "4deed4be-0334-49fd-b4b8-eb40bbb2bf9f",
        "name": "Lord of the Winter II. 2025/2026",
        "csv": "lotw_ii_2025_2026_results.csv",
        "json": "lotw_ii_2025_2026_raw.json",
    },
    {
        "id": "de185f7f-ba7b-4e47-a6ee-a3620d069a75",
        "name": "Lord of the Winter III. 2025/2026",
        "csv": "lotw_iii_2025_2026_results.csv",
        "json": "lotw_iii_2025_2026_raw.json",
    },
]

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

SIZE_MAP = {
    "small": "Small",
    "medium": "Medium",
    "intermediate": "Intermediate",
    "large": "Large",
}

COUNTRY_2_TO_3 = {
    "AT": "AUT", "BE": "BEL", "BG": "BGR", "BR": "BRA", "CA": "CAN", "CH": "CHE",
    "CZ": "CZE", "DE": "DEU", "DK": "DNK", "ES": "ESP", "FI": "FIN", "FR": "FRA",
    "GB": "GBR", "HR": "HRV", "HU": "HUN", "IT": "ITA", "LU": "LUX", "NL": "NLD",
    "NO": "NOR", "PL": "POL", "PT": "PRT", "RS": "SRB", "SE": "SWE", "SI": "SVN",
    "SK": "SVK", "UA": "UKR", "US": "USA",
}


def api_get(path: str):
    req = urllib.request.Request(
        f"{API_BASE}{path}",
        headers={"User-Agent": "ADW-Rating/1.0"},
    )
    with urllib.request.urlopen(req, timeout=60) as resp:
        return json.loads(resp.read().decode("utf-8"))


def normalize_size(name: str) -> str:
    key = (name or "").strip().lower()
    return SIZE_MAP.get(key, name.strip().title())


def classify_discipline(run_name: str, run_type: str) -> str:
    text = f"{run_name} {run_type}".lower()
    if "jump" in text:
        return "Jumping"
    if "agility" in text or text.startswith("a"):
        return "Agility"
    if "final" in text:
        return "Final"
    return run_type.strip().title() if run_type else ""


def normalize_country(raw: str) -> str:
    txt = (raw or "").strip().upper()
    if not txt:
        return ""
    if len(txt) == 2 and txt.isalpha():
        return COUNTRY_2_TO_3.get(txt, "")
    if len(txt) == 3 and txt.isalpha():
        return txt
    return ""


def to_num(v):
    if v is None or v == "":
        return ""
    return v


def to_float(v):
    if v is None or v == "":
        return ""
    try:
        return float(v)
    except Exception:
        return ""


def parse_event(event: dict) -> list[dict]:
    """Download and parse a single LOTW event, return list of row dicts."""
    event_id = event["id"]
    comp_name = event["name"]
    print(f"Downloading {comp_name} ({event_id})...")

    comp = api_get(f"/competition/{event_id}")

    # Save raw JSON
    raw_path = BASE_DIR / event["json"]
    raw_path.write_text(json.dumps(comp, ensure_ascii=False, indent=2), encoding="utf-8")

    runs = sorted(comp.get("runs") or [], key=lambda r: r.get("id", 0))
    counters = defaultdict(int)
    rows = []

    for run in runs:
        run_name = (run.get("name") or "").strip()
        run_type = (run.get("run_type") or "").strip()
        discipline = classify_discipline(run_name, run_type)
        judge = (run.get("judge") or "").strip()

        for sl in sorted(run.get("starting_lists") or [], key=lambda s: s.get("id", 0)):
            size = normalize_size(sl.get("name") or "")
            if size not in {"Small", "Medium", "Intermediate", "Large"}:
                continue

            key = (discipline, size)
            counters[key] += 1
            seq = counters[key]
            round_key = f"ind_{discipline}_{size}_{seq}"
            round_key = re.sub(r"[^a-z0-9_]+", "_", round_key.lower()).strip("_")

            sct = to_float(sl.get("standard_time"))
            mct = to_float(sl.get("max_time"))
            course_len = to_float(sl.get("course_length"))

            for rec in sl.get("records") or []:
                dis = bool(rec.get("dis"))
                not_running = bool(rec.get("not_running"))
                ended = bool(rec.get("ended"))

                rank_raw = rec.get("rank")
                rank = ""
                if not dis and not not_running and ended:
                    try:
                        ir = int(rank_raw)
                        if ir > 0:
                            rank = ir
                    except Exception:
                        rank = ""

                eliminated = dis or not_running or (not ended) or rank == ""

                time_val = to_float(rec.get("time"))
                speed = ""
                if course_len not in ("", 0) and time_val not in ("", 0):
                    try:
                        speed = round(float(course_len) / float(time_val), 2)
                    except Exception:
                        speed = ""

                rows.append({
                    "competition": comp_name,
                    "round_key": round_key,
                    "size": size,
                    "discipline": discipline,
                    "is_team_round": "False",
                    "rank": rank,
                    "start_no": rec.get("starting_number") or "",
                    "handler": (rec.get("handler") or "").strip(),
                    "dog": (rec.get("dog") or "").strip(),
                    "breed": "",
                    "country": normalize_country(rec.get("country")),
                    "faults": to_num(rec.get("faults")),
                    "refusals": to_num(rec.get("refusal")),
                    "time_faults": "",
                    "total_faults": to_num(rec.get("penalty_points")),
                    "time": time_val,
                    "speed": speed,
                    "eliminated": "True" if eliminated else "False",
                    "judge": judge,
                    "sct": sct,
                    "mct": mct,
                    "course_length": course_len,
                })

    rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            str(r["start_no"]),
        )
    )

    # Write CSV
    out_csv = BASE_DIR / event["csv"]
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS, lineterminator="\n")
        w.writeheader()
        w.writerows(rows)

    # Summary
    round_counts = Counter(r["round_key"] for r in rows)
    print(f"  Runs: {len(runs)}, Rows: {len(rows)}, Rounds: {len(round_counts)}")
    print(f"  CSV: {out_csv}")
    for rk in sorted(round_counts):
        print(f"    {rk}: {round_counts[rk]}")

    return rows


def main():
    total = 0
    for event in EVENTS:
        rows = parse_event(event)
        total += len(rows)
    print(f"\nTotal rows across all events: {total}")


if __name__ == "__main__":
    main()
