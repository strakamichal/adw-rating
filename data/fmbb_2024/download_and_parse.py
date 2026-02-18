#!/usr/bin/env python3
"""Download and parse FMBB World Championship 2024 combined individual results.

The official PDF contains a combined table with separate Agility and Jumping
columns (Time Ag, Time Ju, Pen ag, Pen ju). We split these into two individual
runs for the rating system.
"""

import csv
import io
from pathlib import Path

import pdfplumber
import requests

BASE_DIR = Path(__file__).parent
PDF_URL = "https://www.agilitynow.eu/wp-content/uploads/Agility-World-Championship-Results.pdf"
COMPETITION_NAME = "FMBB World Championship 2024"

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

COUNTRY_TO_ISO3 = {
    "Austria": "AUT",
    "Belgium": "BEL",
    "Czech Republic": "CZE",
    "Denmark": "DNK",
    "Estonia": "EST",
    "Finland": "FIN",
    "France": "FRA",
    "Germany": "DEU",
    "Greece": "GRC",
    "Hungary": "HUN",
    "Italy": "ITA",
    "Lithuania": "LTU",
    "Netherlands": "NLD",
    "Poland": "POL",
    "Slovakia": "SVK",
    "Spain": "ESP",
    "Sweden": "SWE",
    "Switzerland": "CHE",
    "Ukraine": "UKR",
    "United Kingdom": "GBR",
}

# Penalty threshold for elimination (DSQ entries have pen=100)
ELIM_PENALTY_THRESHOLD = 100.0


def to_float(v: str):
    v = (v or "").strip().replace(",", ".")
    if not v:
        return ""
    try:
        return float(v)
    except Exception:
        return ""


def main():
    pdf_path = BASE_DIR / "fmbb_2024_individual_combined.pdf"
    if pdf_path.exists():
        pdf_bytes = pdf_path.read_bytes()
    else:
        pdf_bytes = requests.get(PDF_URL, timeout=30, headers={"User-Agent": "Mozilla/5.0"}).content
        pdf_path.write_bytes(pdf_bytes)

    # Parse combined table from PDF
    # Columns: Clas, N, Handler, Dog, Breed, Country, Time Ag, Time Ju, Time Tot,
    #          Pen ag, Pen ju, Pen TOT, Qual
    entries = []
    with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf:
        table = (pdf.pages[0].extract_tables() or [None])[0]
        if not table:
            raise SystemExit("No table in FMBB 2024 PDF")

        for r in table:
            if not r or len(r) < 13:
                continue
            if not (r[0] or "").strip().isdigit():
                continue

            start_no = (r[1] or "").strip()
            handler = (r[2] or "").strip()
            dog = (r[3] or "").strip()
            breed = (r[4] or "").strip()
            country = COUNTRY_TO_ISO3.get((r[5] or "").strip(), (r[5] or "").strip())

            time_ag = to_float(r[6] or "")
            time_ju = to_float(r[7] or "")
            pen_ag = to_float(r[9] or "")
            pen_ju = to_float(r[10] or "")

            entries.append({
                "start_no": start_no,
                "handler": handler,
                "dog": dog,
                "breed": breed,
                "country": country,
                "time_ag": time_ag,
                "time_ju": time_ju,
                "pen_ag": pen_ag,
                "pen_ju": pen_ju,
            })

    # Split into two runs: Agility and Jumping
    all_rows = []

    for discipline, time_key, pen_key, round_key in [
        ("Agility", "time_ag", "pen_ag", "ind_agility_large_1"),
        ("Jumping", "time_ju", "pen_ju", "ind_jumping_large_1"),
    ]:
        run_entries = []
        for e in entries:
            pen = e[pen_key]
            time_val = e[time_key]
            eliminated = (pen != "" and pen >= ELIM_PENALTY_THRESHOLD) or time_val == ""
            run_entries.append({
                "entry": e,
                "pen": pen if pen != "" else 999,
                "time": time_val if time_val != "" else 999,
                "eliminated": eliminated,
            })

        # Rank: clean entries sorted by (penalties, time), then eliminated
        clean = [x for x in run_entries if not x["eliminated"]]
        elim = [x for x in run_entries if x["eliminated"]]
        clean.sort(key=lambda x: (x["pen"], x["time"]))

        for rank, x in enumerate(clean, 1):
            e = x["entry"]
            all_rows.append({
                "competition": COMPETITION_NAME,
                "round_key": round_key,
                "size": "Large",
                "discipline": discipline,
                "is_team_round": "False",
                "rank": rank,
                "start_no": e["start_no"],
                "handler": e["handler"],
                "dog": e["dog"],
                "breed": e["breed"],
                "country": e["country"],
                "faults": "",
                "refusals": "",
                "time_faults": "",
                "total_faults": x["pen"] if x["pen"] != 999 else "",
                "time": x["time"] if x["time"] != 999 else "",
                "speed": "",
                "eliminated": "False",
                "judge": "",
                "sct": "",
                "mct": "",
                "course_length": "",
            })

        for x in elim:
            e = x["entry"]
            all_rows.append({
                "competition": COMPETITION_NAME,
                "round_key": round_key,
                "size": "Large",
                "discipline": discipline,
                "is_team_round": "False",
                "rank": "",
                "start_no": e["start_no"],
                "handler": e["handler"],
                "dog": e["dog"],
                "breed": e["breed"],
                "country": e["country"],
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

        print(f"{discipline}: {len(clean)} clean + {len(elim)} eliminated = {len(run_entries)} total")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            r["start_no"],
        )
    )

    out_csv = BASE_DIR / "fmbb_2024_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
