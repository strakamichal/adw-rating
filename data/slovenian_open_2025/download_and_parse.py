#!/usr/bin/env python3
"""Download and parse Slovenian Agility Open 2025 from AgilityManager API."""

import csv
import json
import re
import urllib.request
from collections import defaultdict
from pathlib import Path

BASE_DIR = Path(__file__).parent
API_BASE = "https://api.agilitymanager.com/api/v1"
COMPETITION_ID = "667b373c-516e-47ff-b62f-be5f69987df6"
COMPETITION_NAME = "Slovenian Agility Open 2025"

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
    if "agility" in text:
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


def main():
    comp = api_get(f"/competition/{COMPETITION_ID}")
    raw_path = BASE_DIR / "agilitymanager_competition.json"
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
            round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"
            round_key = re.sub(r"[^a-z0-9_]+", "_", round_key)

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
                    "competition": COMPETITION_NAME,
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

    out_csv = BASE_DIR / "slovenian_open_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS, lineterminator="\n")
        w.writeheader()
        w.writerows(rows)

    print(f"Runs: {len(runs)}")
    print(f"Rows: {len(rows)}")
    print(f"CSV: {out_csv}")
    print(f"Raw: {raw_path}")


if __name__ == "__main__":
    main()
