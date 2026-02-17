#!/usr/bin/env python3
"""Download and parse Helvetic Agility Masters 2025 results from PDFs."""

import csv
import re
from pathlib import Path

import pdfplumber
import requests

BASE_DIR = Path(__file__).parent
PDF_DIR = BASE_DIR / "pdf"
PDF_DIR.mkdir(exist_ok=True)

COMPETITION_NAME = "Helvetic Agility Masters 2025"

PDFS = {
    "masters_final_intermediate_large": {
        "url": "https://helveticagilitymasters.ch/wp-content/uploads/HAM25_Rangliste_Helvetic_Agility_Masters_Final_Intermediate_Large.pdf",
        "size": "",
        "discipline": "Final",
    },
    "masters_final_small_medium": {
        "url": "https://helveticagilitymasters.ch/wp-content/uploads/HAM25_Rangliste_Helvetic_Agility_Masters_Final_Small_Medium.pdf",
        "size": "",
        "discipline": "Final",
    },
    "novice_final_intermediate_large": {
        "url": "https://helveticagilitymasters.ch/wp-content/uploads/HAM25_Rangliste_Helvetic_Novice_Cup_Final_Intermediate_Large.pdf",
        "size": "",
        "discipline": "Final",
    },
    "novice_final_small_medium": {
        "url": "https://helveticagilitymasters.ch/wp-content/uploads/HAM25_Rangliste_Helvetic_Novice_Cup_Final_Small_Medium.pdf",
        "size": "",
        "discipline": "Final",
    },
}

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

COUNTRY_MAP = {
    "AUS": "AUS",
    "BEL": "BEL",
    "CZE": "CZE",
    "ESP": "ESP",
    "FRA": "FRA",
    "GER": "DEU",
    "HUN": "HUN",
    "ITA": "ITA",
    "LUX": "LUX",
    "NOR": "NOR",
    "POL": "POL",
    "SLO": "SVN",
    "UKR": "UKR",
    "USA": "USA",
}


def to_float(v: str):
    txt = (v or "").strip().replace(",", ".")
    if txt == "":
        return ""
    try:
        return float(txt)
    except Exception:
        return ""


def to_int(v: str):
    txt = (v or "").strip()
    if txt == "":
        return ""
    if txt.isdigit():
        return int(txt)
    try:
        return int(float(txt.replace(",", ".")))
    except Exception:
        return ""


def country_from_lic(lic: str) -> str:
    lic = (lic or "").strip().upper()
    m = re.match(r"^([A-Z]{3})", lic)
    if m:
        return COUNTRY_MAP.get(m.group(1), "")
    if lic.isdigit():
        return "CHE"
    return ""


def infer_size_from_slug(round_slug: str) -> str:
    """Map combined categories to a canonical size bucket used by the project."""
    if "intermediate_large" in round_slug:
        return "Large"
    if "small_medium" in round_slug:
        return "Medium"
    return ""


def parse_metadata(first_page_text: str):
    txt = first_page_text or ""

    judge = ""
    sct = ""
    mct = ""
    course_length = ""

    m = re.search(r"Richter:\s*(.+)", txt)
    if m:
        judge = m.group(1).strip()

    m = re.search(r"Länge:\s*([\d.]+)", txt)
    if m:
        course_length = to_float(m.group(1))

    m = re.search(r"Standardzeit:\s*([\d.]+)", txt)
    if m:
        sct = to_float(m.group(1))

    m = re.search(r"Maximalzeit:\s*([\d.]+)", txt)
    if m:
        mct = to_float(m.group(1))

    return judge, sct, mct, course_length


def group_row_words(words):
    grouped = []
    current = []
    current_top = None

    for w in sorted(words, key=lambda x: (x["top"], x["x0"])):
        top = float(w["top"])
        if current_top is None or abs(top - current_top) <= 1.8:
            current.append(w)
            if current_top is None:
                current_top = top
            else:
                current_top = (current_top + top) / 2
        else:
            grouped.append(current)
            current = [w]
            current_top = top

    if current:
        grouped.append(current)

    return grouped


def parse_page_rows(page):
    words = page.extract_words(use_text_flow=True, keep_blank_chars=False)
    rows = []

    for row_words in group_row_words(words):
        cols = {
            "rank": [], "start_no": [], "lic": [], "handler": [], "dog": [], "breed": [], "club": [],
            "class": [], "time": [], "speed": [], "faults": [], "refusals": [], "time_fault": [], "total": [],
        }

        for w in sorted(row_words, key=lambda x: x["x0"]):
            x0 = float(w["x0"])
            text = w["text"]
            if x0 < 45:
                cols["rank"].append(text)
            elif x0 < 63:
                cols["start_no"].append(text)
            elif x0 < 90:
                cols["lic"].append(text)
            elif x0 < 190:
                cols["handler"].append(text)
            elif x0 < 245:
                cols["dog"].append(text)
            elif x0 < 312:
                cols["breed"].append(text)
            elif x0 < 392:
                cols["club"].append(text)
            elif x0 < 410:
                cols["class"].append(text)
            elif x0 < 445:
                cols["time"].append(text)
            elif x0 < 470:
                cols["speed"].append(text)
            elif x0 < 495:
                cols["faults"].append(text)
            elif x0 < 515:
                cols["refusals"].append(text)
            elif x0 < 545:
                cols["time_fault"].append(text)
            else:
                cols["total"].append(text)

        rank_token = " ".join(cols["rank"]).strip().lower()
        if not re.match(r"^(\d+|dis)$", rank_token):
            continue

        rows.append({k: " ".join(v).strip() for k, v in cols.items()})

    return rows


def parse_pdf(round_slug: str, pdf_path: Path, size: str, discipline: str):
    with pdfplumber.open(pdf_path) as pdf:
        first_text = pdf.pages[0].extract_text() or ""
        judge, sct, mct, course_length = parse_metadata(first_text)

        round_key = f"ind_{round_slug}"
        size = size or infer_size_from_slug(round_slug)
        results = []

        for page in pdf.pages:
            for row in parse_page_rows(page):
                rank_token = row["rank"].lower()
                eliminated = rank_token == "dis"
                rank = "" if eliminated else to_int(row["rank"])

                results.append({
                    "competition": COMPETITION_NAME,
                    "round_key": round_key,
                    "size": size,
                    "discipline": discipline,
                    "is_team_round": "False",
                    "rank": rank,
                    "start_no": row["start_no"],
                    "handler": row["handler"],
                    "dog": row["dog"],
                    "breed": row["breed"],
                    "country": country_from_lic(row["lic"]),
                    "faults": to_float(row["faults"]),
                    "refusals": to_float(row["refusals"]),
                    "time_faults": to_float(row["time_fault"]),
                    "total_faults": to_float(row["total"]),
                    "time": to_float(row["time"]),
                    "speed": to_float(row["speed"]),
                    "eliminated": "True" if eliminated else "False",
                    "judge": judge,
                    "sct": sct,
                    "mct": mct,
                    "course_length": course_length,
                })

        return results


def download_pdf(url: str, target: Path):
    if target.exists():
        return
    r = requests.get(url, timeout=60, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    target.write_bytes(r.content)


def main():
    all_rows = []

    for round_slug, cfg in PDFS.items():
        pdf_path = PDF_DIR / f"{round_slug}.pdf"
        download_pdf(cfg["url"], pdf_path)
        rows = parse_pdf(round_slug, pdf_path, cfg["size"], cfg["discipline"])
        all_rows.extend(rows)
        print(f"{round_slug}: {len(rows)}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if str(r["rank"]).isdigit() else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "helvetic_agility_masters_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS, lineterminator="\n")
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
