#!/usr/bin/env python3
"""
First dry-run rating calculation.

Reads all normalized CSV results from data/*, calculates OpenSkill (Plackett-Luce)
ratings per size category, and outputs:
  - output/ratings.html  (interactive static page)
  - output/ratings.csv   (flat export)
"""

import csv
import glob
import os
import re
import unicodedata
from collections import defaultdict
from math import sqrt

from openskill.models import PlackettLuce

# ---------------------------------------------------------------------------
# Competition registry
# ---------------------------------------------------------------------------

COMPETITIONS = {
    "polish_open_2024_inl":       {"date": "2024-02-09", "tier": 2, "name": "Polish Open 2024 (IN & L)"},
    "polish_open_2024_xsm":       {"date": "2024-02-09", "tier": 2, "name": "Polish Open 2024 (XS, S & M)"},
    "croatian_open_2024":          {"date": "2024-06-21", "tier": 2, "name": "Croatian Open 2024"},
    "slovenian_open_2024":         {"date": "2024-06-28", "tier": 2, "name": "Slovenian Open 2024"},
    "moravia-open-2024":           {"date": "2024-07-05", "tier": 2, "name": "Moravia Open 2024"},
    "joawc_soawc_2024":            {"date": "2024-07-18", "tier": 1, "name": "JOAWC/SOAWC 2024"},
    "prague-agility-party-2024":   {"date": "2024-07-19", "tier": 2, "name": "Prague Agility Party 2024"},
    "eo2024":                      {"date": "2024-08-01", "tier": 1, "name": "EO 2024"},
    "awc2024":                     {"date": "2024-10-01", "tier": 1, "name": "AWC 2024"},
    "polish_open_soft_2024_inl":   {"date": "2024-11-09", "tier": 2, "name": "Polish Open SOFT 2024 (IN & L)"},
    "polish_open_soft_2024_xsm":   {"date": "2024-11-09", "tier": 2, "name": "Polish Open SOFT 2024 (XS, S & M)"},
    "moravia-open-2025":           {"date": "2025-07-04", "tier": 2, "name": "Moravia Open 2025"},
    "eo2025":                      {"date": "2025-07-16", "tier": 1, "name": "EO 2025"},
    "prague-agility-party-2025":   {"date": "2025-08-08", "tier": 2, "name": "Prague Agility Party 2025"},
    "awc2025":                     {"date": "2025-09-17", "tier": 1, "name": "AWC 2025"},
    "polish_open_soft_2025_inl":   {"date": "2025-11-07", "tier": 2, "name": "Polish Open SOFT 2025 (IN & L)"},
    "polish_open_soft_2025_xsm":   {"date": "2025-11-07", "tier": 2, "name": "Polish Open SOFT 2025 (XS, S & M)"},
    "polish_open_2025_inl":        {"date": "2025-02-07", "tier": 2, "name": "Polish Open 2025 (IN & L)"},
    "polish_open_2025_xsm":       {"date": "2025-02-07", "tier": 2, "name": "Polish Open 2025 (XS, S & M)"},
    "polish_open_2026_inl":        {"date": "2026-02-06", "tier": 2, "name": "Polish Open 2026 (IN & L)"},
    "polish_open_2026_xsm":       {"date": "2026-02-06", "tier": 2, "name": "Polish Open 2026 (XS, S & M)"},
}

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA_DIR = os.path.join(BASE_DIR, "data")
OUTPUT_DIR = os.path.join(BASE_DIR, "output")

MIN_FIELD_SIZE = 6

# Display formula: displayed_rating = 1000 + DISPLAY_SCALE * (mu - 3 * sigma)
# NOTE: PlackettLuce sigma barely converges in large fields (50+ competitors),
# so displayed_rating is effectively 1000 + DISPLAY_SCALE * (mu - 25).
# Using scale=100 to get a useful spread for the dry run.
# This is a known issue — sigma convergence needs investigation for production.
DISPLAY_BASE = 1000
DISPLAY_SCALE = 100


def displayed_rating(mu, sigma):
    return DISPLAY_BASE + DISPLAY_SCALE * (mu - 3 * sigma)


def displayed_deviation(sigma):
    return DISPLAY_SCALE * 3 * sigma


def tier_label(rating):
    if rating >= 2500:
        return "Elite"
    if rating >= 2200:
        return "Champion"
    if rating >= 1900:
        return "Expert"
    if rating >= 1600:
        return "Competitor"
    return "Provisional"


# ---------------------------------------------------------------------------
# Name normalization
# ---------------------------------------------------------------------------

# Manual dog name aliases: normalized_call_name -> canonical_call_name
DOG_ALIASES = {
    "sayonara": "seeya",
}


def strip_diacritics(s):
    """Remove diacritics: 'Diviš' -> 'Divis', 'Glejdurová' -> 'Glejdurova'."""
    nfkd = unicodedata.normalize("NFKD", s)
    return "".join(c for c in nfkd if not unicodedata.combining(c))


def normalize_handler(name):
    """Normalize handler name to canonical 'first last' form.

    Handles:
      'Last, First'  -> 'first last'
      'First Last'   -> sorted('first', 'last') to match regardless of order
      Diacritics stripped, lowercased.
    """
    name = strip_diacritics(name).strip().lower()
    # Convert "Last, First" to "first last"
    if "," in name:
        parts = name.split(",", 1)
        name = f"{parts[1].strip()} {parts[0].strip()}"
    # Collapse multiple spaces
    name = re.sub(r"\s+", " ", name)
    # Sort name parts so "jakub divis" == "divis jakub"
    parts = name.split()
    return " ".join(sorted(parts))


def extract_call_name(dog_name):
    """Extract call name from dog's registered name.

    Handles:
      'Shepworld I\\'m On Fire (Brant)' -> 'brant'
      'Finrod Frances "Cis"'            -> 'cis'
      'A3Ch Finrod Frances ...'         -> 'a3ch finrod frances ...' (no call name found)
      'Day'                             -> 'day'
    Diacritics stripped, lowercased.
    """
    dog_name = strip_diacritics(dog_name).strip()
    # Try to extract from parentheses at end: "... (CallName)" -> "CallName"
    match = re.search(r"\(([^)]+)\)\s*$", dog_name)
    if match:
        return match.group(1).strip().lower()
    # Try to extract from quotes: '... "CallName"' -> "CallName"
    match = re.search(r'"([^"]+)"\s*$', dog_name)
    if match:
        result = match.group(1).strip().lower()
        return DOG_ALIASES.get(result, result)
    result = dog_name.lower()
    return DOG_ALIASES.get(result, result)


def make_team_id(handler, dog):
    """Create a normalized team identity from handler and dog names."""
    h = normalize_handler(handler)
    d = extract_call_name(dog) if dog else ""
    return f"{h}|||{d}"


# ---------------------------------------------------------------------------
# Data loading
# ---------------------------------------------------------------------------

def load_all_runs():
    """Load all CSV files, skip team rounds, attach competition metadata."""
    runs = []
    csv_files = sorted(glob.glob(os.path.join(DATA_DIR, "*", "*_results.csv")))
    skipped_no_identity = 0

    for filepath in csv_files:
        comp_dir = os.path.basename(os.path.dirname(filepath))
        if comp_dir == "_downloads":
            continue
        if comp_dir not in COMPETITIONS:
            print(f"WARNING: unknown competition dir '{comp_dir}', skipping")
            continue

        comp_meta = COMPETITIONS[comp_dir]

        with open(filepath, newline="", encoding="utf-8") as f:
            reader = csv.DictReader(f)
            for row in reader:
                if row.get("is_team_round", "") == "True":
                    continue

                handler = row.get("handler", "").strip()
                dog = row.get("dog", "").strip()

                # AWC data sometimes has handler+dog concatenated in handler field
                # with empty dog. Try to split "Handler DogRegisteredName (CallName)"
                if not dog and handler:
                    # Try to extract call name from parentheses in handler field
                    paren_match = re.search(r"\(([^)]+)\)\s*$", handler)
                    if paren_match:
                        call_name = paren_match.group(1).strip()
                        # Use call name as dog, but handler stays as-is for display
                        team_id = make_team_id(handler, call_name)
                    else:
                        # No way to split — use full handler as team_id
                        skipped_no_identity += 1
                        continue
                else:
                    team_id = make_team_id(handler, dog)

                # Parse rank
                try:
                    rank = int(row["rank"])
                except (ValueError, KeyError):
                    rank = None

                eliminated = row.get("eliminated", "") == "True"

                runs.append({
                    "comp_dir": comp_dir,
                    "comp_name": comp_meta["name"],
                    "comp_date": comp_meta["date"],
                    "comp_tier": comp_meta["tier"],
                    "round_key": row.get("round_key", ""),
                    "size": row.get("size", ""),
                    "team_id": team_id,
                    "handler": handler,
                    "dog": dog,
                    "country": row.get("country", ""),
                    "rank": rank,
                    "eliminated": eliminated,
                })

    if skipped_no_identity:
        print(f"Skipped {skipped_no_identity} runs with no parseable team identity")
    print(f"Loaded {len(runs)} individual runs from {len(csv_files)} files")

    # --- Fuzzy dog name merging ---
    # For each handler, find dog name variants that should be the same dog.
    # E.g., "day" and "daylight neverending force" for the same handler.
    runs = _merge_dog_variants(runs)

    return runs


def _merge_dog_variants(runs):
    """Merge team_ids where the same handler has a short call name that is
    a prefix of (or contained in) a longer registered name.

    Strategy: for each normalized handler, collect all dog name variants.
    If a short name (<=2 words) appears as a starting word in a longer name,
    merge the longer name's team_id to the short name's team_id.
    """
    # Collect handler -> set of dog parts from team_ids
    handler_dogs = defaultdict(set)
    for run in runs:
        h, d = run["team_id"].split("|||", 1)
        handler_dogs[h].add(d)

    # Build merge map: long_team_id -> short_team_id
    merge_map = {}
    for handler, dogs in handler_dogs.items():
        dogs = list(dogs)
        if len(dogs) < 2:
            continue
        # Sort by word count (short names first)
        dogs.sort(key=lambda d: len(d.split()))
        for i, short in enumerate(dogs):
            short_words = short.split()
            if len(short_words) > 2 or not short:
                continue
            for long in dogs[i + 1:]:
                long_words = long.split()
                if len(long_words) <= len(short_words):
                    continue
                # Check if short name's first word starts the long name
                if long_words[0].startswith(short_words[0]):
                    long_tid = f"{handler}|||{long}"
                    short_tid = f"{handler}|||{short}"
                    if long_tid not in merge_map:
                        merge_map[long_tid] = short_tid

    if merge_map:
        merged_count = 0
        for run in runs:
            if run["team_id"] in merge_map:
                run["team_id"] = merge_map[run["team_id"]]
                merged_count += 1
        print(f"Merged {merged_count} runs across {len(merge_map)} dog name variants")

    return runs


# ---------------------------------------------------------------------------
# Team profile aggregation
# ---------------------------------------------------------------------------

def build_team_profiles(runs):
    """Aggregate best metadata for each team across all runs.

    Returns dict: team_id -> {
        handler_display: str,   # canonical "First Last"
        dog_display: str,       # "Registered Name (CallName)" or just call name
        call_name: str,
        country: str,
    }
    """
    # Collect raw data per team_id
    raw = defaultdict(lambda: {
        "handlers": [],       # all raw handler strings
        "dogs": [],           # all raw dog strings
        "countries": [],      # all raw country strings
    })

    for run in runs:
        tid = run["team_id"]
        raw[tid]["handlers"].append(run["handler"])
        raw[tid]["dogs"].append(run["dog"])
        raw[tid]["countries"].append(run["country"])

    profiles = {}
    for tid, data in raw.items():
        profiles[tid] = {
            "handler_display": _best_handler_display(data["handlers"]),
            "dog_display": _best_dog_display(data["dogs"], tid),
            "country": _best_country(data["countries"]),
        }

    # Country backfill: use normalized handler to share country across team_ids
    # (same handler with different dogs should have same country)
    handler_country = {}
    for tid, profile in profiles.items():
        h = tid.split("|||")[0]
        if profile["country"]:
            handler_country[h] = profile["country"]

    backfilled = 0
    for tid, profile in profiles.items():
        if not profile["country"]:
            h = tid.split("|||")[0]
            if h in handler_country:
                profile["country"] = handler_country[h]
                backfilled += 1

    stats_no_country = sum(1 for p in profiles.values() if not p["country"])
    print(f"Team profiles: {len(profiles)} teams, "
          f"backfilled {backfilled} countries, "
          f"{stats_no_country} still missing country")

    return profiles


def _best_handler_display(handlers):
    """Pick the best handler display name from all variants.

    Priority:
    1. "Last, First" format → convert to "First Last" (reliable first/last split)
    2. Most frequent "First Last" string, preferring version with diacritics
    """
    # Try to find a comma-separated variant — prefer one with diacritics
    comma_variants = []
    for h in handlers:
        h = h.strip()
        if "," in h and h:
            comma_variants.append(h)

    if comma_variants:
        # Prefer variant with diacritics (non-ASCII chars = more original)
        comma_variants.sort(key=lambda h: sum(1 for c in h if ord(c) > 127), reverse=True)
        parts = comma_variants[0].split(",", 1)
        last = parts[0].strip()
        first = parts[1].strip()
        return f"{first} {last}"

    # No comma variant — pick the most common non-empty name
    counts = defaultdict(int)
    for h in handlers:
        h = h.strip()
        if h:
            counts[h] += 1

    if not counts:
        return ""

    # Among top candidates, prefer the one with diacritics
    max_count = max(counts.values())
    top_candidates = [h for h, c in counts.items() if c == max_count]
    top_candidates.sort(key=lambda h: sum(1 for c in h if ord(c) > 127), reverse=True)
    return top_candidates[0]


def _best_dog_display(dogs, team_id):
    """Build dog display name: "Registered Name (CallName)" or just call name.

    Collects the longest variant as registered name and the call name from team_id.
    """
    call_name = team_id.split("|||")[1] if "|||" in team_id else ""

    # Collect non-empty dog strings
    non_empty = [d.strip() for d in dogs if d.strip()]
    if not non_empty:
        return call_name.title() if call_name else ""

    # Find longest dog name (likely the full registered name)
    longest = max(non_empty, key=len)
    # Normalize: strip parenthesized call name and quotes from the end
    registered = re.sub(r'\s*\([^)]+\)\s*$', '', longest).strip()
    registered = re.sub(r'\s*"[^"]+"\s*$', '', registered).strip()

    # If registered name is just the call name, show only call name
    if registered.lower() == call_name or len(registered.split()) <= 1:
        return call_name.title() if call_name else registered

    # Show "Registered Name (CallName)"
    if call_name:
        return f"{registered} ({call_name.title()})"
    return registered


def _best_country(countries):
    """Pick the most common non-empty country."""
    counts = defaultdict(int)
    for c in countries:
        c = c.strip()
        if c:
            counts[c] += 1
    if not counts:
        return ""
    return max(counts, key=counts.get)


# ---------------------------------------------------------------------------
# Rating calculation
# ---------------------------------------------------------------------------

def calculate_ratings(runs, profiles):
    """
    Calculate OpenSkill ratings per size category.

    Returns dict: size -> {team_id: {mu, sigma, handler, dog, country, num_runs, last_comp}}
    """
    # Group runs by size
    by_size = defaultdict(list)
    for run in runs:
        by_size[run["size"]].append(run)

    all_ratings = {}

    for size in sorted(by_size.keys()):
        size_runs = by_size[size]
        print(f"\n--- {size} ({len(size_runs)} runs) ---")

        model = PlackettLuce()

        # team_id -> PlackettLuceRating
        team_ratings = {}
        # team_id -> {num_runs, last_comp, last_comp_date}
        team_stats = {}

        # Group by competition (chronological)
        comp_runs = defaultdict(list)
        for run in size_runs:
            comp_runs[run["comp_dir"]].append(run)

        # Sort competitions chronologically
        sorted_comps = sorted(comp_runs.keys(), key=lambda c: COMPETITIONS[c]["date"])

        for comp_dir in sorted_comps:
            comp_name = COMPETITIONS[comp_dir]["name"]

            # Group by round
            round_runs = defaultdict(list)
            for run in comp_runs[comp_dir]:
                round_runs[run["round_key"]].append(run)

            for round_key in sorted(round_runs.keys()):
                entries = round_runs[round_key]

                # Deduplicate by team_id (keep first occurrence)
                seen = set()
                unique_entries = []
                for e in entries:
                    if e["team_id"] not in seen:
                        seen.add(e["team_id"])
                        unique_entries.append(e)
                entries = unique_entries

                if len(entries) < MIN_FIELD_SIZE:
                    continue

                # Build ranked list
                # Non-eliminated: use their rank
                # Eliminated: shared last place
                clean = [e for e in entries if not e["eliminated"] and e["rank"] is not None]
                elim = [e for e in entries if e["eliminated"]]

                # Sort clean by rank
                clean.sort(key=lambda e: e["rank"])

                # Assign OpenSkill ranks (1-indexed)
                ranked_entries = []
                for i, entry in enumerate(clean):
                    ranked_entries.append((entry, i + 1))

                # Eliminated share last rank
                last_rank = len(clean) + 1
                for entry in elim:
                    ranked_entries.append((entry, last_rank))

                if len(ranked_entries) < MIN_FIELD_SIZE:
                    continue

                # Prepare teams and ranks for openskill
                teams = []
                ranks = []
                entry_order = []

                for entry, rank in ranked_entries:
                    tid = entry["team_id"]
                    if tid not in team_ratings:
                        team_ratings[tid] = model.rating()
                        team_stats[tid] = {
                            "num_runs": 0,
                            "last_comp": "",
                            "last_comp_date": "",
                        }
                    teams.append([team_ratings[tid]])
                    ranks.append(rank)
                    entry_order.append(tid)

                    # Update stats
                    team_stats[tid]["num_runs"] += 1
                    if entry["comp_date"] >= team_stats[tid]["last_comp_date"]:
                        team_stats[tid]["last_comp"] = comp_name
                        team_stats[tid]["last_comp_date"] = entry["comp_date"]

                # Rate!
                result = model.rate(teams, ranks=ranks)

                # Update ratings
                for i, tid in enumerate(entry_order):
                    team_ratings[tid] = result[i][0]

            # end round loop
        # end competition loop

        # Build final results for this size, using profiles for display metadata
        size_results = {}
        for tid, rating in team_ratings.items():
            stats = team_stats[tid]
            profile = profiles.get(tid, {})
            size_results[tid] = {
                "mu": rating.mu,
                "sigma": rating.sigma,
                "displayed_rating": round(displayed_rating(rating.mu, rating.sigma), 1),
                "deviation": round(displayed_deviation(rating.sigma), 1),
                "handler": profile.get("handler_display", ""),
                "dog": profile.get("dog_display", ""),
                "country": profile.get("country", ""),
                "num_runs": stats["num_runs"],
                "last_comp": stats["last_comp"],
            }

        # Sort by displayed rating descending
        sorted_teams = sorted(size_results.items(), key=lambda x: -x[1]["displayed_rating"])
        print(f"  {len(sorted_teams)} unique teams rated")
        if sorted_teams:
            top = sorted_teams[0][1]
            print(f"  Top: {top['handler']} / {top['dog']} ({top['country']}) — {top['displayed_rating']}")

        all_ratings[size] = size_results

    return all_ratings


# ---------------------------------------------------------------------------
# Output: CSV
# ---------------------------------------------------------------------------

def write_csv(all_ratings):
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(OUTPUT_DIR, "ratings.csv")

    with open(outpath, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow([
            "rank", "handler", "dog", "size", "country",
            "mu", "sigma", "displayed_rating", "deviation",
            "tier", "num_runs", "last_competition",
        ])

        for size in sorted(all_ratings.keys()):
            sorted_teams = sorted(
                all_ratings[size].values(),
                key=lambda x: -x["displayed_rating"],
            )
            for i, team in enumerate(sorted_teams, 1):
                writer.writerow([
                    i,
                    team["handler"],
                    team["dog"],
                    size,
                    team["country"],
                    round(team["mu"], 4),
                    round(team["sigma"], 4),
                    team["displayed_rating"],
                    team["deviation"],
                    tier_label(team["displayed_rating"]),
                    team["num_runs"],
                    team["last_comp"],
                ])

    print(f"\nCSV written to {outpath}")


# ---------------------------------------------------------------------------
# Output: HTML
# ---------------------------------------------------------------------------

def write_html(all_ratings):
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(OUTPUT_DIR, "ratings.html")

    sizes = sorted(all_ratings.keys())

    # Build table rows per size
    tables = {}
    for size in sizes:
        sorted_teams = sorted(
            all_ratings[size].values(),
            key=lambda x: -x["displayed_rating"],
        )
        rows = []
        for i, team in enumerate(sorted_teams, 1):
            rating = team["displayed_rating"]
            tl = tier_label(rating)
            rows.append(
                f"<tr class='tier-{tl.lower()}'>"
                f"<td>{i}</td>"
                f"<td>{_esc(team['handler'])}</td>"
                f"<td>{_esc(team['dog'])}</td>"
                f"<td>{_esc(team['country'])}</td>"
                f"<td class='num'>{rating:.0f}</td>"
                f"<td class='num'>±{team['deviation']:.0f}</td>"
                f"<td class='num'>{team['num_runs']}</td>"
                f"<td>{_esc(team['last_comp'])}</td>"
                f"</tr>"
            )
        tables[size] = "\n".join(rows)

    # Tab buttons
    tab_buttons = []
    for i, size in enumerate(sizes):
        count = len(all_ratings[size])
        active = " active" if i == 0 else ""
        tab_buttons.append(
            f'<button class="tab-btn{active}" onclick="showTab(\'{size}\')">'
            f'{size} <span class="count">({count})</span></button>'
        )

    # Tab content
    tab_contents = []
    for i, size in enumerate(sizes):
        display = "block" if i == 0 else "none"
        tab_contents.append(f"""
        <div id="tab-{size}" class="tab-content" style="display:{display}">
            <table class="rating-table" id="table-{size}">
                <thead>
                    <tr>
                        <th onclick="sortTable('table-{size}', 0, 'num')">#</th>
                        <th onclick="sortTable('table-{size}', 1, 'str')">Handler</th>
                        <th onclick="sortTable('table-{size}', 2, 'str')">Dog</th>
                        <th onclick="sortTable('table-{size}', 3, 'str')">Country</th>
                        <th onclick="sortTable('table-{size}', 4, 'num')">Rating</th>
                        <th onclick="sortTable('table-{size}', 5, 'num')">±Dev</th>
                        <th onclick="sortTable('table-{size}', 6, 'num')">Runs</th>
                        <th onclick="sortTable('table-{size}', 7, 'str')">Last Competition</th>
                    </tr>
                </thead>
                <tbody>
                    {tables[size]}
                </tbody>
            </table>
        </div>""")

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>ADW Rating — Dry Run</title>
<style>
* {{ margin: 0; padding: 0; box-sizing: border-box; }}
body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; padding: 20px; }}
h1 {{ margin-bottom: 4px; }}
.subtitle {{ color: #666; margin-bottom: 20px; font-size: 14px; }}
.tabs {{ display: flex; gap: 8px; margin-bottom: 16px; flex-wrap: wrap; }}
.tab-btn {{ padding: 8px 16px; border: 1px solid #ddd; background: #fff; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
.tab-btn.active {{ background: #2563eb; color: #fff; border-color: #2563eb; }}
.tab-btn .count {{ font-weight: 400; opacity: 0.8; }}
.rating-table {{ width: 100%; border-collapse: collapse; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }}
.rating-table th {{ background: #f8f9fa; padding: 10px 12px; text-align: left; font-size: 13px; color: #555; cursor: pointer; user-select: none; border-bottom: 2px solid #e5e7eb; }}
.rating-table th:hover {{ background: #e5e7eb; }}
.rating-table td {{ padding: 8px 12px; border-bottom: 1px solid #f0f0f0; font-size: 14px; }}
.rating-table td.num {{ text-align: right; font-variant-numeric: tabular-nums; }}
.rating-table tbody tr:hover {{ background: #f8faff; }}
.tier-elite td:first-child {{ border-left: 3px solid #f59e0b; }}
.tier-champion td:first-child {{ border-left: 3px solid #8b5cf6; }}
.tier-expert td:first-child {{ border-left: 3px solid #3b82f6; }}
.tier-competitor td:first-child {{ border-left: 3px solid #10b981; }}
.tier-provisional td:first-child {{ border-left: 3px solid #d1d5db; }}
.tier-provisional {{ color: #999; }}
.legend {{ display: flex; gap: 16px; margin-bottom: 16px; flex-wrap: wrap; font-size: 13px; }}
.legend-item {{ display: flex; align-items: center; gap: 4px; }}
.legend-color {{ width: 12px; height: 12px; border-radius: 2px; }}
.search-box {{ margin-bottom: 16px; }}
.search-box input {{ padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 14px; width: 300px; max-width: 100%; }}
</style>
</head>
<body>
<h1>ADW Rating — Dry Run</h1>
<p class="subtitle">OpenSkill (Plackett-Luce) · {len(sizes)} size categories · No tier weighting</p>

<div class="legend">
    <div class="legend-item"><div class="legend-color" style="background:#f59e0b"></div> Elite (2500+)</div>
    <div class="legend-item"><div class="legend-color" style="background:#8b5cf6"></div> Champion (2200–2499)</div>
    <div class="legend-item"><div class="legend-color" style="background:#3b82f6"></div> Expert (1900–2199)</div>
    <div class="legend-item"><div class="legend-color" style="background:#10b981"></div> Competitor (1600–1899)</div>
    <div class="legend-item"><div class="legend-color" style="background:#d1d5db"></div> Provisional (&lt;1600)</div>
</div>

<div class="search-box">
    <input type="text" id="search" placeholder="Search handler, dog, or country..." oninput="filterRows()">
</div>

<div class="tabs">
    {"".join(tab_buttons)}
</div>

{"".join(tab_contents)}

<script>
let currentTab = '{sizes[0]}';

function showTab(size) {{
    document.querySelectorAll('.tab-content').forEach(el => el.style.display = 'none');
    document.querySelectorAll('.tab-btn').forEach(el => el.classList.remove('active'));
    document.getElementById('tab-' + size).style.display = 'block';
    event.target.closest('.tab-btn').classList.add('active');
    currentTab = size;
    filterRows();
}}

function sortTable(tableId, colIdx, type) {{
    const table = document.getElementById(tableId);
    const tbody = table.querySelector('tbody');
    const rows = Array.from(tbody.querySelectorAll('tr'));
    const th = table.querySelectorAll('th')[colIdx];
    const asc = th.dataset.sort !== 'asc';
    th.dataset.sort = asc ? 'asc' : 'desc';

    rows.sort((a, b) => {{
        let va = a.cells[colIdx].textContent.replace('±', '').trim();
        let vb = b.cells[colIdx].textContent.replace('±', '').trim();
        if (type === 'num') {{
            va = parseFloat(va) || 0;
            vb = parseFloat(vb) || 0;
            return asc ? va - vb : vb - va;
        }}
        return asc ? va.localeCompare(vb) : vb.localeCompare(va);
    }});

    rows.forEach(r => tbody.appendChild(r));
}}

function filterRows() {{
    const q = document.getElementById('search').value.toLowerCase();
    const table = document.getElementById('table-' + currentTab);
    if (!table) return;
    const rows = table.querySelectorAll('tbody tr');
    rows.forEach(row => {{
        const text = row.textContent.toLowerCase();
        row.style.display = text.includes(q) ? '' : 'none';
    }});
}}
</script>
</body>
</html>"""

    with open(outpath, "w", encoding="utf-8") as f:
        f.write(html)

    print(f"HTML written to {outpath}")


def _esc(s):
    """Escape HTML special characters."""
    return (s or "").replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    runs = load_all_runs()
    profiles = build_team_profiles(runs)
    all_ratings = calculate_ratings(runs, profiles)

    total_teams = sum(len(r) for r in all_ratings.values())
    print(f"\nTotal unique teams across all sizes: {total_teams}")

    write_csv(all_ratings)
    write_html(all_ratings)
    print("\nDone!")
