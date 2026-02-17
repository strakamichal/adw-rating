#!/usr/bin/env python3
"""Fetch EO 2024 results from Agility Plaza API and produce CSV."""

import csv
import json
import re
import sys
import urllib.request
from pathlib import Path

API_BASE = "https://api.agilityplaza.com/agilityClass/{}/results"

# All rounds with their Agility Plaza IDs
ROUNDS = {
    # Saturday - Individual
    "ind_jumping_small": 1545017752,
    "ind_jumping_medium": 1605314283,
    "ind_jumping_intermediate": 1687191962,
    "ind_jumping_large": 1286695442,
    "ind_agility_small": 1812640589,
    "ind_agility_medium": 1123067360,
    "ind_agility_intermediate": 2125178842,
    "ind_agility_large": 2108928429,
    # Friday - Team (individual results within team runs)
    "team_jumping_small_ind": 2083877147,
    "team_jumping_medium_ind": 1501426684,
    "team_jumping_intermediate_ind": 1115573195,
    "team_jumping_large_ind": 1474432811,
    "team_agility_small_ind": 1370394117,
    "team_agility_medium_ind": 1611951627,
    "team_agility_intermediate_ind": 1672309882,
    "team_agility_large_ind": 1163730597,
    # Sunday - Individual Finals (agility)
    "ind_final_small": 1172534983,
    "ind_final_medium": 1923551902,
    "ind_final_intermediate": 1704660964,
    "ind_final_large": 1576960420,
    # Sunday - Team Finals (relay — team-level aggregates)
    "team_final_small": 1437037765,
    "team_final_medium": 1568811192,
    "team_final_intermediate": 1510390452,
    "team_final_large": 1343050813,
}

# 2-letter to 3-letter ISO country code mapping
COUNTRY_MAP = {
    "AT": "AUT", "AU": "AUS", "BE": "BEL", "BR": "BRA", "CA": "CAN",
    "CH": "CHE", "CZ": "CZE", "DE": "DEU", "DK": "DNK", "ES": "ESP",
    "FI": "FIN", "FR": "FRA", "GB": "GBR", "GR": "GRC", "HR": "HRV",
    "HU": "HUN", "IE": "IRL", "IL": "ISR", "IT": "ITA", "JP": "JPN",
    "KR": "KOR", "LU": "LUX", "NL": "NLD", "NO": "NOR", "PL": "POL",
    "PT": "PRT", "RO": "ROU", "RS": "SRB", "RU": "RUS", "SE": "SWE",
    "SG": "SGP", "SI": "SVN", "SK": "SVK", "TH": "THA", "US": "USA",
    "ZA": "ZAF", "LT": "LTU", "LV": "LVA", "EE": "EST", "BG": "BGR",
    "MX": "MEX", "CL": "CHL", "CO": "COL", "TW": "TWN", "NZ": "NZL",
    "AR": "ARG", "EC": "ECU", "CR": "CRI", "HK": "HKG",
}

SIZE_MAP = {
    "small": "Small",
    "medium": "Medium",
    "intermediate": "Intermediate",
    "large": "Large",
}


def fetch_json(class_id):
    """Fetch results JSON from Agility Plaza API."""
    url = API_BASE.format(class_id)
    req = urllib.request.Request(url)
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read().decode("utf-8"))


def parse_spec(spec_str):
    """Parse specification string like 'SCT: 39 secs, MCT: 68 secs, 202m, 2 not run'."""
    sct = mct = course_length = None
    m = re.search(r"SCT:\s*([\d.]+)", spec_str)
    if m:
        sct = float(m.group(1))
    m = re.search(r"MCT:\s*([\d.]+)", spec_str)
    if m:
        mct = float(m.group(1))
    m = re.search(r"(\d+)m\b", spec_str)
    if m:
        course_length = float(m.group(1))
    return sct, mct, course_length


def parse_run_data(run_data):
    """Parse runData like 'R, 5, T0.806' into (obstacle_faults_count, refusals_count, time_faults)."""
    if not run_data:
        return 0, 0, 0.0

    # Team final relay format: "C | C | 5 | C"
    if "|" in run_data:
        parts = [p.strip() for p in run_data.split("|")]
        obstacle_faults = sum(1 for p in parts if p == "5")
        refusals = sum(1 for p in parts if p == "R")
        return obstacle_faults, refusals, 0.0

    parts = [p.strip() for p in run_data.split(",")]
    obstacle_faults = 0
    refusals = 0
    time_faults = 0.0

    for part in parts:
        if part == "5":
            obstacle_faults += 1
        elif part == "R":
            refusals += 1
        elif part.startswith("T"):
            try:
                time_faults = float(part[1:])
            except ValueError:
                pass

    return obstacle_faults, refusals, time_faults


def extract_start_no(description_short):
    """Extract numeric start number from description like 'Handler & Dog (PL3417)'."""
    m = re.search(r"\(([A-Z]{2})(\d+)\)", description_short)
    if m:
        return int(m.group(2))
    return 0


def country_to_iso3(code_2):
    """Convert 2-letter country code to ISO 3166-1 alpha-3."""
    return COUNTRY_MAP.get(code_2, code_2)


def parse_round_key(key):
    """Parse round key to get size, discipline, is_team."""
    # e.g. ind_jumping_small, team_agility_large_ind, ind_final_small
    parts = key.split("_")

    is_team = parts[0] == "team"

    # Determine size
    size_raw = None
    for p in parts:
        if p in SIZE_MAP:
            size_raw = p
            break

    size = SIZE_MAP.get(size_raw, "Unknown")

    # Determine discipline
    if "jumping" in parts:
        discipline = "Jumping"
    elif "agility" in parts:
        discipline = "Agility"
    elif "final" in parts:
        discipline = "Agility"  # Finals are agility runs
    else:
        discipline = "Unknown"

    return size, discipline, is_team


def parse_eliminated_text(elim_text, country_map_for_elim=None):
    """Parse eliminated text string into list of dicts.

    Format: 'Handler & Dog (CC1234) (R, 5), Handler2 & Dog2 (CC5678), ...'
    """
    if not elim_text:
        return []

    results = []
    # Split on '), ' but be careful — each entry ends with a closing paren
    # Pattern: 'Handler & Dog (CCnnnn)' optionally followed by ' (fault info)'
    # Then comma-space separator

    # Use regex to find all entries
    pattern = r"([^,]+?\s*\([A-Z]{2}\d+\)(?:\s*\([^)]*\))?)"
    matches = re.findall(pattern, elim_text)

    if not matches:
        # Fallback: try splitting by the pattern of handler entries
        return results

    for match in matches:
        match = match.strip().rstrip(",").strip()
        # Extract handler & dog
        m = re.match(r"^(.+?)\s+&\s+(.+?)\s+\(([A-Z]{2})(\d+)\)(?:\s+\(([^)]*)\))?$", match)
        if m:
            handler = m.group(1).strip()
            dog = m.group(2).strip()
            country_2 = m.group(3)
            start_no = int(m.group(4))
            fault_info = m.group(5) or ""

            results.append({
                "handler": handler,
                "dog": dog,
                "country": country_to_iso3(country_2),
                "start_no": start_no,
                "fault_info": fault_info,
            })

    return results


def is_team_final(round_key):
    """Check if this is a team final (relay aggregate, not individual results)."""
    return round_key.startswith("team_final_")


def process_round(round_key, class_id):
    """Fetch and process one round, returning list of CSV row dicts."""
    print(f"  Fetching {round_key} (id={class_id})...", file=sys.stderr)

    data = fetch_json(class_id)
    ac = data["data"]["agilityClass"]
    competition_name = data["data"]["competition"]["name"]
    judge = ac.get("judge", "")

    size, discipline, is_team = parse_round_key(round_key)

    # For team finals, check if entries are team-level or individual
    if is_team_final(round_key):
        # Team finals are relay aggregates — individual breakdown may not be meaningful
        # We'll still include them but mark accordingly
        pass

    rows = []

    for sc in ac["subClasses"]:
        sct, mct, course_length = parse_spec(sc.get("specification", ""))

        # Process entries
        for entry in sc["entries"]:
            handler = entry["handler"]
            pet_name = entry["petName"]
            reg_name = entry.get("registeredName", "")
            country = country_to_iso3(entry["countryCode"])
            start_no = extract_start_no(entry.get("descriptionShort", ""))
            rank = entry.get("rank", "")
            time_val = entry.get("time", "")
            run_data = entry.get("runData", "")

            # rank=E means eliminated (common in team rounds)
            is_eliminated = (rank == "E")

            # Build dog name: "Registered Name (Pet Name)"
            if reg_name:
                dog = f"{reg_name} ({pet_name})"
            else:
                dog = pet_name

            # Parse faults from runData
            obs_faults, refusals, time_faults = parse_run_data(run_data)

            try:
                time_float = float(time_val) if time_val else None
            except (ValueError, TypeError):
                time_float = None

            speed = None
            if time_float and course_length and time_float > 0:
                speed = round(course_length / time_float, 2)

            # Use API's authoritative total faults value
            try:
                api_total_faults = float(entry.get("faults", "0") or "0")
            except (ValueError, TypeError):
                api_total_faults = obs_faults * 5 + refusals * 5 + time_faults

            # Derive time_faults from API total if available (more precise)
            if api_total_faults > 0:
                time_faults = round(api_total_faults - obs_faults * 5 - refusals * 5, 2)
                if time_faults < 0:
                    time_faults = 0.0

            if is_eliminated:
                row = {
                    "competition": competition_name,
                    "round_key": round_key,
                    "size": size,
                    "discipline": discipline,
                    "is_team_round": str(is_team),
                    "rank": "",
                    "start_no": start_no,
                    "handler": handler,
                    "dog": dog,
                    "breed": "",
                    "country": country,
                    "faults": "",
                    "refusals": "",
                    "time_faults": "",
                    "total_faults": "",
                    "time": "",
                    "speed": "",
                    "eliminated": "True",
                    "judge": judge,
                    "sct": sct if sct else "",
                    "mct": mct if mct else "",
                    "course_length": course_length if course_length else "",
                }
                rows.append(row)
                continue

            row = {
                "competition": competition_name,
                "round_key": round_key,
                "size": size,
                "discipline": discipline,
                "is_team_round": str(is_team),
                "rank": rank if rank else "",
                "start_no": start_no,
                "handler": handler,
                "dog": dog,
                "breed": "",  # Not available in API
                "country": country,
                "faults": obs_faults if not is_team_final(round_key) else "",
                "refusals": refusals if not is_team_final(round_key) else "",
                "time_faults": round(time_faults, 2) if time_faults else 0.0,
                "total_faults": round(api_total_faults, 2) if api_total_faults else 0.0,
                "time": round(time_float, 2) if time_float else "",
                "speed": speed if speed else "",
                "eliminated": "False",
                "judge": judge,
                "sct": sct if sct else "",
                "mct": mct if mct else "",
                "course_length": course_length if course_length else "",
            }
            rows.append(row)

        # Process eliminated entries (from text)
        elim_entries = parse_eliminated_text(sc.get("eliminated", ""))
        for elim in elim_entries:
            row = {
                "competition": competition_name,
                "round_key": round_key,
                "size": size,
                "discipline": discipline,
                "is_team_round": str(is_team),
                "rank": "",
                "start_no": elim["start_no"],
                "handler": elim["handler"],
                "dog": elim["dog"],
                "breed": "",
                "country": elim["country"],
                "faults": "",
                "refusals": "",
                "time_faults": "",
                "total_faults": "",
                "time": "",
                "speed": "",
                "eliminated": "True",
                "judge": judge,
                "sct": sct if sct else "",
                "mct": mct if mct else "",
                "course_length": course_length if course_length else "",
            }
            rows.append(row)

    return rows


def main():
    output_path = Path(__file__).parent.parent / "data" / "eo2024" / "eo2024_results.csv"
    output_path.parent.mkdir(parents=True, exist_ok=True)

    all_rows = []

    # Skip team finals (relay aggregates, not individual results)
    rounds_to_fetch = {k: v for k, v in ROUNDS.items() if not is_team_final(k)}

    print(f"Fetching {len(rounds_to_fetch)} rounds...", file=sys.stderr)

    for round_key, class_id in rounds_to_fetch.items():
        try:
            rows = process_round(round_key, class_id)
            all_rows.extend(rows)
            print(f"    → {len(rows)} entries", file=sys.stderr)
        except Exception as e:
            print(f"    ERROR: {e}", file=sys.stderr)

    # Sort by round_key, then by rank (non-eliminated first, eliminated last)
    def sort_key(row):
        rk = row["round_key"]
        eliminated = row["eliminated"] == "True"
        try:
            rank = int(row["rank"]) if row["rank"] else 9999
        except ValueError:
            rank = 9999
        return (rk, eliminated, rank)

    all_rows.sort(key=sort_key)

    # Write CSV
    fieldnames = [
        "competition", "round_key", "size", "discipline", "is_team_round",
        "rank", "start_no", "handler", "dog", "breed", "country",
        "faults", "refusals", "time_faults", "total_faults", "time", "speed",
        "eliminated", "judge", "sct", "mct", "course_length",
    ]

    with open(output_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(all_rows)

    print(f"\nWrote {len(all_rows)} rows to {output_path}", file=sys.stderr)


if __name__ == "__main__":
    main()
