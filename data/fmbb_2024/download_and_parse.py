#!/usr/bin/env python3
"""Download and parse FMBB World Championship 2024 combined individual results."""

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


def to_float(v: str):
    v = (v or "").strip().replace(",", ".")
    if not v:
        return ""
    try:
        return float(v)
    except Exception:
        return ""


def main():
    pdf_bytes = requests.get(PDF_URL, timeout=30, headers={"User-Agent": "Mozilla/5.0"}).content
    (BASE_DIR / "fmbb_2024_individual_combined.pdf").write_bytes(pdf_bytes)

    rows = []
    with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf:
        table = (pdf.pages[0].extract_tables() or [None])[0]
        if not table:
            raise SystemExit("No table in FMBB 2024 PDF")

        for r in table:
            if not r or len(r) < 13:
                continue
            if not (r[0] or "").strip().isdigit():
                continue

            rank = int((r[0] or "").strip())
            start_no = (r[1] or "").strip()
            handler = (r[2] or "").strip()
            dog = (r[3] or "").strip()
            breed = (r[4] or "").strip()
            country = COUNTRY_TO_ISO3.get((r[5] or "").strip(), (r[5] or "").strip())

            # Combined final: use total values from PDF
            time_total = to_float(r[8] or "")
            penalties_total = to_float(r[11] or "")

            rows.append({
                "competition": COMPETITION_NAME,
                "round_key": "ind_final_large_1",
                "size": "Large",
                "discipline": "Final",
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
                "total_faults": penalties_total,
                "time": time_total,
                "speed": "",
                "eliminated": "False",
                "judge": "",
                "sct": "",
                "mct": "",
                "course_length": "",
            })

    rows.sort(key=lambda r: int(r["rank"]))

    out_csv = BASE_DIR / "fmbb_2024_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(rows)

    print(f"Rows: {len(rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
