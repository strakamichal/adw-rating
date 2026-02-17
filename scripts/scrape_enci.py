#!/usr/bin/env python3
"""Scrape competition results from sport.enci.it and save as CSV.

Usage:
    python scripts/scrape_enci.py proseccup-2025
    python scripts/scrape_enci.py --list          # show available competitions
"""

import csv
import os
import re
import sys
import time

import requests

BASE_URL = "https://sport.enci.it"
DELAY = 0.5  # seconds between requests

# URL pattern: /agility-dog/trial/ranking/{trial_id}/{round}/{size_code}
# Size codes: 3=Small, 4=Medium, 5=Large, 6=Intermedium
SIZE_CODES = {3: "Small", 4: "Medium", 5: "Large", 6: "Intermediate"}

# Microchip prefix (first 3 digits) to ISO3 country code.
CHIP_COUNTRY_MAP = {
    "380": "ITA", "250": "FRA", "276": "DEU", "705": "SVN", "203": "CZE",
    "756": "CHE", "040": "AUT", "826": "GBR", "967": "GBR", "941": "AUS",
    "528": "NLD", "616": "POL", "246": "FIN", "752": "SWE", "578": "NOR",
    "348": "HUN", "191": "HRV", "703": "SVK", "642": "ROU", "100": "BGR",
    "724": "ESP", "620": "PRT", "056": "BEL", "208": "DNK", "372": "IRL",
    "840": "USA", "124": "CAN", "900": "AUT", "985": "ITA", "981": "ITA",
    "982": "ITA", "688": "SRB",
}

# Competition definitions: trial IDs per day
COMPETITIONS = {
    "proseccup-2025": {
        "name": "ProsecCup 2025",
        "trials": [4446, 4447, 4448],  # Fri 17/1, Sat 18/1, Sun 19/1
        "rounds_per_trial": 3,
    },
    "proseccup-2024": {
        "name": "ProsecCup 2024",
        "trials": [3961, 3962, 3963],  # Fri 19/1, Sat 20/1, Sun 21/1
        "rounds_per_trial": 3,
    },
}

HEADERS = {
    "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
                  "AppleWebKit/537.36 (KHTML, like Gecko) "
                  "Chrome/120.0.0.0 Safari/537.36"
}


def fetch(url):
    """Fetch a URL with delay and return text."""
    print(f"  GET {url}")
    time.sleep(DELAY)
    resp = requests.get(url, headers=HEADERS, timeout=30)
    resp.raise_for_status()
    return resp.text


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


def parse_sections(html):
    """Parse an ENCI ranking page into sections (Agility, Jumping, Combinata).

    Returns list of dicts with keys: title, judge, course_length, sct, mct,
    obstacles, rows (list of dicts).
    """
    # Split on orange section headers
    parts = re.split(r'<div class="grid orange">', html)
    sections = []

    for part in parts[1:]:  # skip before first header
        # Title
        h1 = re.search(r'<h1>(.*?)</h1>', part, re.DOTALL)
        if not h1:
            continue
        title = re.sub(r'<[^>]+>', '', h1.group(1)).strip()

        # Skip COMBINATA sections (combined results, not individual runs)
        if "COMBINATA" in title.upper():
            continue

        # Course info
        course_length = ""
        sct = ""
        mct = ""
        judge = ""
        obstacles = ""

        info_match = re.search(
            r'Lunghezza:\s*([\d.]+).*?Velocità:\s*([\d.]+).*?'
            r'TPS:\s*([\d.]+).*?TPM:\s*([\d.]+).*?Ostacoli:\s*(\d+)',
            part, re.DOTALL
        )
        if info_match:
            course_length = info_match.group(1)
            sct = info_match.group(3)
            mct = info_match.group(4)
            obstacles = info_match.group(5)

        judge_match = re.search(r'Giudice:\s*([^<]+)', part)
        if judge_match:
            judge = judge_match.group(1).strip()

        # Parse rows (skip header row)
        rows = []
        for row_match in re.finditer(
            r'<div class="row">(.*?)</div>\s*</div>', part, re.DOTALL
        ):
            cells = re.findall(
                r'<div class="cell">(.*?)</div>', row_match.group(1)
            )
            cells = [re.sub(r'<[^>]+>', '', c).strip() for c in cells]

            if len(cells) >= 11:
                rank = cells[0]
                # "-" means eliminated/absent
                eliminated = "True" if rank == "-" else "False"

                # Qualifica (if present)
                qualifica = cells[11] if len(cells) > 11 else ""
                if qualifica.lower() in ("eliminato", "assente"):
                    eliminated = "True"

                rows.append({
                    "rank": rank if rank != "-" else "",
                    "start_no": cells[1],
                    "dog": cells[2],
                    "handler": cells[3],
                    "breed": cells[4],
                    "chip": cells[5],
                    "club": cells[6],
                    "time": cells[7],
                    "faults": cells[8],
                    "refusals": cells[9],
                    "total_faults": cells[10],
                    "eliminated": eliminated,
                })

        sections.append({
            "title": title,
            "judge": judge,
            "course_length": course_length,
            "sct": sct,
            "mct": mct,
            "rows": rows,
        })

    return sections


def parse_discipline(title):
    """Extract discipline from section title.

    Examples:
        'AGILITY - 3 ASSOLUTI - LARGE' -> 'Agility'
        'JUMPING - 2 ASSOLUTI - MEDIUM' -> 'Jumping'
        'AGILITY BIS - 1 ASSOLUTI - SMALL' -> 'Agility'
    """
    t = title.upper()
    if "JUMPING" in t:
        return "Jumping"
    return "Agility"


def parse_size(title):
    """Extract size from section title."""
    t = title.upper()
    # Check INTERMEDIUM before MEDIUM (MEDIUM is substring of INTERMEDIUM)
    if "INTERMEDIUM" in t or "INTERMEDIATE" in t:
        return "Intermediate"
    if "SMALL" in t:
        return "Small"
    if "MEDIUM" in t:
        return "Medium"
    if "LARGE" in t:
        return "Large"
    return ""


def scrape_competition(comp_key):
    """Scrape all results for a competition and return list of CSV rows."""
    comp = COMPETITIONS[comp_key]
    all_rows = []
    round_counter = 0

    for trial_idx, trial_id in enumerate(comp["trials"]):
        day_num = trial_idx + 1

        for round_num in range(1, comp["rounds_per_trial"] + 1):
            for size_code, size_name in SIZE_CODES.items():
                url = (
                    f"{BASE_URL}/agility-dog/trial/ranking/"
                    f"{trial_id}/{round_num}/{size_code}"
                )
                try:
                    html = fetch(url)
                except Exception as e:
                    print(f"    SKIP {url}: {e}")
                    continue

                sections = parse_sections(html)
                if not sections:
                    continue

                for section in sections:
                    round_counter += 1
                    discipline = parse_discipline(section["title"])
                    actual_size = parse_size(section["title"]) or size_name

                    # Build round key like "day1_agility_r1" or "day2_jumping_r2"
                    # Use the section title for uniqueness
                    title_parts = section["title"].split(" - ")
                    disc_label = title_parts[0].strip().lower().replace(" ", "_")
                    grade = ""
                    if len(title_parts) > 1:
                        grade = title_parts[1].strip().split()[0]

                    round_key = f"day{day_num}_{disc_label}_g{grade}"

                    for row in section["rows"]:
                        all_rows.append({
                            "competition": comp["name"],
                            "round_key": round_key,
                            "size": actual_size,
                            "discipline": discipline,
                            "is_team_round": "False",
                            "rank": row["rank"],
                            "start_no": row["start_no"],
                            "handler": row["handler"],
                            "dog": row["dog"],
                            "breed": row["breed"],
                            "country": guess_country_from_chip(row["chip"]),
                            "faults": row["faults"],
                            "refusals": row["refusals"],
                            "time_faults": "",
                            "total_faults": row["total_faults"],
                            "time": row["time"],
                            "speed": "",
                            "eliminated": row["eliminated"],
                            "judge": section["judge"],
                            "sct": section["sct"],
                            "mct": section["mct"],
                            "course_length": section["course_length"],
                        })

    return all_rows


def save_csv(rows, output_dir):
    """Save rows to CSV in the target format."""
    os.makedirs(output_dir, exist_ok=True)
    filepath = os.path.join(output_dir, "results.csv")

    fieldnames = [
        "competition", "round_key", "size", "discipline", "is_team_round",
        "rank", "start_no", "handler", "dog", "breed", "country",
        "faults", "refusals", "time_faults", "total_faults", "time", "speed",
        "eliminated", "judge", "sct", "mct", "course_length",
    ]

    with open(filepath, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        for row in rows:
            out = dict(row)
            if out.get("eliminated") == "True":
                for col in ("rank", "faults", "refusals", "time_faults", "total_faults", "time", "speed"):
                    out[col] = ""
            writer.writerow(out)

    print(f"\nSaved {len(rows)} rows to {filepath}")


def main():
    if len(sys.argv) < 2 or sys.argv[1] == "--list":
        print("Available competitions:")
        for key, comp in COMPETITIONS.items():
            status = "ready" if comp["trials"] else "no trial IDs"
            print(f"  {key} ({status})")
        sys.exit(0)

    comp_key = sys.argv[1]
    if comp_key not in COMPETITIONS:
        print(f"Unknown competition: {comp_key}")
        print(f"Available: {', '.join(COMPETITIONS.keys())}")
        sys.exit(1)

    if not COMPETITIONS[comp_key]["trials"]:
        print(f"No trial IDs configured for {comp_key}")
        sys.exit(1)

    print(f"Scraping {COMPETITIONS[comp_key]['name']}...")
    rows = scrape_competition(comp_key)

    output_dir = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "data",
        comp_key.replace("-", "_"),
    )
    save_csv(rows, output_dir)

    # Summary
    sizes = {}
    disciplines = {}
    for row in rows:
        sizes[row["size"]] = sizes.get(row["size"], 0) + 1
        disciplines[row["discipline"]] = disciplines.get(row["discipline"], 0) + 1

    print(f"\nSummary:")
    print(f"  Total rows: {len(rows)}")
    print(f"  By size: {sizes}")
    print(f"  By discipline: {disciplines}")


if __name__ == "__main__":
    main()
