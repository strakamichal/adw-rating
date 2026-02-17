#!/usr/bin/env python3
"""Normalize all CSV results under data/ to the target 22-column format.

Usage:
    python scripts/normalize_csv.py           # normalize in-place
    python scripts/normalize_csv.py --dry-run # preview changes without writing
"""

from __future__ import annotations

import argparse
import csv
import io
import os
import re
import sys
from collections import Counter
from pathlib import Path

DATA_DIR = Path(__file__).resolve().parent.parent / "data"

TARGET_COLUMNS = [
    "competition", "round_key", "size", "discipline", "is_team_round",
    "rank", "start_no", "handler", "dog", "breed", "country",
    "faults", "refusals", "time_faults", "total_faults", "time", "speed",
    "eliminated", "judge", "sct", "mct", "course_length",
]

# ── Size normalization ──────────────────────────────────────────────
SIZE_MAP = {
    "S": "Small", "Small": "Small", "small": "Small",
    "M": "Medium", "Medium": "Medium", "medium": "Medium",
    "I": "Intermediate", "In": "Intermediate", "Inter": "Intermediate",
    "Intermediate": "Intermediate", "intermediate": "Intermediate",
    "L": "Large", "Large": "Large", "large": "Large",
    "XS": "Small", "Xs": "Small", "XS & S": "Small", "Xs+S": "Small",
    "xs": "Small", "xs+s": "Small",
}

# ── Discipline normalization ────────────────────────────────────────
DISCIPLINE_MAP = {
    "Jumping": "Jumping", "jumping": "Jumping", "JUMPING": "Jumping",
    "Agility": "Agility", "agility": "Agility", "AGILITY": "Agility",
    "A1": "Agility", "A2": "Agility", "A3": "Agility",
    "Final": "Final", "final": "Final", "FINAL": "Final",
    "Grand Final": "Final", "GRAND FINAL": "Final",
    "SOFT FINAL": "Final",
    "Final Team Relay": "Final",
}

EXCLUDE_DISCIPLINES = {"Warm Up"}

# ── Country normalization ───────────────────────────────────────────
COUNTRY_NAME_TO_ISO3 = {
    "Argentina": "ARG", "Australia": "AUS", "Austria": "AUT",
    "Belgium": "BEL", "Brasil": "BRA", "Brazil": "BRA",
    "Bulgaria": "BGR", "Canada": "CAN",
    "Chile": "CHL", "China": "CHN", "Colombia": "COL",
    "Costa Rica": "CRI", "Croatia": "HRV",
    "Czech Republic": "CZE", "Czechia": "CZE",
    "Denmark": "DNK", "Estonia": "EST", "Finland": "FIN",
    "France": "FRA", "Francie": "FRA",
    "Germany": "DEU", "Greece": "GRC",
    "Hungary": "HUN", "Iceland": "ISL", "Ireland": "IRL",
    "Israel": "ISR", "Italy": "ITA", "italy": "ITA",
    "Japan": "JPN", "Latvia": "LVA", "Lithuania": "LTU",
    "Luxembourg": "LUX", "Mexico": "MEX",
    "Netherlands": "NLD", "Norway": "NOR",
    "Poland": "POL", "Portugal": "PRT",
    "Romania": "ROU", "Russia": "RUS",
    "Serbia": "SRB", "Slovakia": "SVK", "Slovenia": "SVN",
    "South Korea": "KOR", "Spain": "ESP",
    "Sweden": "SWE", "Switzerland": "CHE",
    "Ukraine": "UKR", "United Kingdom": "GBR",
    "United States": "USA", "USA": "USA",
}

COUNTRY_ISO2_TO_ISO3 = {
    "AT": "AUT", "BE": "BEL", "BR": "BRA", "CA": "CAN",
    "CH": "CHE", "CL": "CHL", "CN": "CHN", "CO": "COL",
    "CR": "CRI", "CZ": "CZE", "DE": "DEU", "DK": "DNK",
    "EE": "EST", "ES": "ESP", "EU": "EUR", "FI": "FIN",
    "FR": "FRA", "GB": "GBR", "GR": "GRC", "HR": "HRV",
    "HU": "HUN", "IE": "IRL", "IL": "ISR", "IT": "ITA",
    "JP": "JPN", "LT": "LTU", "LU": "LUX", "LV": "LVA",
    "MX": "MEX", "NL": "NLD", "NO": "NOR", "PL": "POL",
    "PT": "PRT", "RO": "ROU", "RS": "SRB", "SE": "SWE",
    "SI": "SVN", "SK": "SVK", "UA": "UKR", "US": "USA",
    "UY": "URY",
}


def normalize_country(val: str) -> str:
    val = val.strip()
    if not val:
        return ""
    # Already valid 3-letter uppercase
    if len(val) == 3 and val.isalpha() and val.isupper():
        return val
    # 2-letter code
    stripped = val.strip()
    if len(stripped) == 2 and stripped.isalpha():
        iso3 = COUNTRY_ISO2_TO_ISO3.get(stripped.upper())
        if iso3:
            return iso3
    # Full country name (case-insensitive lookup)
    for name, iso3 in COUNTRY_NAME_TO_ISO3.items():
        if val.lower() == name.lower():
            return iso3
    return val  # keep original, will be flagged in validation


def normalize_size(val: str) -> str:
    val = val.strip()
    return SIZE_MAP.get(val, val)


def normalize_discipline(val: str) -> str:
    val = val.strip()
    return DISCIPLINE_MAP.get(val, val)


def normalize_eliminated(val: str) -> str:
    val = val.strip()
    if val in ("True", "False"):
        return val
    if val in ("true", "1", "yes", "DIS", "DISQ", "-", "DNS", "DNF", "NP"):
        return "True"
    if val in ("false", "0", "no"):
        return "False"
    return "False"


def normalize_bool(val: str) -> str:
    val = val.strip()
    if val in ("True", "true", "1", "yes"):
        return "True"
    return "False"


def has_elimination_marker(row: dict) -> bool:
    """Detect DIS/DNF-style markers in result fields."""
    markers = {"DIS", "DISQ", "DQ", "DNF", "DNS", "NP"}
    for col in ("rank", "faults", "refusals", "time_faults", "total_faults", "time", "speed"):
        val = (row.get(col, "") or "").strip().upper()
        if val in markers:
            return True
    return False


# ── Format detection ────────────────────────────────────────────────

def detect_format(header: list[str]) -> str:
    """Detect CSV source format from header columns."""
    if len(header) >= 4 and header[:4] == ["competition", "round_key", "size", "discipline"]:
        return "target"
    if "competition_id" in header and "run_id" in header and "run_description" in header:
        return "kacr"
    if "bid" in header and "run_description" in header and "size_class" in header:
        return "agigames"
    return "unknown"


# ── KACR run_description parsing ────────────────────────────────────

def parse_kacr_description(desc: str) -> dict | None:
    """Parse KACR run_description like 'Agility 1 I' or 'GRAND FINAL L'.

    Returns dict with keys: discipline, size, is_team, number, or None if excluded.
    """
    desc = desc.strip()

    # Exclude warm-up
    if desc.lower().startswith("warm up"):
        return None

    is_team = "team" in desc.lower()

    # Grand Final / GRAND FINAL / SOFT FINAL
    m = re.match(r"(?:GRAND|SOFT)\s+FINAL\s+(.+)", desc, re.IGNORECASE)
    if m:
        size_part = m.group(1).strip()
        return {"discipline": "Final", "size_raw": size_part, "is_team": is_team, "number": None}

    # "Grand Final XS & S" pattern
    m = re.match(r"Grand\s+Final\s+(.+)", desc, re.IGNORECASE)
    if m:
        size_part = m.group(1).strip()
        return {"discipline": "Final", "size_raw": size_part, "is_team": is_team, "number": None}

    # "Final Individual I" / "Final Team Relay L" (prague-agility-party-2025 KACR format)
    m = re.match(r"Final\s+(?:Individual|Team(?:\s+Relay)?)\s+(.+)", desc, re.IGNORECASE)
    if m:
        is_team = "team" in desc.lower()
        size_part = m.group(1).strip()
        return {"discipline": "Final", "size_raw": size_part, "is_team": is_team, "number": None}

    # "Agility/Jumping Individual/Team [size]" (prague-agility-party-2025)
    m = re.match(r"(Agility|Jumping)\s+(Individual|Team)\s+(.+)", desc, re.IGNORECASE)
    if m:
        discipline = m.group(1).capitalize()
        is_team = m.group(2).lower() == "team"
        size_part = m.group(3).strip()
        return {"discipline": discipline, "size_raw": size_part, "is_team": is_team, "number": None}

    # "Agility 1 IA1 & IA2" → discipline=Agility, number=1, size needs extraction
    # "Agility 2 XS & S" → discipline=Agility, number=2, size=XS & S
    # "Jumping 1 L" → discipline=Jumping, number=1, size=L
    m = re.match(r"(Agility|Jumping)\s+(\d+)\s+(.+)", desc, re.IGNORECASE)
    if m:
        discipline = m.group(1).capitalize()
        number = int(m.group(2))
        size_part = m.group(3).strip()
        return {"discipline": discipline, "size_raw": size_part, "is_team": is_team, "number": number}

    return None


def extract_size_from_kacr(size_raw: str) -> str:
    """Extract size category from KACR size_raw string.

    Handles patterns like: 'I', 'L', 'XS & S', 'IA1 & IA2', 'LA3', 'XSA1 & XSA2 & SA1 & SA2'
    """
    s = size_raw.strip()

    # Direct matches
    if s in SIZE_MAP:
        return normalize_size(s)

    # Compound patterns like "IA1 & IA2", "LA3", "MA1 & MA2", "XSA1 & XSA2 & SA1 & SA2"
    # Extract the size letter(s) from the first part
    m = re.match(r"(XS|I|L|M|S)(?:A\d|$|\s)", s)
    if m:
        return normalize_size(m.group(1))

    # "XSA1 & XSA2 & SA1 & SA2" → XS (merged XS and S → Small)
    if s.startswith("XS"):
        return "Small"

    return normalize_size(s)


# ── agigames run_description parsing ────────────────────────────────

def parse_agigames_description(desc: str) -> dict | None:
    """Parse agigames run_description like 'Agility Team M by Přines!'.

    Returns dict with discipline, size_raw, is_team, or None if excluded.
    """
    desc = desc.strip()

    # Remove sponsor suffix "by ..."
    desc_clean = re.sub(r"\s+by\s+.+$", "", desc).strip()

    # Remove quotes that may wrap the field
    desc_clean = desc_clean.strip('"')

    is_team = "team" in desc_clean.lower()

    # "Final Individual/Team Relay [size]"
    m = re.match(r"Final\s+(?:Individual|Team(?:\s+Relay)?)\s+(.+)", desc_clean, re.IGNORECASE)
    if m:
        size_part = m.group(1).strip()
        return {"discipline": "Final", "size_raw": size_part, "is_team": is_team}

    # "Agility/Jumping Individual/Team [size]"
    m = re.match(r"(Agility|Jumping)\s+(?:Individual|Team)\s+(.+)", desc_clean, re.IGNORECASE)
    if m:
        discipline = m.group(1).capitalize()
        size_part = m.group(2).strip()
        return {"discipline": discipline, "size_raw": size_part, "is_team": is_team}

    return None


# ── round_key generation ────────────────────────────────────────────

def make_round_key(is_team: bool, discipline: str, size: str) -> str:
    """Build a round_key base like 'ind_agility_large'."""
    typ = "team" if is_team else "ind"
    disc = discipline.lower()
    sz = size.lower()
    return f"{typ}_{disc}_{sz}"


def normalize_round_key_from_existing(round_key: str, size: str, discipline: str,
                                       is_team: str) -> str:
    """Normalize an existing round_key from target-format files.

    Converts day-prefixed keys (fri_agility_1_in) to standard format (ind_agility_intermediate_1).
    """
    rk = round_key.strip()

    # Already in the standard format: ind_*/team_*
    if rk.startswith("ind_") or rk.startswith("team_"):
        # Re-derive using normalized size/discipline to fix e.g. _in → _intermediate
        parts = rk.split("_")
        is_team_bool = parts[0] == "team"
        disc = normalize_discipline(discipline).lower()
        sz = normalize_size(size).lower()
        base = f"{'team' if is_team_bool else 'ind'}_{disc}_{sz}"

        # Preserve _ind suffix for team individual results
        if is_team_bool and rk.endswith("_ind"):
            return base + "_ind"

        # Preserve trailing number if present
        if parts[-1].isdigit():
            return base + f"_{parts[-1]}"

        return base

    # Day-prefixed format: fri_agility_1_in, sun_final_l, etc.
    # Parse: {day}_{discipline}_{variant}_{size} or {day}_{discipline}_{size}
    parts = rk.split("_")
    if len(parts) >= 3:
        is_team_bool = is_team.strip() == "True"
        disc = normalize_discipline(discipline).lower()
        sz = normalize_size(size).lower()
        typ = "team" if is_team_bool else "ind"

        # Extract number from parts if present (e.g., fri_agility_1_in → number=1)
        number = None
        for p in parts[1:]:
            if p.isdigit():
                number = int(p)
                break
        # Check for day-based names like "friday", "saturday" → number from sequence
        for p in parts[1:]:
            if p in ("friday", "saturday", "sunday", "team"):
                # The day acts as a group identifier; we'll deduplicate later
                pass

        base = f"{typ}_{disc}_{sz}"
        if number:
            return base + f"_{number}"
        return base

    return rk


def assign_round_key_numbers(rows: list[dict]) -> None:
    """Assign sequential numbers to duplicate round_keys within a file."""
    # Count occurrences of each base round_key
    key_counts = Counter()
    for row in rows:
        key_counts[row["round_key"]] += 1

    # For keys appearing in multiple distinct groups, we need to renumber
    # Group rows by round_key and check if they represent different runs
    # (different judge, sct, mct values indicate different runs)
    from itertools import groupby

    # Collect unique runs per base key
    key_runs = {}
    for i, row in enumerate(rows):
        rk = row["round_key"]
        # Use (judge, sct, mct) as a run signature
        sig = (row.get("judge", ""), row.get("sct", ""), row.get("mct", ""))
        if rk not in key_runs:
            key_runs[rk] = []
        if sig not in [s for s, _ in key_runs[rk]]:
            key_runs[rk].append((sig, []))
        # Find the matching run and add row index
        for s, indices in key_runs[rk]:
            if s == sig:
                indices.append(i)
                break

    # Renumber keys with multiple runs
    for rk, runs in key_runs.items():
        if len(runs) <= 1:
            continue
        for num, (sig, indices) in enumerate(runs, 1):
            # Check if the key already has a number suffix
            m = re.match(r"(.+)_(\d+)$", rk)
            if m:
                base = m.group(1)
                new_key = f"{base}_{num}"
            else:
                new_key = f"{rk}_{num}"
            for idx in indices:
                rows[idx]["round_key"] = new_key


# ── Format-specific transformers ────────────────────────────────────

def transform_kacr(rows: list[dict], competition: str) -> list[dict]:
    """Transform KACR-format rows to target format."""
    result = []
    for row in rows:
        desc = row.get("run_description", "")
        parsed = parse_kacr_description(desc)
        if parsed is None:
            continue  # skip Warm Up etc.

        discipline = normalize_discipline(parsed["discipline"])
        if discipline in EXCLUDE_DISCIPLINES:
            continue

        size = extract_size_from_kacr(parsed.get("size_raw", ""))
        is_team = parsed["is_team"]

        # Determine elimination
        status = row.get("status", "").strip()
        eliminated = "True" if status == "DIS" else "False"
        is_elim = eliminated == "True"

        out = {
            "competition": competition,
            "round_key": "",  # assigned later
            "size": size,
            "discipline": discipline,
            "is_team_round": "True" if is_team else "False",
            "rank": "" if is_elim else row.get("rank", ""),
            "start_no": row.get("prukaz", ""),
            "handler": row.get("handler", ""),
            "dog": row.get("dog", ""),
            "breed": "",
            "country": "",  # KACR doesn't have country
            "faults": "" if is_elim else row.get("chb", ""),
            "refusals": "" if is_elim else row.get("odm", ""),
            "time_faults": "" if is_elim else row.get("tb_time", ""),
            "total_faults": "" if is_elim else row.get("tb_total", ""),
            "time": "" if is_elim else row.get("time", ""),
            "speed": "" if is_elim else row.get("speed", ""),
            "eliminated": eliminated,
            "judge": row.get("judge", ""),
            "sct": row.get("standard_time", ""),
            "mct": row.get("max_time", ""),
            "course_length": row.get("course_length", ""),
        }

        # Generate round_key base
        rk_base = make_round_key(is_team, discipline, size)
        if is_team:
            rk_base += "_ind"
        out["round_key"] = rk_base

        result.append(out)

    assign_round_key_numbers(result)
    return result


def _is_junk_row(row: dict) -> bool:
    """Detect non-result rows (course walking markers, etc.)."""
    handler = row.get("handler", "")
    if handler.startswith("[") or "Coursewalking" in handler or "Prohlídka" in handler:
        return True
    return False


def transform_agigames(rows: list[dict], competition: str) -> list[dict]:
    """Transform agigames-format rows to target format."""
    result = []
    for row in rows:
        if _is_junk_row(row):
            continue
        desc = row.get("run_description", "")
        parsed = parse_agigames_description(desc)
        if parsed is None:
            continue

        discipline = normalize_discipline(parsed["discipline"])
        if discipline in EXCLUDE_DISCIPLINES:
            continue

        size_raw = parsed.get("size_raw", "")
        size = normalize_size(size_raw) if size_raw in SIZE_MAP else ""
        # Fallback to size_class column
        if not size or size == size_raw:
            sc = row.get("size_class", "").strip()
            # size_class like "M A3" → extract first letter(s)
            sc_match = re.match(r"(XS|S|M|I|L)", sc)
            if sc_match:
                size = normalize_size(sc_match.group(1))
            elif size_raw:
                size = extract_size_from_kacr(size_raw)

        is_team = parsed["is_team"]

        # Determine elimination
        status = row.get("status", "").strip()
        eliminated = "True" if status in ("DIS", "-") else "False"
        is_elim = eliminated == "True"

        country = normalize_country(row.get("country", ""))

        faults_str = "" if is_elim else row.get("chb", "")
        refusals_str = "" if is_elim else row.get("odm", "")
        total_faults_str = "" if is_elim else row.get("tb_total", "")

        # Compute time_faults if not available
        time_faults = ""
        if not is_elim and total_faults_str and faults_str and refusals_str:
            try:
                tf = float(total_faults_str) - int(faults_str) * 5 - int(refusals_str) * 5
                time_faults = str(round(tf, 2)) if tf != int(tf) else str(int(tf))
                if tf < 0:
                    time_faults = "0"
            except (ValueError, TypeError):
                time_faults = ""

        out = {
            "competition": competition,
            "round_key": "",
            "size": size,
            "discipline": discipline,
            "is_team_round": "True" if is_team else "False",
            "rank": "" if is_elim else row.get("rank", ""),
            "start_no": row.get("start_num", ""),
            "handler": row.get("handler", ""),
            "dog": row.get("dog", ""),
            "breed": "",
            "country": country,
            "faults": faults_str,
            "refusals": refusals_str,
            "time_faults": time_faults,
            "total_faults": total_faults_str,
            "time": "" if is_elim else row.get("time", ""),
            "speed": "" if is_elim else row.get("speed", ""),
            "eliminated": eliminated,
            "judge": row.get("judge", ""),
            "sct": row.get("standard_time", ""),
            "mct": row.get("max_time", ""),
            "course_length": row.get("course_length", ""),
        }

        rk_base = make_round_key(is_team, discipline, size)
        if is_team:
            rk_base += "_ind"
        out["round_key"] = rk_base

        result.append(out)

    assign_round_key_numbers(result)
    return result


def normalize_target_rows(rows: list[dict]) -> list[dict]:
    """Normalize values in already-target-format rows."""
    result = []
    for row in rows:
        size = normalize_size(row.get("size", ""))
        discipline = normalize_discipline(row.get("discipline", ""))
        country = normalize_country(row.get("country", ""))
        eliminated = normalize_eliminated(row.get("eliminated", ""))
        is_team = normalize_bool(row.get("is_team_round", ""))

        # Detect eliminated from empty rank + empty time (source marked False incorrectly)
        rank_val = row.get("rank", "").strip()
        time_val = row.get("time", "").strip()
        if not rank_val and not time_val:
            eliminated = "True"

        # Some sources encode eliminations as DIS tokens in numeric columns.
        if has_elimination_marker(row):
            eliminated = "True"

        is_elim = eliminated == "True"

        out = {col: row.get(col, "") for col in TARGET_COLUMNS}
        out["size"] = size
        out["discipline"] = discipline
        out["country"] = country
        out["eliminated"] = eliminated
        out["is_team_round"] = is_team

        # Clear fields for eliminated rows
        if is_elim:
            for col in ("rank", "faults", "refusals", "time_faults", "total_faults", "time", "speed"):
                out[col] = ""

        # Normalize round_key
        out["round_key"] = normalize_round_key_from_existing(
            row.get("round_key", ""), size, discipline, is_team
        )

        result.append(out)

    assign_round_key_numbers(result)
    return result


# ── Sorting ─────────────────────────────────────────────────────────

def sort_rows(rows: list[dict]) -> list[dict]:
    """Sort by round_key, then rank (non-eliminated first, eliminated last)."""
    def sort_key(row):
        rk = row.get("round_key", "")
        elim = row.get("eliminated", "False") == "True"
        rank_str = row.get("rank", "")
        try:
            rank = int(rank_str)
        except (ValueError, TypeError):
            rank = 99999
        return (rk, elim, rank)

    return sorted(rows, key=sort_key)


# ── Validation ──────────────────────────────────────────────────────

VALID_SIZES = {"Small", "Medium", "Intermediate", "Large"}
VALID_DISCIPLINES = {"Jumping", "Agility", "Final"}

def validate_rows(rows: list[dict], filepath: str) -> list[str]:
    """Validate normalized rows. Returns list of issues."""
    issues = []
    for i, row in enumerate(rows, 1):
        line = f"{filepath}:{i}"

        # Check required columns
        for col in TARGET_COLUMNS:
            if col not in row:
                issues.append(f"{line}: missing column '{col}'")

        size = row.get("size", "")
        if size and size not in VALID_SIZES:
            issues.append(f"{line}: invalid size '{size}'")

        discipline = row.get("discipline", "")
        if discipline and discipline not in VALID_DISCIPLINES:
            issues.append(f"{line}: invalid discipline '{discipline}'")

        country = row.get("country", "")
        if country and (len(country) != 3 or not country.isupper() or not country.isalpha()):
            issues.append(f"{line}: invalid country '{country}'")

        elim = row.get("eliminated", "")
        if elim not in ("True", "False"):
            issues.append(f"{line}: invalid eliminated '{elim}'")

        is_team = row.get("is_team_round", "")
        if is_team not in ("True", "False"):
            issues.append(f"{line}: invalid is_team_round '{is_team}'")

        is_elim = elim == "True"
        if is_elim:
            for col in ("rank", "faults", "refusals", "time_faults", "total_faults", "time", "speed"):
                if row.get(col, "").strip():
                    issues.append(f"{line}: eliminated row has non-empty {col}='{row[col]}'")
        else:
            if not row.get("time", "").strip() and not row.get("rank", "").strip():
                # Some rows may legitimately lack time (DNS etc.)
                pass

    return issues


# ── CSV writing ─────────────────────────────────────────────────────

def rows_to_csv_string(rows: list[dict]) -> str:
    """Convert rows to CSV string with target column order."""
    output = io.StringIO()
    writer = csv.DictWriter(output, fieldnames=TARGET_COLUMNS, lineterminator="\n",
                            extrasaction="ignore")
    writer.writeheader()
    for row in rows:
        writer.writerow(row)
    return output.getvalue()


# ── Main processing ─────────────────────────────────────────────────

def find_csv_files() -> list[Path]:
    """Find all CSV result files under data/."""
    files = []
    for d in sorted(DATA_DIR.iterdir()):
        if not d.is_dir() or d.name.startswith("_"):
            continue
        for f in sorted(d.glob("*.csv")):
            files.append(f)
    return files


def determine_output_path(csv_path: Path) -> Path:
    """Determine output path, renaming results.csv to <folder>_results.csv."""
    if csv_path.name == "results.csv":
        folder_name = csv_path.parent.name
        return csv_path.parent / f"{folder_name}_results.csv"
    return csv_path


def process_file(csv_path: Path, dry_run: bool = False) -> dict:
    """Process a single CSV file. Returns stats dict."""
    stats = {"path": str(csv_path), "format": "unknown", "rows_in": 0,
             "rows_out": 0, "issues": [], "skipped_warmup": 0}

    with open(csv_path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        header = reader.fieldnames or []
        fmt = detect_format(header)
        stats["format"] = fmt

        rows = list(reader)
        stats["rows_in"] = len(rows)

    if fmt == "unknown":
        stats["issues"].append(f"Unknown format, skipping: {header[:5]}")
        return stats

    # Extract competition name from first row
    competition = rows[0].get("competition", "") if rows else ""

    if fmt == "kacr":
        normalized = transform_kacr(rows, competition)
    elif fmt == "agigames":
        normalized = transform_agigames(rows, competition)
    elif fmt == "target":
        normalized = normalize_target_rows(rows)
    else:
        return stats

    normalized = sort_rows(normalized)
    stats["rows_out"] = len(normalized)
    stats["skipped_warmup"] = stats["rows_in"] - len(normalized)

    # Validate
    issues = validate_rows(normalized, str(csv_path))
    stats["issues"] = issues

    # Determine output path
    out_path = determine_output_path(csv_path)
    stats["output_path"] = str(out_path)

    csv_content = rows_to_csv_string(normalized)

    if not dry_run:
        with open(out_path, "w", newline="", encoding="utf-8") as f:
            f.write(csv_content)
        # Remove old file if renamed
        if out_path != csv_path and csv_path.exists():
            csv_path.unlink()

    return stats


def main():
    parser = argparse.ArgumentParser(description="Normalize CSV results to target format")
    parser.add_argument("--dry-run", action="store_true", help="Preview without writing")
    args = parser.parse_args()

    csv_files = find_csv_files()
    print(f"Found {len(csv_files)} CSV files\n")

    total_issues = 0
    for csv_path in csv_files:
        stats = process_file(csv_path, dry_run=args.dry_run)
        fmt = stats["format"]
        rows_in = stats["rows_in"]
        rows_out = stats["rows_out"]
        issues = stats["issues"]
        out_path = stats.get("output_path", str(csv_path))
        skipped = stats.get("skipped_warmup", 0)

        status_icon = "OK" if not issues else f"{len(issues)} issues"
        rename_note = ""
        if out_path != str(csv_path):
            rename_note = f" → {Path(out_path).name}"

        print(f"  {csv_path.parent.name}/{csv_path.name}{rename_note}")
        print(f"    Format: {fmt} | Rows: {rows_in} → {rows_out}"
              f"{f' (skipped {skipped})' if skipped > 0 else ''} | {status_icon}")

        if issues:
            for issue in issues[:5]:
                print(f"    ⚠ {issue}")
            if len(issues) > 5:
                print(f"    ... and {len(issues) - 5} more issues")
            total_issues += len(issues)

    print(f"\n{'DRY RUN - no files modified' if args.dry_run else 'Done.'}")
    if total_issues:
        print(f"Total issues: {total_issues}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
