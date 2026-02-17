#!/usr/bin/env python3
"""
Download and parse JOAWC + SOAWC 2025 results from FlowAgility into CSV.
Requires: playwright (pip3 install playwright && python3 -m playwright install chromium)

Usage:
  python3 download_and_parse.py --email YOUR_EMAIL --password YOUR_PASSWORD
  # Or set environment variables:
  FLOW_EMAIL=... FLOW_PASSWORD=... python3 download_and_parse.py
  # Parse already cached files:
  python3 download_and_parse.py --skip-download
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

# FlowAgility event for JOAWC + SOAWC 2025 in Abrantes
EVENT_ID = "466ec264-39cb-48da-9d1a-b01209014cf7"

# JOAWC individual runs — /results (agility runs) and /combined_results-only (jumping runs)
# Mapped from agilitynow.eu/results/junior-open-agility-world-championship-2025/
JOAWC_INDIVIDUAL_RUNS = {
    # U19
    "joawc_ind_agility_large_u19": "3461bcc4-de94-4d59-aa17-b1e6be04284f",
    "joawc_ind_agility_intermediate_u19": "5f514d8d-b219-4f72-9054-e942c17409ee",
    "joawc_ind_agility_medium_u19": "0d93b33a-f672-4dca-8e25-493e126f8491",
    "joawc_ind_agility_small_u19": "ea2b6a26-9652-4eb0-9ade-cd5f4cb3d3c6",
    # U15
    "joawc_ind_agility_large_u15": "8fe83a81-3106-4b75-a191-0bedf19e29b0",
    "joawc_ind_agility_intermediate_u15": "1b96df62-001a-4223-b1d7-3d36ca0ab797",
    "joawc_ind_agility_medium_u15": "d3fe1c08-3fcd-4a18-97dd-22c8eadc72d6",
    "joawc_ind_agility_small_u15": "0b16fbe6-5a12-4603-b20e-1255b97ad756",
    # U12
    "joawc_ind_agility_large_u12": "61b35cf4-5889-4fa5-bd43-a98992563db3",
    "joawc_ind_agility_intermediate_u12": "a2709324-49fd-49fc-8462-d912ed5b6347",
    "joawc_ind_agility_medium_u12": "eec1df36-55b4-46bc-8b3d-426bed1f7a0c",
    "joawc_ind_agility_small_u12": "ab2d167c-63b0-47d8-a4ef-bfc97cde3e2b",
}

# JOAWC jumping runs (only linked via /combined_results on agilitynow,
# but the same UUID has /results available too)
JOAWC_JUMPING_RUNS = {
    # U19
    "joawc_ind_jumping_large_u19": "2a3ca30e-96cd-4fe4-ac7f-1621ea2add00",
    "joawc_ind_jumping_intermediate_u19": "c8a0c12e-887f-49f1-a0c9-20fde5fe2b9d",
    "joawc_ind_jumping_medium_u19": "54b5e6cb-0ad0-4b8f-a21b-e9deeedf1768",
    "joawc_ind_jumping_small_u19": "7d11d127-0e7e-4e52-8804-2f0fec0ad778",
    # U15
    "joawc_ind_jumping_large_u15": "5d152a2b-ac3f-455b-b3c3-7132d5bb2950",
    "joawc_ind_jumping_intermediate_u15": "1291072f-cd4c-43a4-9adb-3ba00ef57d87",
    "joawc_ind_jumping_medium_u15": "0a33b6b9-1612-4991-9ffe-d58e706d257a",
    "joawc_ind_jumping_small_u15": "104b7c6b-e016-467b-98a4-ad78a36da1b0",
    # U12
    "joawc_ind_jumping_large_u12": "3d62b7e7-2ad2-43b1-891e-fda395f20b7e",
    "joawc_ind_jumping_intermediate_u12": "48c83760-975d-47f2-b1b0-8eadcbc678e0",
    "joawc_ind_jumping_medium_u12": "1d80b125-2f61-4243-8108-b22a929730a0",
    "joawc_ind_jumping_small_u12": "b1a50aef-3484-4970-be57-a267251f4455",
}

# JOAWC team runs (individual results from team competition)
JOAWC_TEAM_RUNS = {
    "joawc_team_agility_large": "733bf495-a13b-4aec-82bc-ca75a9e21a73",
    "joawc_team_agility_intermediate": "90a31d13-8154-4eaf-9a6e-08428d1d7fa5",
    "joawc_team_agility_medium": "d4b30a18-c1ca-4b2f-bf59-b73994df2953",
    "joawc_team_agility_small": "0a7810db-ed5d-4a25-bd84-b6440c88d318",
    # Team jumping (from team_combined_results page — separate UUIDs)
    "joawc_team_jumping_large": "8181871a-1370-4a79-adbd-154fdc4bf8e3",
    "joawc_team_jumping_intermediate": "3c178f15-a68f-4eff-bde5-dd9fa13b0a8f",
    "joawc_team_jumping_medium": "72317421-c24f-460b-8349-0a9f1241aa52",
    "joawc_team_jumping_small": "310cfaf9-b82d-4eba-903d-bcf05d286779",
}

# SOAWC individual runs
SOAWC_INDIVIDUAL_RUNS = {
    # Senior 55
    "soawc_ind_agility_large_s55": "0a844b65-2dbd-42b3-b05a-93834880ae3f",
    "soawc_ind_jumping_large_s55": "4149836c-ed25-4f41-8b58-025a574610a5",
    "soawc_ind_agility_intermediate_s55": "6df4e422-e290-4660-a8eb-0c03ae0f52da",
    "soawc_ind_jumping_intermediate_s55": "7437aeed-3033-4cff-8529-f1aba2a6ada2",
    "soawc_ind_agility_medium_s55": "866bf0f3-7da0-4344-8677-a8a3c8273226",
    "soawc_ind_jumping_medium_s55": "fcfc0df5-474b-40d0-a683-0dc2cfcfe35e",
    "soawc_ind_agility_small_s55": "9e5a3303-0543-46c9-98ae-315f0876787d",
    "soawc_ind_jumping_small_s55": "f2e2b2ce-a4f5-4443-b508-74c93f3dd224",
    # Senior 65
    "soawc_ind_agility_large_s65": "5951aa90-e577-4646-8c9b-39ec0b77f306",
    "soawc_ind_jumping_large_s65": "5978c314-0af4-4621-b980-851651427f1d",
    "soawc_ind_agility_intermediate_s65": "f2482e14-0411-404c-8005-55d06629a0d0",
    "soawc_ind_jumping_intermediate_s65": "ca02a0cf-7627-4d46-b46e-3aeb3808b4cf",
    "soawc_ind_agility_medium_s65": "7f6624b8-26f6-4917-88b5-5b974bdb50d4",
    "soawc_ind_jumping_medium_s65": "9b6da5cb-1053-4541-8e41-72477b87b04f",
    "soawc_ind_agility_small_s65": "a7e235c3-93ee-44de-97a3-54f17bb5cc86",
    "soawc_ind_jumping_small_s65": "2a48f478-49c7-4685-a7c0-814c48198679",
}

# SOAWC team runs
SOAWC_TEAM_RUNS = {
    "soawc_team_large": "5e69c6bc-3b20-4216-8ca8-367fb210b3b8",
    "soawc_team_intermediate": "b193485d-a80d-42ce-920a-0662f0963f25",
    "soawc_team_medium": "d2550b72-9855-4acd-9971-b50765e5eb17",
    "soawc_team_small": "9fcb1475-3cad-4ff6-9ae5-a8e3aa7c73dc",
}

ALL_RUNS = {
    **JOAWC_INDIVIDUAL_RUNS,
    **JOAWC_JUMPING_RUNS,
    **JOAWC_TEAM_RUNS,
    **SOAWC_INDIVIDUAL_RUNS,
    **SOAWC_TEAM_RUNS,
}

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

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
    "US": "USA", "WE": "GBR",
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


def download_run_text(page, run_key: str, run_id: str) -> str:
    """Navigate to a run results page and save its text content."""
    text_path = HTML_DIR / f"{run_key}.txt"
    if text_path.exists():
        print(f"  [cached] {run_key}")
        return text_path.read_text(encoding="utf-8")

    # Use /results for individual runs
    url = f"https://www.flowagility.com/zone/run/{run_id}/results#list_anchor_header"
    print(f"  [downloading] {run_key} ...")

    page.goto(url, wait_until="networkidle")
    time.sleep(4)  # Wait for LiveView to render

    # Scroll to bottom to trigger lazy loading
    page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
    time.sleep(2)

    # Keep scrolling until all results are loaded
    prev_len = 0
    for _ in range(10):
        text = page.evaluate("() => document.body.innerText")
        if len(text) == prev_len:
            break
        prev_len = len(text)
        page.evaluate("window.scrollTo(0, document.body.scrollHeight)")
        time.sleep(1.5)

    text = page.evaluate("() => document.body.innerText")
    text_path.write_text(text, encoding="utf-8")
    print(f"  [saved] {run_key} ({len(text)} chars)")
    return text


def extract_metadata(run_key: str) -> dict:
    """Extract competition, size, discipline, age group, and team flag from run key."""
    meta = {
        "competition": "",
        "size": "",
        "discipline": "",
        "age_group": "",
        "is_team_round": False,
    }

    if run_key.startswith("joawc_"):
        meta["competition"] = "JOAWC 2025"
    elif run_key.startswith("soawc_"):
        meta["competition"] = "SOAWC 2025"

    meta["is_team_round"] = "_team_" in run_key

    for s in ["small", "medium", "intermediate", "large"]:
        if s in run_key:
            meta["size"] = s.capitalize()
            break

    if "jumping" in run_key:
        meta["discipline"] = "Jumping"
    elif "agility" in run_key:
        meta["discipline"] = "Agility"
    elif meta["is_team_round"]:
        # SOAWC team run keys don't encode agility/jumping explicitly.
        meta["discipline"] = "Final"

    # Age group for JOAWC
    for ag in ["u12", "u15", "u19"]:
        if ag in run_key:
            meta["age_group"] = ag.upper()
            break

    # Senior category for SOAWC
    for sc in ["s55", "s65"]:
        if sc in run_key:
            meta["age_group"] = sc.upper()
            break

    return meta


def parse_country(raw: str) -> str:
    txt = (raw or "").replace("\xa0", " ").strip()
    if not txt:
        return ""

    # Team labels often look like "LU - Team Name"
    txt = txt.split(" - ", 1)[0].strip()
    txt = re.sub(r"^team\s+", "", txt, flags=re.IGNORECASE).strip()
    txt = COUNTRY_NAME_ALIASES.get(txt, txt)

    # Full country name
    if txt in COUNTRY_MAP:
        return COUNTRY_MAP[txt]
    for name, iso3 in COUNTRY_MAP.items():
        if txt.lower() == name.lower():
            return iso3

    code = txt.upper().replace(".", "")
    if re.fullmatch(r"[A-Z]{2}", code):
        return COUNTRY_ISO2_TO_ISO3.get(code, "")
    if re.fullmatch(r"[A-Z]{3}", code):
        return COUNTRY_CODE_ALIASES.get(code, code)

    # Team names usually keep country in first token, e.g. "GB Green"
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

    lower = txt.lower()
    for name in COUNTRY_NAMES:
        if re.search(rf"\b{re.escape(name.lower())}\b", lower):
            return COUNTRY_MAP[name]

    return ""


def parse_text_results(text: str, run_key: str) -> list:
    """Parse result entries from FlowAgility page text.

    Format per entry:
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

    # Find where results start (after "[count]")
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

        # Collect entry lines
        entry_lines = []
        j = i + 1
        while j < len(lines) and len(entry_lines) < 7:
            l = lines[j].strip()
            if not l:
                j += 1
                continue
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

        time_val = None
        total_faults = None
        speed = None
        eliminated = False

        remaining = entry_lines[3:]

        if remaining:
            status_line = remaining[0]
            if "DISQ" in status_line or status_line == "NP":
                eliminated = True
            else:
                time_match = re.match(r'^([\d.]+)\s*s$', status_line)
                if time_match:
                    time_val = float(time_match.group(1))

        if not eliminated and len(remaining) > 1:
            tp_match = re.match(r'^([\d.]+)\s*TP$', remaining[1])
            if tp_match:
                total_faults = float(tp_match.group(1))

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
    parser = argparse.ArgumentParser(description="Download JOAWC + SOAWC 2025 results from FlowAgility")
    parser.add_argument("--email", default=os.environ.get("FLOW_EMAIL"), help="FlowAgility email")
    parser.add_argument("--password", default=os.environ.get("FLOW_PASSWORD"), help="FlowAgility password")
    parser.add_argument("--headed", action="store_true", help="Run browser in headed mode")
    parser.add_argument("--skip-download", action="store_true", help="Skip download, only parse cached files")
    parser.add_argument("--only", help="Download only runs matching this prefix (e.g. 'joawc_ind', 'soawc')")
    args = parser.parse_args()

    if not args.skip_download and (not args.email or not args.password):
        print("Error: Provide credentials via --email/--password or FLOW_EMAIL/FLOW_PASSWORD env vars")
        print("  Or use --skip-download to parse already cached files")
        sys.exit(1)

    runs_to_process = ALL_RUNS
    if args.only:
        runs_to_process = {k: v for k, v in ALL_RUNS.items() if k.startswith(args.only)}
        print(f"Filtering to {len(runs_to_process)} runs matching '{args.only}'")

    # Step 1: Download
    if not args.skip_download:
        with sync_playwright() as p:
            browser = p.chromium.launch(headless=not args.headed)
            context = browser.new_context(
                viewport={"width": 1920, "height": 1080},
                locale="en-GB",
            )
            page = context.new_page()
            login(page, args.email, args.password)

            for run_key, run_id in runs_to_process.items():
                download_run_text(page, run_key, run_id)

            browser.close()

    # Step 2: Parse
    print("\n--- Parsing results ---")
    all_results = []

    for run_key in runs_to_process:
        text_path = HTML_DIR / f"{run_key}.txt"
        if not text_path.exists():
            print(f"  [MISSING] {run_key}")
            continue

        text = text_path.read_text(encoding="utf-8")
        rows = parse_text_results(text, run_key)
        meta = extract_metadata(run_key)

        for row in rows:
            row["competition"] = meta["competition"]
            row["round_key"] = run_key
            row["size"] = meta["size"]
            row["discipline"] = meta["discipline"]
            row["is_team_round"] = meta["is_team_round"]
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

    if not all_results:
        print("No results to write.")
        return

    # Step 3: Write CSV
    csv_path = BASE_DIR / "joawc_soawc_2025_results.csv"
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=TARGET_COLUMNS, lineterminator="\n")
        writer.writeheader()
        for row in all_results:
            csv_row = {}
            for field in TARGET_COLUMNS:
                val = row.get(field)
                if val is None:
                    csv_row[field] = ""
                elif isinstance(val, bool):
                    csv_row[field] = str(val)
                else:
                    csv_row[field] = val
            writer.writerow(csv_row)
    print(f"CSV written to: {csv_path}")

    # Summary
    print("\n--- Summary by round ---")
    from collections import Counter
    by_round = Counter(r["round_key"] for r in all_results)
    for key, count in sorted(by_round.items()):
        elim = sum(1 for r in all_results if r["round_key"] == key and r["eliminated"])
        print(f"  {key}: {count} runs ({elim} eliminated)")

    # Summary by competition
    print("\n--- Summary by competition ---")
    by_comp = Counter(r["competition"] for r in all_results)
    for comp, count in sorted(by_comp.items()):
        print(f"  {comp}: {count} total results")


if __name__ == "__main__":
    main()
