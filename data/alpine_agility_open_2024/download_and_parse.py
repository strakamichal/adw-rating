#!/usr/bin/env python3
"""
Download and parse Alpine Agility Open 2024 results from ENCI sport into CSV.
No authentication required — ENCI pages are public.

Usage:
  python3 download_and_parse.py
"""

import csv
import re
import time
from pathlib import Path

import requests
from bs4 import BeautifulSoup

BASE_DIR = Path(__file__).parent
HTML_DIR = BASE_DIR / "html"
HTML_DIR.mkdir(exist_ok=True)

COMPETITION_ID = 4100  # ENCI competition ID

# Trials for Alpine Agility Open 2024
# 2046 = Jun 8 junior/open final (tiny fields, skip for rating)
# 2047 = Jun 8 Jumping qualifying
# 2048 = Jun 7 Agility qualifying
# 2049 = Jun 9 Individual final + Team final
TRIALS = {
    "agility_qualifying": {"trial_id": 2048, "discipline": "Agility", "seq": 1},
    "jumping_qualifying": {"trial_id": 2047, "discipline": "Jumping", "seq": 1},
    "individual_final": {"trial_id": 2049, "discipline": "Final", "seq": 1},
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

# Microchip prefix (first 3 digits) to country code
# Based on ICAR manufacturer codes / ISO 11784
CHIP_COUNTRY_MAP = {
    "380": "ITA",  # Italy
    "250": "FRA",  # France
    "276": "DEU",  # Germany
    "705": "SVN",  # Slovenia
    "203": "CZE",  # Czech Republic
    "756": "CHE",  # Switzerland
    "040": "AUT",  # Austria
    "826": "GBR",  # United Kingdom
    "967": "GBR",  # UK (alternate manufacturer)
    "941": "AUS",  # Australia (or other)
    "528": "NLD",  # Netherlands
    "616": "POL",  # Poland
    "246": "FIN",  # Finland
    "752": "SWE",  # Sweden
    "578": "NOR",  # Norway
    "348": "HUN",  # Hungary
    "191": "HRV",  # Croatia
    "703": "SVK",  # Slovakia
    "642": "ROU",  # Romania
    "100": "BGR",  # Bulgaria
    "724": "ESP",  # Spain
    "620": "PRT",  # Portugal
    "056": "BEL",  # Belgium
    "208": "DNK",  # Denmark
    "372": "IRL",  # Ireland
    "840": "USA",  # United States
    "124": "CAN",  # Canada
    "900": "AUT",  # Austria (alternate)
    "985": "ITA",  # Italy (alternate manufacturer)
    "981": "ITA",  # Italy (alternate)
    "982": "ITA",  # Possibly Italy
    "688": "SRB",  # Serbia
}


def fetch_url(url):
    """Fetch URL with caching."""
    print(f"  GET {url}")
    time.sleep(0.5)
    resp = requests.get(url, timeout=30)
    resp.raise_for_status()
    return resp.text


def guess_country_from_chip(chip_str):
    """Guess country from microchip number prefix."""
    chip = chip_str.strip()
    if not chip or len(chip) < 3:
        return ""

    prefix = chip[:3]
    country = CHIP_COUNTRY_MAP.get(prefix, "")

    # Handle some common patterns
    if not country and chip.endswith(" SVN"):
        return "SVN"
    if not country and chip.endswith(" CZE"):
        return "CZE"

    # Check for country suffix (e.g., "705091000014601 SVN")
    parts = chip.split()
    if len(parts) > 1 and len(parts[-1]) == 3:
        return parts[-1].upper()

    return country


def parse_enci_page(html):
    """Parse ENCI sport results page.

    Returns (metadata, results) where metadata is a dict with course info
    and results is a list of dicts.
    """
    soup = BeautifulSoup(html, "html.parser")

    metadata = {"judge": "", "sct": "", "mct": "", "course_length": ""}

    # Header info
    header = soup.find("header", class_="header")
    if header:
        h1 = header.find("h1")
        if h1:
            metadata["title"] = h1.get_text(strip=True)

        # Course info from "Risultati: N Lunghezza: N ..."
        info_div = header.find("div", class_="col-1-2")
        if info_div:
            text = info_div.get_text()
            for pattern, key in [
                (r"Risultati:\s*(\d+)", "total_entries"),
                (r"Lunghezza:\s*([\d.]+)", "course_length"),
                (r"Velocit[àa]:\s*([\d.]+)", "required_speed"),
                (r"TPS:\s*([\d.]+)", "sct"),
                (r"TPM:\s*([\d.]+)", "mct"),
                (r"Ostacoli:\s*(\d+)", "obstacles"),
            ]:
                m = re.search(pattern, text)
                if m:
                    metadata[key] = m.group(1)

        # Judge
        judge_div = header.find_all("div", class_="col-1-2")
        if len(judge_div) > 1:
            judge_text = judge_div[1].get_text(strip=True)
            m = re.search(r"Giudice:\s*(.+)", judge_text)
            if m:
                metadata["judge"] = m.group(1).strip()

    # Results
    results = []
    main = soup.find("main", class_="content")
    if not main:
        return metadata, results

    for row in main.find_all("div", class_="row"):
        if "header" in row.get("class", []):
            continue

        cells = row.find_all("div", class_="cell")
        if len(cells) < 12:
            continue

        rank_str = cells[0].get_text(strip=True)
        start_no = cells[1].get_text(strip=True)
        dog = cells[2].get_text(strip=True)
        handler = cells[3].get_text(strip=True)
        breed = cells[4].get_text(strip=True)
        chip = cells[5].get_text(strip=True)
        # cells[6] = club (not used in target format)
        time_str = cells[7].get_text(strip=True)
        faults_str = cells[8].get_text(strip=True)
        refusals_str = cells[9].get_text(strip=True)
        total_pen_str = cells[10].get_text(strip=True)
        qualification = cells[11].get_text(strip=True)

        # Clean dog name — remove "(cp)" suffix
        dog = re.sub(r'\s*\(cp\)\s*$', '', dog).strip()

        rank = ""
        if rank_str != "-":
            try:
                ir = int(rank_str)
                if ir > 0:
                    rank = ir
            except ValueError:
                rank = ""

        time_val = ""
        if time_str and time_str != "0":
            try:
                time_val = float(time_str)
            except ValueError:
                time_val = ""

        faults = ""
        if faults_str != "":
            try:
                faults = int(faults_str)
            except ValueError:
                faults = ""

        refusals = ""
        if refusals_str != "":
            try:
                refusals = int(refusals_str)
            except ValueError:
                refusals = ""

        total_faults = ""
        if total_pen_str != "":
            try:
                total_faults = float(total_pen_str)
            except (ValueError, TypeError):
                total_faults = ""

        eliminated = qualification.lower() in ("eliminato", "eliminated")
        absent = qualification.lower() in ("assente", "absent")

        country = guess_country_from_chip(chip)

        speed = ""
        course_length = metadata.get("course_length", "")
        if time_val not in ("", 0) and course_length not in ("", 0):
            try:
                speed = round(float(course_length) / float(time_val), 2)
            except Exception:
                speed = ""

        time_faults = ""
        if total_faults != "" and faults != "" and refusals != "":
            try:
                tf = float(total_faults) - int(faults) * 5 - int(refusals) * 5
                if tf < 0:
                    tf = 0
                time_faults = round(tf, 2)
            except Exception:
                time_faults = ""

        results.append({
            "rank": rank,
            "start_no": start_no,
            "handler": handler,
            "dog": dog,
            "breed": breed,
            "country": country,
            "time": time_val,
            "faults": faults,
            "refusals": refusals,
            "time_faults": time_faults,
            "total_faults": total_faults,
            "speed": speed,
            "eliminated": eliminated,
            "absent": absent,
        })

    return metadata, results


def main():
    all_results = []

    for trial_key, trial_info in TRIALS.items():
        trial_id = trial_info["trial_id"]
        discipline = trial_info["discipline"]
        seq = trial_info["seq"]

        for size_id, size_name in SIZE_IDS.items():
            url = f"https://sport.enci.it/agility-dog/open/ranking/{COMPETITION_ID}/{trial_id}/all/{size_id}"
            cache_path = HTML_DIR / f"{trial_key}_{size_name.lower()}.html"

            if cache_path.exists():
                print(f"  [cached] {trial_key} {size_name}")
                html = cache_path.read_text(encoding="utf-8")
            else:
                html = fetch_url(url)
                cache_path.write_text(html, encoding="utf-8")

            metadata, results = parse_enci_page(html)

            # Filter out absent entries
            results = [r for r in results if not r["absent"]]

            for row in results:
                round_key = f"ind_{discipline.lower()}_{size_name.lower()}_{seq}"
                round_key = re.sub(r"[^a-z0-9_]+", "_", round_key)

                is_elim = bool(row["eliminated"]) or row["rank"] == ""

                all_results.append({
                    "competition": "Alpine Agility Open 2024",
                    "round_key": round_key,
                    "size": size_name,
                    "discipline": discipline,
                    "is_team_round": "False",
                    "rank": "" if is_elim else row["rank"],
                    "start_no": row["start_no"],
                    "handler": row["handler"],
                    "dog": row["dog"],
                    "breed": row["breed"],
                    "country": row["country"],
                    "faults": "" if is_elim else row["faults"],
                    "refusals": "" if is_elim else row["refusals"],
                    "time_faults": "" if is_elim else row["time_faults"],
                    "total_faults": "" if is_elim else row["total_faults"],
                    "time": "" if is_elim else row["time"],
                    "speed": "" if is_elim else row["speed"],
                    "eliminated": "True" if is_elim else "False",
                    "judge": metadata.get("judge", ""),
                    "sct": metadata.get("sct", ""),
                    "mct": metadata.get("mct", ""),
                    "course_length": metadata.get("course_length", ""),
                })

            elim_count = sum(1 for r in results if r["eliminated"])
            print(f"  {trial_key} {size_name}: {len(results)} results ({elim_count} eliminated)")

    print(f"\nTotal results: {len(all_results)}")

    if not all_results:
        print("No results to write.")
        return

    all_results.sort(
        key=lambda r: (
            r["round_key"],
            int(r["rank"]) if str(r["rank"]).isdigit() else 999999,
            str(r["start_no"]),
        )
    )

    # Write CSV
    csv_path = BASE_DIR / "alpine_agility_open_2024_results.csv"
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=TARGET_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(all_results)

    print(f"CSV written to: {csv_path}")

    # Summary
    print("\n--- Summary by round ---")
    from collections import Counter
    by_round = Counter(r["round_key"] for r in all_results)
    for key, count in sorted(by_round.items()):
        elim = sum(1 for r in all_results if r["round_key"] == key and r["eliminated"] == "True")
        print(f"  {key}: {count} ({elim} eliminated)")

    # Country summary
    print("\n--- Countries detected ---")
    by_country = Counter(r["country"] for r in all_results if r["country"])
    for country, count in by_country.most_common():
        print(f"  {country}: {count}")

    unknown = sum(1 for r in all_results if not r["country"])
    if unknown:
        print(f"  [unknown]: {unknown}")


if __name__ == "__main__":
    main()
