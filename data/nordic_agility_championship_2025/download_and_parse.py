#!/usr/bin/env python3
"""Download and parse Nordic Agility Championship 2025 from official PDF results."""

import csv
import io
import re
import time
from collections import defaultdict
from pathlib import Path

import pdfplumber
import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
PDF_DIR = BASE_DIR / "pdf"
PDF_DIR.mkdir(exist_ok=True)

COMPETITION_NAME = "Nordic Agility Championship 2025"
RESULTS_PAGE = "https://tonsberghundeklubb.no/results/"

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

COUNTRY_CODE_TO_ISO3 = {
    "DNK": "DNK",
    "DEN": "DNK",
    "FIN": "FIN",
    "NOR": "NOR",
    "SWE": "SWE",
    "ISL": "ISL",
}

COUNTRY_ISO2_TO_ISO3 = {
    "DK": "DNK",
    "FI": "FIN",
    "NO": "NOR",
    "SE": "SWE",
    "IS": "ISL",
}


def fetch(url: str) -> str:
    time.sleep(0.15)
    r = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    return r.text


def fetch_bytes(url: str) -> bytes:
    time.sleep(0.15)
    r = requests.get(url, timeout=40, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    return r.content


def to_float(v: str):
    v = (v or "").strip().replace(",", ".")
    if not v:
        return ""
    try:
        return float(v)
    except Exception:
        return ""


def detect_size_from_name(name: str) -> str:
    low = name.lower()
    if "intermedium" in low or "intermediate" in low:
        return "Intermediate"
    if "small" in low:
        return "Small"
    if "medium" in low:
        return "Medium"
    if "large" in low:
        return "Large"
    return ""


def normalize_country(raw: str) -> str:
    txt = (raw or "").strip()
    if not txt:
        return ""

    code = re.sub(r"[^A-Za-z]", "", txt).upper()
    if len(code) == 2:
        return COUNTRY_ISO2_TO_ISO3.get(code, "")
    if len(code) == 3:
        return COUNTRY_CODE_TO_ISO3.get(code, "")
    return ""


def collect_pdf_links():
    html = fetch(RESULTS_PAGE)
    soup = BeautifulSoup(html, "html.parser")

    links = []
    for a in soup.find_all("a", href=True):
        href = requests.compat.urljoin(RESULTS_PAGE, a["href"])
        low = href.lower()
        if "nordisk-2025" not in low:
            continue
        if not low.endswith(".pdf"):
            continue
        # skip team relay merged PDF (no individual handlers)
        if "merged" in low:
            continue
        if "resultat" not in low:
            continue
        links.append(href)

    # dedupe and sort
    links = sorted(set(links))
    return links


def parse_pdf(pdf_name: str, pdf_bytes: bytes):
    with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf:
        if not pdf.pages:
            return None, []

        first_text = pdf.pages[0].extract_text() or ""

        # metadata
        discipline = "Agility"
        ft = first_text.lower()
        if "jumping" in ft:
            discipline = "Jumping"
        if "final" in ft:
            discipline = "Final"

        size = detect_size_from_name(pdf_name)
        if not size:
            # fallback from title text
            size = detect_size_from_name(first_text)

        m = re.search(r"Judge:\s*(.+?)(?:\s+Length:|$)", first_text, re.IGNORECASE)
        judge = m.group(1).strip() if m else ""

        m = re.search(r"Length:\s*([\d.,]+)", first_text, re.IGNORECASE)
        course_length = to_float(m.group(1)) if m else ""

        m = re.search(r"SCT:\s*([\d.,]+)", first_text, re.IGNORECASE)
        sct = to_float(m.group(1)) if m else ""

        mct = ""

        rows = []
        for page in pdf.pages:
            for table in page.extract_tables() or []:
                for row in table:
                    if not row or len(row) < 8:
                        continue
                    c0 = (row[0] or "").strip()
                    if c0.lower().startswith("rank"):
                        continue

                    # Check if this is an eliminated row (no rank, "Elim" in row)
                    row_text = " ".join((c or "") for c in row).lower()
                    is_elim = "elim" in row_text

                    if not c0.isdigit() and not is_elim:
                        continue
                    if not c0 and not is_elim:
                        continue

                    rank = int(c0) if c0.isdigit() else ""
                    handler = (row[1] or "").strip()
                    dog_short = (row[2] or "").strip()
                    dog_registered = (row[3] or "").strip()
                    breed = (row[4] or "").strip()
                    country_raw = (row[5] or "").strip()
                    country = normalize_country(country_raw)

                    # Some rows have breed split across columns and missing country.
                    if not country and country_raw:
                        breed = f"{breed} {country_raw}".strip()

                    if is_elim:
                        faults = ""
                        time_val = ""
                    else:
                        faults = to_float(row[6] or "")
                        time_val = to_float(row[7] or "")

                    dog = dog_registered or dog_short
                    if not handler:
                        continue

                    speed = ""
                    if course_length not in ("", 0) and time_val not in ("", 0):
                        try:
                            speed = round(float(course_length) / float(time_val), 2)
                        except Exception:
                            speed = ""

                    rows.append({
                        "size": size,
                        "discipline": discipline,
                        "rank": rank,
                        "start_no": "",
                        "handler": handler,
                        "dog": dog,
                        "breed": breed,
                        "country": country,
                        "faults": "",
                        "refusals": "",
                        "time_faults": "",
                        "total_faults": faults,
                        "time": time_val,
                        "speed": speed,
                        "eliminated": str(is_elim),
                        "judge": judge,
                        "sct": sct,
                        "mct": mct,
                        "course_length": course_length,
                    })

    # dedupe duplicates across pages
    dedup = {}
    for r in rows:
        key = (r["rank"], r["handler"], r["dog"], r["eliminated"])
        dedup[key] = r

    meta = {
        "size": size,
        "discipline": discipline,
    }
    return meta, list(dedup.values())


def main():
    links = collect_pdf_links()
    print(f"PDF links: {len(links)}")

    counters = defaultdict(int)
    all_rows = []

    for url in links:
        name = url.split("/")[-1]
        pdf_path = PDF_DIR / name
        if pdf_path.exists():
            pdf_bytes = pdf_path.read_bytes()
        else:
            pdf_bytes = fetch_bytes(url)
            pdf_path.write_bytes(pdf_bytes)

        meta, rows = parse_pdf(name, pdf_bytes)
        if not meta or not rows:
            continue

        discipline = meta["discipline"]
        size = meta["size"]
        if size == "":
            continue

        counters[(discipline, size)] += 1
        seq = counters[(discipline, size)]
        round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"
        round_key = re.sub(r"[^a-z0-9_]+", "_", round_key)

        for r in rows:
            row = {
                "competition": COMPETITION_NAME,
                "round_key": round_key,
                "size": r["size"],
                "discipline": r["discipline"],
                "is_team_round": "False",
                "rank": r["rank"],
                "start_no": r["start_no"],
                "handler": r["handler"],
                "dog": r["dog"],
                "breed": r["breed"],
                "country": r["country"],
                "faults": r["faults"],
                "refusals": r["refusals"],
                "time_faults": r["time_faults"],
                "total_faults": r["total_faults"],
                "time": r["time"],
                "speed": r["speed"],
                "eliminated": r["eliminated"],
                "judge": r["judge"],
                "sct": r["sct"],
                "mct": r["mct"],
                "course_length": r["course_length"],
            }
            all_rows.append(row)

        print(f"{name}: {len(rows)} rows -> {round_key}")

    all_rows.sort(
        key=lambda r: (r["round_key"], int(r["rank"]) if r["rank"] != "" else 999999, r["handler"])
    )

    out_csv = BASE_DIR / "nordic_agility_championship_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS, lineterminator="\n")
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
