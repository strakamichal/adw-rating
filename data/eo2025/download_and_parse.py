#!/usr/bin/env python3
"""
Download and parse EO 2025 results from FlowAgility into CSV/JSON.
Requires: playwright (pip3 install playwright && python3 -m playwright install chromium)

Usage:
  python3 download_and_parse.py --email YOUR_EMAIL --password YOUR_PASSWORD
  # Or set environment variables:
  FLOW_EMAIL=... FLOW_PASSWORD=... python3 download_and_parse.py
"""

import argparse
import csv
import json
import os
import re
import sys
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

BASE_DIR = Path(__file__).parent
HTML_DIR = BASE_DIR / "html"
HTML_DIR.mkdir(exist_ok=True)

# FlowAgility run IDs mapped to round keys
INDIVIDUAL_RUNS = {
    # Day 2 - Individual Jumping
    "ind_jumping_large": "2d3473ca-b43b-49ae-b4aa-93faa48d24f1",
    "ind_jumping_intermediate": "f7990049-34b7-4bcc-9336-33a5d9e634cd",
    "ind_jumping_medium": "68b85992-843c-4c3c-8678-8fab24970a0d",
    "ind_jumping_small": "a570374f-2a6e-4073-8edf-c9ceaf743830",
    # Day 2 - Individual Agility
    "ind_agility_large": "1ebc8bb1-f6a5-4bc8-bc7d-14ebf6175c41",
    "ind_agility_intermediate": "f596830e-3023-4595-abf1-22717ce768e4",
    "ind_agility_medium": "f533de06-77d1-4094-8529-206fa7785095",
    "ind_agility_small": "52fc7062-fb82-4feb-9c64-b4d13599afc5",
}

TEAM_RUNS = {
    # Day 1 - Team Jumping (individual results)
    "team_jumping_large_ind": "d93a9730-6e87-4420-9f15-7fc99c664b94",
    "team_jumping_intermediate_ind": "b2349c3d-43ed-4215-858f-b2e40187b333",
    "team_jumping_medium_ind": "9d0d8e04-6ec8-4415-9d11-c9cfb4a9bb86",
    "team_jumping_small_ind": "c3bfad46-9c88-4334-b55b-12c7e00ef0a8",
    # Day 1 - Team Agility (individual results)
    "team_agility_large_ind": "5dd66ed6-5af6-4b3d-97f6-df71943bf77f",
    "team_agility_intermediate_ind": "4c1159a2-138d-4c5c-8a8e-f1936fdc461f",
    "team_agility_medium_ind": "a77823dc-c112-45df-91f4-9261e8f2bbee",
    "team_agility_small_ind": "11fc6d79-2bda-4642-aa7b-db82c2210a67",
}

# Day 3 - Individual Finals
FINAL_RUNS = {
    "ind_final_large": "7a199944-0dc9-4e27-9f23-fb5d325f3116",
    "ind_final_intermediate": "bc503a93-28a4-4f95-b97f-4914c1c50338",
    "ind_final_medium": "5975b86e-b058-42ba-ab4c-c778977bb03e",
    "ind_final_small": "c6ea581d-0102-4856-8093-a336c4bd7441",
}

ALL_RUNS = {**INDIVIDUAL_RUNS, **TEAM_RUNS, **FINAL_RUNS}

# Country name to ISO 3166-1 alpha-3 mapping
COUNTRY_MAP = {
    "Afghanistan": "AFG", "Albania": "ALB", "Algeria": "DZA", "Andorra": "AND",
    "Argentina": "ARG", "Armenia": "ARM", "Australia": "AUS", "Austria": "AUT",
    "Azerbaijan": "AZE", "Bahrain": "BHR", "Bangladesh": "BGD", "Belarus": "BLR",
    "Belgium": "BEL", "Bolivia": "BOL", "Bosnia and Herzegovina": "BIH",
    "Brazil": "BRA", "Bulgaria": "BGR", "Cambodia": "KHM", "Canada": "CAN",
    "Chile": "CHL", "China": "CHN", "Colombia": "COL", "Costa Rica": "CRI",
    "Croatia": "HRV", "Cuba": "CUB", "Cyprus": "CYP", "Czech Republic": "CZE",
    "Denmark": "DNK", "Dominican Republic": "DOM", "Ecuador": "ECU", "Egypt": "EGY",
    "El Salvador": "SLV", "Estonia": "EST", "Finland": "FIN", "France": "FRA",
    "Georgia": "GEO", "Germany": "DEU", "Greece": "GRC", "Guatemala": "GTM",
    "Honduras": "HND", "Hong Kong": "HKG", "Hungary": "HUN", "Iceland": "ISL",
    "India": "IND", "Indonesia": "IDN", "Iran": "IRN", "Iraq": "IRQ",
    "Ireland": "IRL", "Israel": "ISR", "Italy": "ITA", "Jamaica": "JAM",
    "Japan": "JPN", "Jordan": "JOR", "Kazakhstan": "KAZ", "Kenya": "KEN",
    "Kuwait": "KWT", "Latvia": "LVA", "Lebanon": "LBN", "Lithuania": "LTU",
    "Luxembourg": "LUX", "Malaysia": "MYS", "Malta": "MLT", "Mexico": "MEX",
    "Moldova": "MDA", "Monaco": "MCO", "Mongolia": "MNG", "Montenegro": "MNE",
    "Morocco": "MAR", "Netherlands": "NLD", "New Zealand": "NZL", "Nicaragua": "NIC",
    "Nigeria": "NGA", "North Macedonia": "MKD", "Norway": "NOR", "Oman": "OMN",
    "Pakistan": "PAK", "Palestine": "PSE", "Panama": "PAN", "Paraguay": "PRY",
    "Peru": "PER", "Philippines": "PHL", "Poland": "POL", "Portugal": "PRT",
    "Qatar": "QAT", "Romania": "ROU", "Russia": "RUS", "Saudi Arabia": "SAU",
    "Serbia": "SRB", "Singapore": "SGP", "Slovakia": "SVK", "Slovenia": "SVN",
    "South Africa": "ZAF", "South Korea": "KOR", "Spain": "ESP", "Sri Lanka": "LKA",
    "Sweden": "SWE", "Switzerland": "CHE", "Taiwan": "TWN", "Thailand": "THA",
    "Tunisia": "TUN", "Turkey": "TUR", "Ukraine": "UKR",
    "United Arab Emirates": "ARE", "United Kingdom": "GBR",
    "United States of America": "USA", "United States": "USA",
    "Uruguay": "URY", "Uzbekistan": "UZB", "Venezuela": "VEN", "Vietnam": "VNM",
}

COUNTRY_ISO2_TO_ISO3 = {
    "AT": "AUT", "AU": "AUS", "BE": "BEL", "BG": "BGR", "BR": "BRA", "CA": "CAN",
    "CH": "CHE", "CL": "CHL", "CN": "CHN", "CO": "COL", "CR": "CRI", "CY": "CYP",
    "CZ": "CZE", "DE": "DEU", "DK": "DNK", "EE": "EST", "ES": "ESP", "FI": "FIN",
    "FR": "FRA", "GB": "GBR", "GR": "GRC", "HR": "HRV", "HU": "HUN", "IE": "IRL",
    "IL": "ISR", "IS": "ISL", "IT": "ITA", "JP": "JPN", "LT": "LTU", "LU": "LUX",
    "LV": "LVA", "MX": "MEX", "NL": "NLD", "NO": "NOR", "PL": "POL", "PT": "PRT",
    "RO": "ROU", "RS": "SRB", "SE": "SWE", "SI": "SVN", "SK": "SVK", "UA": "UKR",
    "US": "USA",
}

COUNTRY_CODE_ALIASES = {
    "DEN": "DNK",
    "GER": "DEU",
    "SLO": "SVN",
    "UK": "GBR",
}

COUNTRY_NAME_ALIASES = {
    "Luxemburg": "Luxembourg",
    "Filand": "Finland",
}

COUNTRY_NAMES = sorted(COUNTRY_MAP.keys(), key=len, reverse=True)


def build_result_url(run_id: str, is_team_results: bool = False) -> str:
    if is_team_results:
        return f"https://www.flowagility.com/zone/run/{run_id}/team_results#list_anchor_header"
    return f"https://www.flowagility.com/zone/run/{run_id}/results#list_anchor_header"


def login(page, email: str, password: str):
    """Log in to FlowAgility."""
    print("Logging in to FlowAgility...")
    page.goto("https://www.flowagility.com/user/login", wait_until="networkidle")
    page.fill("#user_email", email)
    page.fill("#user_password", password)
    page.click('button[type="submit"]')
    page.wait_for_load_state("networkidle")
    time.sleep(2)
    print(f"  Logged in. Current URL: {page.url}")


def download_run_text(page, run_key: str, run_id: str, is_team: bool = False) -> str:
    """Navigate to a run results page and save its text content."""
    text_path = HTML_DIR / f"{run_key}.txt"
    if text_path.exists():
        print(f"  [cached] {run_key}")
        return text_path.read_text(encoding="utf-8")

    url = build_result_url(run_id, is_team_results=is_team)
    print(f"  [downloading] {run_key} ...")

    page.goto(url, wait_until="networkidle")
    time.sleep(4)  # Wait for LiveView to render

    # Scroll to bottom to trigger any lazy loading
    page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
    time.sleep(2)

    text = page.evaluate("() => document.body.innerText")
    text_path.write_text(text, encoding="utf-8")
    print(f"  [saved] {run_key} ({len(text)} chars)")
    return text


def extract_size_and_discipline(run_key: str) -> tuple:
    """Extract size, discipline, and team flag from run key."""
    is_team = run_key.startswith("team_")

    size = "Unknown"
    for s in ["small", "medium", "intermediate", "large"]:
        if s in run_key:
            size = s.capitalize()
            break

    if "jumping" in run_key:
        discipline = "Jumping"
    elif "agility" in run_key:
        discipline = "Agility"
    elif "final" in run_key:
        # Finals are combined (jumping + agility relay), mark as "Final"
        discipline = "Final"
    else:
        discipline = "Unknown"

    return size, discipline, is_team


def parse_country(raw: str) -> str:
    txt = (raw or "").replace("\xa0", " ").strip()
    if not txt:
        return ""

    # Team labels often look like "LU - Team Name"
    txt = txt.split(" - ", 1)[0].strip()
    txt = re.sub(r"^team\s+", "", txt, flags=re.IGNORECASE).strip()
    txt = COUNTRY_NAME_ALIASES.get(txt, txt)

    # Full country name (exact / case-insensitive)
    if txt in COUNTRY_MAP:
        return COUNTRY_MAP[txt]
    for name, iso3 in COUNTRY_MAP.items():
        if txt.lower() == name.lower():
            return iso3

    # Direct code
    code = txt.upper().replace(".", "")
    if re.fullmatch(r"[A-Z]{2}", code):
        return COUNTRY_ISO2_TO_ISO3.get(code, "")
    if re.fullmatch(r"[A-Z]{3}", code):
        return COUNTRY_CODE_ALIASES.get(code, code)

    # First token often carries country in team runs, e.g. "GB Green"
    tokens = re.findall(r"[A-Za-z]+", txt)
    if tokens:
        first = tokens[0].upper()
        if len(first) == 2:
            mapped = COUNTRY_ISO2_TO_ISO3.get(first, "")
            if mapped:
                return mapped
        if len(first) == 3:
            first = COUNTRY_CODE_ALIASES.get(first, first)
            if first in COUNTRY_MAP.values():
                return first

    # Fallback: search known country names inside the string
    lower = txt.lower()
    for name in COUNTRY_NAMES:
        if re.search(rf"\b{re.escape(name.lower())}\b", lower):
            return COUNTRY_MAP[name]

    return ""


def parse_text_results(text: str, run_key: str) -> list:
    """Parse result entries from the page text content.

    FlowAgility text format per entry:
        1º                          (or "-" for eliminated)
        D1 / Austria                (start_no / country)
        Lisa Frick                  (handler)
        Taco                        (dog)
        33.32 s                     (time - or "DISQ" / "DISQ (R)" / "NP")
        0.00 TP                     (total penalties)
        6.12 m/s                    (speed)
        EXC_0                       (quality mark)
    """
    lines = text.strip().split("\n")
    results = []

    # Find where results start (after "Results" and "[count]")
    start_idx = 0
    for i, line in enumerate(lines):
        if re.match(r'\[\d+\]', line.strip()):
            start_idx = i + 1
            break

    i = start_idx
    while i < len(lines):
        line = lines[i].strip()

        # Match rank line: "1º" or "-"
        rank_match = re.match(r'^(\d+)º$', line)
        is_dash = line == "-"

        if not rank_match and not is_dash:
            i += 1
            continue

        rank = int(rank_match.group(1)) if rank_match else None

        # Next lines: start_no/country, handler, dog, time/DISQ, [TP], [speed], [quality]
        entry_lines = []
        j = i + 1
        while j < len(lines) and len(entry_lines) < 7:
            l = lines[j].strip()
            if not l:
                j += 1
                continue
            # Stop if we hit the next rank
            if re.match(r'^(\d+)º$', l) or (l == "-" and len(entry_lines) >= 3):
                break
            entry_lines.append(l)
            j += 1

        if len(entry_lines) < 3:
            i = j
            continue

        # Parse start_no and country
        id_country_match = re.match(r'^[A-Z]?(\d+)\s*/\s*(.+)$', entry_lines[0])
        if not id_country_match:
            i = j
            continue

        start_no = int(id_country_match.group(1))
        country_name = id_country_match.group(2).strip()
        country = parse_country(country_name)

        handler = entry_lines[1]
        dog = entry_lines[2]

        # Check for truncated handler names (ending with "…")
        # Can't fix automatically, just note it

        # Parse time/status
        time_val = None
        total_faults = None
        speed = None
        eliminated = False

        remaining = entry_lines[3:]

        # Check first remaining line for DISQ/NP/time
        if remaining:
            status_line = remaining[0]
            if "DISQ" in status_line or status_line == "NP":
                eliminated = True
            else:
                time_match = re.match(r'^([\d.]+)\s*s$', status_line)
                if time_match:
                    time_val = float(time_match.group(1))

        # Parse TP (total penalties)
        if not eliminated and len(remaining) > 1:
            tp_match = re.match(r'^([\d.]+)\s*TP$', remaining[1])
            if tp_match:
                total_faults = float(tp_match.group(1))

        # Parse speed
        if not eliminated and len(remaining) > 2:
            speed_match = re.match(r'^([\d.]+)\s*m/s$', remaining[2])
            if speed_match:
                speed = float(speed_match.group(1))

        results.append({
            "rank": rank,
            "start_no": start_no,
            "handler": handler,
            "dog": dog,
            "country": country,
            "time": time_val,
            "total_faults": total_faults,
            "speed": speed,
            "eliminated": eliminated,
        })

        i = j

    return results


def main():
    parser = argparse.ArgumentParser(description="Download EO 2025 results from FlowAgility")
    parser.add_argument("--email", default=os.environ.get("FLOW_EMAIL"), help="FlowAgility email")
    parser.add_argument("--password", default=os.environ.get("FLOW_PASSWORD"), help="FlowAgility password")
    parser.add_argument("--headed", action="store_true", help="Run browser in headed mode (visible)")
    parser.add_argument("--skip-download", action="store_true", help="Skip download, only parse cached files")
    args = parser.parse_args()

    if not args.skip_download and (not args.email or not args.password):
        print("Error: Provide credentials via --email/--password or FLOW_EMAIL/FLOW_PASSWORD env vars")
        print("  Or use --skip-download to parse already cached files")
        sys.exit(1)

    # Step 1: Download all run texts
    if not args.skip_download:
        with sync_playwright() as p:
            browser = p.chromium.launch(headless=not args.headed)
            context = browser.new_context(
                viewport={"width": 1920, "height": 1080},
                locale="en-GB",
            )
            page = context.new_page()
            login(page, args.email, args.password)

            print("\nDownloading individual runs...")
            for run_key, run_id in INDIVIDUAL_RUNS.items():
                download_run_text(page, run_key, run_id)

            print("\nDownloading team runs (individual results)...")
            for run_key, run_id in TEAM_RUNS.items():
                download_run_text(page, run_key, run_id)

            print("\nDownloading final runs...")
            for run_key, run_id in FINAL_RUNS.items():
                download_run_text(page, run_key, run_id)

            browser.close()

    # Step 2: Parse all texts into results
    print("\n--- Parsing results ---")
    all_results = []

    for run_key in ALL_RUNS:
        text_path = HTML_DIR / f"{run_key}.txt"
        if not text_path.exists():
            print(f"  [MISSING] {run_key}")
            continue

        text = text_path.read_text(encoding="utf-8")
        rows = parse_text_results(text, run_key)
        size, discipline, is_team = extract_size_and_discipline(run_key)

        for row in rows:
            row["competition"] = "EO 2025"
            row["round_key"] = run_key
            row["size"] = size
            row["discipline"] = discipline
            row["is_team_round"] = is_team
            # Fields not available from FlowAgility
            row["breed"] = ""
            row["faults"] = None if row["eliminated"] else ""
            row["refusals"] = None if row["eliminated"] else ""
            row["time_faults"] = None if row["eliminated"] else ""
            row["judge"] = ""
            row["sct"] = ""
            row["mct"] = ""
            row["course_length"] = ""

        elim_count = sum(1 for r in rows if r["eliminated"])
        print(f"  {run_key}: {len(rows)} results ({elim_count} eliminated)")
        all_results.extend(rows)

    print(f"\nTotal results parsed: {len(all_results)}")

    # Step 3: Write CSV
    csv_path = BASE_DIR / "eo2025_results.csv"
    fieldnames = [
        "competition", "round_key", "size", "discipline", "is_team_round",
        "rank", "start_no", "handler", "dog", "breed", "country",
        "faults", "refusals", "time_faults", "total_faults", "time", "speed",
        "eliminated", "judge", "sct", "mct", "course_length",
    ]
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        for row in all_results:
            csv_row = {}
            for field in fieldnames:
                val = row.get(field)
                if val is None:
                    csv_row[field] = ""
                elif isinstance(val, bool):
                    csv_row[field] = str(val)
                else:
                    csv_row[field] = val
            writer.writerow(csv_row)
    print(f"CSV written to: {csv_path}")

    # Step 4: Write JSON
    json_path = BASE_DIR / "eo2025_results.json"
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(all_results, f, ensure_ascii=False, indent=2)
    print(f"JSON written to: {json_path}")

    # Summary
    print("\n--- Summary by round ---")
    from collections import Counter
    by_round = Counter(r["round_key"] for r in all_results)
    for key, count in sorted(by_round.items()):
        elim = sum(1 for r in all_results if r["round_key"] == key and r["eliminated"])
        print(f"  {key}: {count} runs ({elim} eliminated)")


if __name__ == "__main__":
    main()
