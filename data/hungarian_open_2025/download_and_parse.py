#!/usr/bin/env python3
"""Download and parse Hungarian Open 2025 from dogresult.com print result lists."""

import csv
import re
from collections import defaultdict
from pathlib import Path

import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
HTML_PATH = BASE_DIR / "print_result_lists_435.html"

EVENT_ID = 435
COMPETITION_NAME = "Hungarian Open 2025"

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

SIZE_MAP = {
    "S": "Small",
    "M": "Medium",
    "I": "Intermediate",
    "L": "Large",
}


def to_float(v: str):
    v = (v or "").strip().replace(",", ".")
    if not v:
        return ""
    try:
        return float(v)
    except Exception:
        return ""


def parse_title(title: str):
    t = " ".join((title or "").split())
    low = t.lower()

    if "jumping" in low:
        discipline = "Jumping"
    elif "agility" in low:
        discipline = "Agility"
    elif "finál" in low or "final" in low:
        discipline = "Final"
    else:
        discipline = "Agility"

    size = ""
    m = re.search(r"\b([SMIL])\s+[SMIL]\s+O$", t)
    if m:
        size = SIZE_MAP.get(m.group(1), "")

    return discipline, size


def parse_meta(meta_text: str):
    txt = " ".join((meta_text or "").split())
    m = re.search(r"Judge\s*:\s*(.*?)\s+Max time\s*:", txt)
    judge = m.group(1).strip() if m else ""
    m = re.search(r"Course length\s*:\s*([\d.,]+)", txt)
    course_length = to_float(m.group(1)) if m else ""
    m = re.search(r"Standard time\s*:\s*([\d.,]+)", txt)
    sct = to_float(m.group(1)) if m else ""
    m = re.search(r"Max time\s*:\s*([\d.,]+)", txt)
    mct = to_float(m.group(1)) if m else ""
    return judge, sct, mct, course_length


def main():
    url = f"https://dogresult.com/en/event/printResultLists/{EVENT_ID}"
    html = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"}).text
    HTML_PATH.write_text(html, encoding="utf-8")
    soup = BeautifulSoup(html, "html.parser")

    counters = defaultdict(int)
    rows = []

    for table in soup.find_all("table"):
        title_tag = table.find_previous("h4")
        title = " ".join(title_tag.get_text(" ", strip=True).split()) if title_tag else ""
        discipline, size = parse_title(title)
        if size == "":
            continue

        meta_div = None
        sib = table
        for _ in range(10):
            sib = sib.find_previous_sibling()
            if not sib:
                break
            st = " ".join(sib.get_text(" ", strip=True).split())
            if "Judge :" in st and "Course length :" in st:
                meta_div = sib
                break
        judge, sct, mct, course_length = parse_meta(meta_div.get_text(" ", strip=True) if meta_div else "")

        counters[(discipline, size)] += 1
        seq = counters[(discipline, size)]
        round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"

        for tr in table.find_all("tr"):
            cols = [c.get_text(" ", strip=True) for c in tr.find_all(["th", "td"])]
            if len(cols) < 11:
                continue

            rank_token = cols[0].strip()
            if rank_token == "":
                continue

            eliminated = rank_token.upper() in {"DIS", "EL", "E"} or cols[9].strip().lower().startswith("dis")
            rank = int(rank_token) if rank_token.isdigit() else ""
            if eliminated:
                rank = ""

            start_no = cols[1].strip()
            handler = cols[2].strip()
            dog = cols[3].strip()
            time_val = to_float(cols[4])
            faults = to_float(cols[5])
            refusals = to_float(cols[6])
            time_faults = to_float(cols[7])
            total_faults = to_float(cols[8])
            speed = to_float(cols[10])

            rows.append({
                "competition": COMPETITION_NAME,
                "round_key": round_key,
                "size": size,
                "discipline": discipline,
                "is_team_round": "False",
                "rank": rank,
                "start_no": start_no,
                "handler": handler,
                "dog": dog,
                "breed": "",
                "country": "",
                "faults": faults,
                "refusals": refusals,
                "time_faults": time_faults,
                "total_faults": total_faults,
                "time": time_val,
                "speed": speed,
                "eliminated": "True" if eliminated else "False",
                "judge": judge,
                "sct": sct,
                "mct": mct,
                "course_length": course_length,
            })

    rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "hungarian_open_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(rows)

    print(f"Rows: {len(rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
