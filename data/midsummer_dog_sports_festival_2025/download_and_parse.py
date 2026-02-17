#!/usr/bin/env python3
"""Download and parse Midsummer Dog Sports Festival 2025 results from Tolleri."""

import csv
import re
from pathlib import Path

import requests

BASE_DIR = Path(__file__).parent
CACHE_DIR = BASE_DIR / "json"
CACHE_DIR.mkdir(exist_ok=True)

COMPETITION_NAME = "Midsummer Dog Sports Festival 2025"
ORGANIZER_ID = "-306421049"
DATES = ["2025-06-19", "2025-06-20", "2025-06-21", "2025-06-22"]

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

SIZE_PREFIX_MAP = {
    "XS": "Small",
    "S": "Small",
    "M": "Medium",
    "SL": "Intermediate",
    "L": "Large",
}

SKIP_NAME_TOKENS = (
    "team relay",
    "running order",
    " team",
    " j0",
)


def slug(s: str) -> str:
    return re.sub(r"[^a-z0-9]+", "_", (s or "").lower()).strip("_")


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


def parse_size(class_code: str) -> str:
    code = (class_code or "").upper()
    for prefix in ("XS", "SL", "S", "M", "L"):
        if code.startswith(prefix):
            return SIZE_PREFIX_MAP[prefix]
    return ""


def parse_discipline(name: str) -> str:
    low = (name or "").lower()
    if "hyppy" in low or "jump" in low:
        return "Jumping"
    if "final" in low or "finaal" in low:
        return "Final"
    return "Agility"


def include_class(meta: dict) -> bool:
    class_type = str(meta.get("kilpailu_tyyppi", "")).strip()
    name = (meta.get("kilpailu_kuvaus") or "").lower()

    if class_type not in {"1", "12"}:
        return False

    if any(token in name for token in SKIP_NAME_TOKENS):
        return False

    if parse_size(meta.get("luokka_koodi", "")) == "":
        return False

    return True


def fetch_json(url: str, cache_path: Path):
    if cache_path.exists():
        return requests.models.complexjson.loads(cache_path.read_text(encoding="utf-8"))

    r = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    cache_path.write_text(r.text, encoding="utf-8")
    return r.json()


def parse_class(date_str: str, meta: dict):
    competition_id = meta["kilpailu_id"]
    class_code = meta["luokka_koodi"]

    class_url = (
        "https://tolleri.net/tulospalvelu/luokka/"
        f"?paiva={date_str}&jarjestaja_id={ORGANIZER_ID}"
        f"&kilpailu_id={competition_id}&luokka_koodi={class_code}"
    )
    cache = CACHE_DIR / f"class_{date_str}_{competition_id}_{class_code}.json"
    data = fetch_json(class_url, cache)

    size = parse_size(class_code)
    discipline = parse_discipline(meta.get("kilpailu_kuvaus", ""))
    round_key = f"{date_str.replace('-', '')}_{competition_id}_{slug(class_code)}"

    judge = (data.get("tuomari") or "").strip()
    sct = to_float(data.get("ihanneaika"))
    mct = to_float(data.get("enimmaisaika"))
    course_length = to_float(data.get("pituus"))

    rows = []
    for result in data.get("tulokset", []):
        note = (result.get("muuta") or "").strip().upper()
        raw_rank = to_int(result.get("sijoitus"))
        rank = raw_rank if isinstance(raw_rank, int) and raw_rank > 0 else ""

        eliminated = note in {"HYL", "DIS", "EL", "ELIM", "ABS", "DNS", "POIS", "KESK"}
        if rank == "" and note != "":
            eliminated = True

        rows.append({
            "competition": COMPETITION_NAME,
            "round_key": round_key,
            "size": size,
            "discipline": discipline,
            "is_team_round": "False",
            "rank": "" if eliminated else rank,
            "start_no": str(result.get("numero") or "").strip(),
            "handler": str(result.get("ohjaaja") or "").strip(),
            "dog": str(result.get("virallinenNimi") or result.get("kutsumanimi") or "").strip(),
            "breed": str(result.get("rotunimi") or "").strip(),
            "country": "",
            "faults": to_float(result.get("ratavirhe")),
            "refusals": to_float(result.get("kieltovirheita")),
            "time_faults": to_float(result.get("aikavirhe")),
            "total_faults": to_float(result.get("tulos")),
            "time": to_float(result.get("suoritusaika")),
            "speed": to_float(result.get("nopeus")),
            "eliminated": "True" if eliminated else "False",
            "judge": judge,
            "sct": sct,
            "mct": mct,
            "course_length": course_length,
        })

    return round_key, rows


def main():
    all_rows = []

    for date_str in DATES:
        event_url = f"https://tolleri.net/tulospalvelu/tapahtuma/?paiva={date_str}&jarjestaja_id={ORGANIZER_ID}"
        event_cache = CACHE_DIR / f"event_{date_str}.json"
        event = fetch_json(event_url, event_cache)

        day_rows = 0
        for cls in event.get("luokat", []):
            if not include_class(cls):
                continue
            round_key, rows = parse_class(date_str, cls)
            all_rows.extend(rows)
            day_rows += len(rows)
            print(f"{date_str} {round_key}: {len(rows)}")

        print(f"{date_str}: kept rows {day_rows}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if str(r["rank"]).isdigit() else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "midsummer_dog_sports_festival_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
