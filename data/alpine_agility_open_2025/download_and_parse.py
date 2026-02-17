#!/usr/bin/env python3
"""Download and parse Alpine Agility Open 2025 results from ENCI."""

import csv
import re
import time
from pathlib import Path

import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
HTML_DIR = BASE_DIR / "html"
HTML_DIR.mkdir(exist_ok=True)

COMPETITION_ID = 4100
COMPETITION_NAME = "Alpine Agility Open 2025"

TRIALS = {
    "agility_qualifying": {"trial_id": 2108, "discipline": "Agility", "seq": 1},
    "jumping_qualifying": {"trial_id": 2109, "discipline": "Jumping", "seq": 1},
    "individual_final": {"trial_id": 2110, "discipline": "Final", "seq": 1},
}

SIZE_IDS = {
    3: "Small",
    4: "Medium",
    6: "Intermediate",
    5: "Large",
}

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

CHIP_COUNTRY_MAP = {
    "380": "ITA", "250": "FRA", "276": "DEU", "705": "SVN", "203": "CZE",
    "756": "CHE", "040": "AUT", "826": "GBR", "967": "GBR", "941": "AUS",
    "528": "NLD", "616": "POL", "246": "FIN", "752": "SWE", "578": "NOR",
    "348": "HUN", "191": "HRV", "703": "SVK", "642": "ROU", "100": "BGR",
    "724": "ESP", "620": "PRT", "056": "BEL", "208": "DNK", "372": "IRL",
    "840": "USA", "124": "CAN", "900": "AUT", "985": "ITA", "981": "ITA",
    "982": "ITA", "688": "SRB",
}


def fetch(url: str) -> str:
    time.sleep(0.4)
    r = requests.get(url, timeout=30)
    r.raise_for_status()
    return r.text


def guess_country_from_chip(chip: str) -> str:
    chip = (chip or "").strip()
    if len(chip) < 3:
        return ""
    prefix = chip[:3]
    if prefix in CHIP_COUNTRY_MAP:
        return CHIP_COUNTRY_MAP[prefix]
    parts = chip.split()
    if len(parts) > 1 and len(parts[-1]) == 3 and parts[-1].isalpha():
        return parts[-1].upper()
    return ""


def parse_page(html: str):
    soup = BeautifulSoup(html, "html.parser")

    metadata = {
        "judge": "",
        "sct": "",
        "mct": "",
        "course_length": "",
    }

    header = soup.find("header", class_="header")
    if header:
        halves = header.find_all("div", class_="col-1-2")
        if halves:
            txt = halves[0].get_text(" ", strip=True)
            m = re.search(r"Lunghezza:\s*([\d.]+)", txt)
            if m:
                metadata["course_length"] = float(m.group(1))
            m = re.search(r"TPS:\s*([\d.]+)", txt)
            if m:
                metadata["sct"] = float(m.group(1))
            m = re.search(r"TPM:\s*([\d.]+)", txt)
            if m:
                metadata["mct"] = float(m.group(1))
        if len(halves) > 1:
            txt = halves[1].get_text(" ", strip=True)
            m = re.search(r"Giudice:\s*(.+)", txt)
            if m:
                metadata["judge"] = m.group(1).strip()

    rows = []
    main = soup.find("main", class_="content")
    if not main:
        return metadata, rows

    for row in main.find_all("div", class_="row"):
        if "header" in (row.get("class") or []):
            continue

        cells = row.find_all("div", class_="cell")
        if len(cells) < 12:
            continue

        rank_str = cells[0].get_text(strip=True)
        start_no = cells[1].get_text(strip=True)
        dog = re.sub(r"\s*\(cp\)\s*$", "", cells[2].get_text(strip=True))
        handler = cells[3].get_text(strip=True)
        breed = cells[4].get_text(strip=True)
        chip = cells[5].get_text(strip=True)
        # cell[6] = club
        time_str = cells[7].get_text(strip=True)
        faults_str = cells[8].get_text(strip=True)
        refusals_str = cells[9].get_text(strip=True)
        total_faults_str = cells[10].get_text(strip=True)
        qualification = cells[11].get_text(strip=True).lower()

        absent = qualification in {"assente", "absent"}
        if absent:
            continue

        eliminated = qualification in {"eliminato", "eliminated"}
        rank = ""
        if not eliminated and rank_str != "-":
            try:
                ir = int(rank_str)
                if ir > 0:
                    rank = ir
            except Exception:
                rank = ""

        faults = ""
        refusals = ""
        total_faults = ""
        tval = ""
        if faults_str != "":
            try:
                faults = int(faults_str)
            except Exception:
                pass
        if refusals_str != "":
            try:
                refusals = int(refusals_str)
            except Exception:
                pass
        if total_faults_str != "":
            try:
                total_faults = float(total_faults_str)
            except Exception:
                pass
        if time_str and time_str != "0":
            try:
                tval = float(time_str)
            except Exception:
                pass

        speed = ""
        clen = metadata.get("course_length", "")
        if tval not in ("", 0) and clen not in ("", 0):
            try:
                speed = round(float(clen) / float(tval), 2)
            except Exception:
                speed = ""

        rows.append({
            "rank": rank,
            "start_no": start_no,
            "handler": handler,
            "dog": dog,
            "breed": breed,
            "country": guess_country_from_chip(chip),
            "faults": faults,
            "refusals": refusals,
            "total_faults": total_faults,
            "time": tval,
            "speed": speed,
            "eliminated": "True" if eliminated or rank == "" else "False",
        })

    return metadata, rows


def main():
    all_rows = []

    for trial_key, trial in TRIALS.items():
        trial_id = trial["trial_id"]
        discipline = trial["discipline"]
        seq = trial["seq"]

        for size_id, size in SIZE_IDS.items():
            url = f"https://sport.enci.it/agility-dog/open/ranking/{COMPETITION_ID}/{trial_id}/all/{size_id}"
            cache = HTML_DIR / f"{trial_key}_{size.lower()}.html"

            if cache.exists():
                html = cache.read_text(encoding="utf-8")
            else:
                html = fetch(url)
                cache.write_text(html, encoding="utf-8")

            metadata, rows = parse_page(html)
            round_key = f"ind_{discipline.lower()}_{size.lower()}_{seq}"
            round_key = re.sub(r"[^a-z0-9_]+", "_", round_key)

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
                    "breed": r["breed"],
                    "country": r["country"],
                    "faults": r["faults"],
                    "refusals": r["refusals"],
                    "time_faults": "",
                    "total_faults": r["total_faults"],
                    "time": r["time"],
                    "speed": r["speed"],
                    "eliminated": r["eliminated"],
                    "judge": metadata.get("judge", ""),
                    "sct": metadata.get("sct", ""),
                    "mct": metadata.get("mct", ""),
                    "course_length": metadata.get("course_length", ""),
                })

            print(f"{trial_key} {size}: {len(rows)}")

    all_rows.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if r["rank"] != "" else 999999,
            str(r["start_no"]),
        )
    )

    out = BASE_DIR / "alpine_agility_open_2025_results.csv"
    with out.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=TARGET_COLUMNS)
        w.writeheader()
        w.writerows(all_rows)

    print(f"Total rows: {len(all_rows)}")
    print(f"CSV: {out}")


if __name__ == "__main__":
    main()
