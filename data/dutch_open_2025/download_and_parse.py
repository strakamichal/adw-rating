#!/usr/bin/env python3
"""Download and parse Dutch Open 2025 results from agilityresults.nl."""

import csv
import re
import time
from collections import defaultdict
from pathlib import Path

import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
HTML_DIR = BASE_DIR / "html"
HTML_DIR.mkdir(exist_ok=True)

COMPETITION_NAME = "Dutch Open 2025"
EVENT_IDS = [204, 205, 206, 207]  # Thu-Sun

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]


def fetch(url: str) -> str:
    time.sleep(0.15)
    r = requests.get(url, timeout=30, headers={"User-Agent": "Mozilla/5.0"})
    r.raise_for_status()
    return r.text


def to_float(v: str):
    v = (v or "").strip().replace(",", ".")
    if not v or v in {"--", "-"}:
        return ""
    try:
        return float(v)
    except Exception:
        return ""


def parse_run_list(event_id: int):
    url = f"https://www.agilityresults.nl/results/spellen/all/{event_id}/0/1"
    html = fetch(url)
    (HTML_DIR / f"event_{event_id}_runs.html").write_text(html, encoding="utf-8")
    soup = BeautifulSoup(html, "html.parser")

    runs = []
    for a in soup.find_all("a", href=True):
        href = a["href"]
        m = re.search(rf"/results/uitslagen/all/{event_id}/(\d+)/1$", href)
        if not m:
            continue
        run_id = int(m.group(1))
        label = " ".join(a.get_text(" ", strip=True).split())
        runs.append((run_id, label, requests.compat.urljoin(url, href)))

    # dedupe by run_id
    uniq = {}
    for run_id, label, href in runs:
        uniq[run_id] = (run_id, label, href)
    return sorted(uniq.values(), key=lambda x: x[0])


def parse_run_page(event_id: int, run_id: int, run_url: str):
    html = fetch(run_url)
    (HTML_DIR / f"event_{event_id}_run_{run_id}.html").write_text(html, encoding="utf-8")
    soup = BeautifulSoup(html, "html.parser")

    table = soup.find("table")
    if not table:
        return None, []

    card = table.find_parent("div", class_="card-body") or table.find_parent("div")
    context = " ".join(card.get_text(" ", strip=True).split()) if card else ""

    # Example: "Agility 1 Small spt: 53.80 lengte:220 Keurder: Marc Valk ..."
    run_desc = ""
    m = re.search(r"^(.*?)\s+spt:\s*([\d.,]+)", context)
    sct = ""
    if m:
        run_desc = m.group(1).strip()
        sct = to_float(m.group(2))
    else:
        m2 = re.search(r"^(.*?)\s+plt\s+strt\s+handlers", context, re.IGNORECASE)
        if m2:
            run_desc = m2.group(1).strip()

    course_length = ""
    m = re.search(r"lengte:\s*([\d.,]+)", context, re.IGNORECASE)
    if m:
        course_length = to_float(m.group(1))

    judge = ""
    m = re.search(r"Keurder:\s*([^\n]+?)\s+plt\s+strt", context, re.IGNORECASE)
    if m:
        judge = m.group(1).strip()
    else:
        m = re.search(r"Keurder:\s*([^\n]+)", context, re.IGNORECASE)
        if m:
            judge = m.group(1).strip()

    # mpt is often in embedded state but not consistently visible
    mct = ""

    # infer discipline + size from run description
    low = run_desc.lower()
    if "jump" in low:
        discipline = "Jumping"
    elif "agility" in low or "a-lauf" in low:
        discipline = "Agility"
    elif "final" in low:
        discipline = "Final"
    else:
        discipline = "Agility"

    size = ""
    for token, mapped in [
        ("small", "Small"),
        ("medium", "Medium"),
        ("inter", "Intermediate"),
        ("large", "Large"),
    ]:
        if token in low:
            size = mapped
            break

    if size == "":
        return None, []

    rows = []
    running_rank = 0

    for tr in table.find_all("tr")[1:]:
        tds = tr.find_all("td")
        if len(tds) < 8:
            continue

        rank_raw = tds[0].get_text(" ", strip=True)
        start_no = tds[1].get_text(" ", strip=True)
        handler_dog = tds[2].get_text(" ", strip=True)
        waf = tds[3].get_text(" ", strip=True).upper()
        faults_total_raw = tds[4].get_text(" ", strip=True)
        time_raw = tds[5].get_text(" ", strip=True)
        speed_raw = tds[6].get_text(" ", strip=True)
        remark = tds[7].get_text(" ", strip=True)

        if " - " in handler_dog:
            handler, dog = handler_dog.split(" - ", 1)
        else:
            handler, dog = handler_dog, ""

        eliminated = False
        if waf in {"D", "DIS", "DSQ", "NAW"}:
            eliminated = True
        if rank_raw == "0":
            eliminated = True

        rank = ""
        if not eliminated:
            if rank_raw.isdigit() and int(rank_raw) > 0:
                rank = int(rank_raw)
                running_rank = rank
            else:
                running_rank += 1
                rank = running_rank

        # Parse faults/refusals from WAF pattern X-Y-Z
        faults = ""
        refusals = ""
        time_faults = ""
        m = re.match(r"^(\d+)-(\d+)-(\d+)$", waf)
        if m:
            faults = int(m.group(1))
            refusals = int(m.group(2))

        total_faults = to_float(faults_total_raw)
        if total_faults == "" and not eliminated:
            total_faults = 0.0

        time_val = to_float(time_raw)
        speed = to_float(speed_raw)

        country = remark if re.fullmatch(r"[A-Z]{3}", remark or "") else ""

        rows.append({
            "size": size,
            "discipline": discipline,
            "rank": rank,
            "start_no": start_no,
            "handler": handler.strip(),
            "dog": dog.strip(),
            "country": country,
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

    return (discipline, size), rows


def main():
    all_rows = []
    counters = defaultdict(int)

    for event_id in EVENT_IDS:
        runs = parse_run_list(event_id)
        print(f"event {event_id}: {len(runs)} runs")

        for run_id, label, run_url in runs:
            key, rows = parse_run_page(event_id, run_id, run_url)
            if not key:
                continue
            discipline, size = key

            counters[(discipline, size)] += 1
            seq = counters[(discipline, size)]
            round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"
            round_key = re.sub(r"[^a-z0-9_]+", "_", round_key)

            for r in rows:
                all_rows.append({
                    "competition": COMPETITION_NAME,
                    "round_key": round_key,
                    "size": r["size"],
                    "discipline": r["discipline"],
                    "is_team_round": "False",
                    "rank": r["rank"],
                    "start_no": r["start_no"],
                    "handler": r["handler"],
                    "dog": r["dog"],
                    "breed": "",
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
                })

            print(f"  run {run_id} {label}: {len(rows)}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            str(r["start_no"]),
        )
    )

    out_csv = BASE_DIR / "dutch_open_2025_results.csv"
    with out_csv.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out_csv}")


if __name__ == "__main__":
    main()
