#!/usr/bin/env python3
"""Download and parse Austrian Agility Open 2025 results from dognow.at PDFs."""

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

COMPETITION_NAME = "Austrian Agility Open 2025"

EVENT_IDS = [2903, 2905, 2904]  # Friday, Saturday, Sunday

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]


def to_float_de(v: str):
    v = (v or "").strip().replace(" ", "")
    if not v or v in {"--", "-"}:
        return ""
    v = v.replace(",", ".")
    try:
        return float(v)
    except Exception:
        return ""


def to_int(v: str):
    v = (v or "").strip()
    if not v:
        return ""
    try:
        return int(v)
    except Exception:
        return ""


def fetch(url: str) -> str:
    time.sleep(0.2)
    r = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    return r.text


def fetch_bytes(url: str) -> bytes:
    time.sleep(0.2)
    r = requests.get(url, timeout=40, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    return r.content


def parse_run_list(event_id: int):
    html = fetch(f"https://www.dognow.at/ergebnisse/src/data.php?event={event_id}&lauf=0")
    soup = BeautifulSoup(html, "html.parser")
    runs = []
    for tr in soup.find_all("tr", id=re.compile(r"^showrun_\d+$")):
        run_id = int(tr["id"].split("_")[1])
        tds = tr.find_all("td")
        if not tds:
            continue
        run_name = " ".join(tds[0].get_text(" ", strip=True).split())
        if len(tds) >= 3:
            size_hint = " ".join(tds[2].get_text(" ", strip=True).split()).upper()
            if size_hint in {"S", "M", "I", "L"} and not run_name.upper().endswith(f" {size_hint}"):
                run_name = f"{run_name} {size_hint}"
        runs.append((run_id, run_name))
    return runs


def parse_pdf(pdf_bytes: bytes):
    metadata = {
        "judge": "",
        "sct": "",
        "mct": "",
        "course_length": "",
        "date": "",
    }
    rows = []

    with pdfplumber.open(io.BytesIO(pdf_bytes)) as pdf:
        if not pdf.pages:
            return metadata, rows

        first_text = pdf.pages[0].extract_text() or ""
        lines = [ln.strip() for ln in first_text.splitlines() if ln.strip()]
        title = lines[0] if lines else ""

        m = re.search(r"\((\d{2}\.\d{2}\.\d{4})\)", title)
        if m:
            metadata["date"] = m.group(1)

        m = re.search(r"Richter:\s*(.+)", first_text)
        if m:
            metadata["judge"] = m.group(1).strip().splitlines()[0].strip()

        m = re.search(r"Länge:\s*([\d.,]+)", first_text)
        if m:
            metadata["course_length"] = to_float_de(m.group(1))

        m = re.search(r"Normzeit:\s*([\d.,]+)", first_text)
        if m:
            metadata["sct"] = to_float_de(m.group(1))

        m = re.search(r"Maxzeit:\s*([\d.,]+)", first_text)
        if m:
            metadata["mct"] = to_float_de(m.group(1))

        for page in pdf.pages:
            for table in page.extract_tables() or []:
                for row in table:
                    if not row or len(row) < 11:
                        continue
                    if (row[0] or "").strip().lower() == "rang":
                        continue
                    rank_raw = (row[0] or "").strip()
                    start_no = (row[1] or "").strip()
                    handler = (row[2] or "").strip()
                    dog = (row[3] or "").strip()
                    faults = to_int(row[5] or "")
                    refusals = to_int(row[6] or "")
                    time_faults = to_float_de(row[7] or "")
                    time_val = to_float_de(row[8] or "")
                    total_faults = to_float_de(row[9] or "")
                    speed = to_float_de(row[10] or "")

                    rank = ""
                    if rank_raw.isdigit():
                        rank = int(rank_raw)

                    eliminated = rank == ""

                    rows.append({
                        "rank": rank,
                        "start_no": start_no,
                        "handler": handler,
                        "dog": dog,
                        "faults": faults,
                        "refusals": refusals,
                        "time_faults": time_faults,
                        "time": time_val,
                        "total_faults": total_faults,
                        "speed": speed,
                        "eliminated": "True" if eliminated else "False",
                    })

    # dedupe rows by rank/start_no/handler/dog to avoid page overlap duplicates
    dedup = {}
    for r in rows:
        key = (r["rank"], r["start_no"], r["handler"], r["dog"])
        dedup[key] = r
    return metadata, list(dedup.values())


def classify_run(run_name: str):
    name = run_name.strip()
    low = name.lower()

    if "jump" in low:
        discipline = "Jumping"
    elif "a-lauf" in low or "agility" in low:
        discipline = "Agility"
    elif "final" in low:
        discipline = "Final"
    else:
        discipline = "Agility"

    size = ""
    matches = re.findall(r"\b([SMIL])\b", name.upper())
    if matches:
        size = {
            "S": "Small",
            "M": "Medium",
            "I": "Intermediate",
            "L": "Large",
        }[matches[-1]]

    return discipline, size


def main():
    all_rows = []
    counters = defaultdict(int)

    for event_id in EVENT_IDS:
        runs = parse_run_list(event_id)
        print(f"event {event_id}: {len(runs)} runs")

        for run_id, run_name in runs:
            discipline, size = classify_run(run_name)
            if size == "":
                continue

            counters[(discipline, size)] += 1
            seq = counters[(discipline, size)]
            round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"
            round_key = re.sub(r"[^a-z0-9_]+", "_", round_key)

            pdf_url = f"https://www.dognow.at/ergebnisse/pdf.php?lauf={run_id}&event={event_id}"
            pdf_path = PDF_DIR / f"event_{event_id}_run_{run_id}.pdf"
            if pdf_path.exists():
                pdf_bytes = pdf_path.read_bytes()
            else:
                pdf_bytes = fetch_bytes(pdf_url)
                pdf_path.write_bytes(pdf_bytes)

            metadata, rows = parse_pdf(pdf_bytes)
            print(f"  run {run_id} {run_name}: {len(rows)}")

            for r in rows:
                all_rows.append({
                    "competition": COMPETITION_NAME,
                    "round_key": round_key,
                    "size": size,
                    "discipline": discipline,
                    "is_team_round": "False",
                    "rank": r["rank"],
                    "start_no": r["start_no"],
                    "handler": r["handler"],
                    "dog": r["dog"],
                    "breed": "",
                    "country": "",
                    "faults": r["faults"],
                    "refusals": r["refusals"],
                    "time_faults": r["time_faults"],
                    "total_faults": r["total_faults"],
                    "time": r["time"],
                    "speed": r["speed"],
                    "eliminated": r["eliminated"],
                    "judge": metadata["judge"],
                    "sct": metadata["sct"],
                    "mct": metadata["mct"],
                    "course_length": metadata["course_length"],
                })

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "austrian_agility_open_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
