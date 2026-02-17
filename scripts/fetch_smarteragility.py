#!/usr/bin/env python3
"""
Download competition results from SmarterAgility API and save as CSV + JSON.

Usage:
    python fetch_smarteragility.py <hash_code> <output_dir> <competition_name>

Example:
    python fetch_smarteragility.py c60a8983e049c8285a5fe199f64fa455c4fca768 data/polish_open_2024_inl "Polish Open 2024 (IN & L)"
"""

import csv
import json
import os
import sys
import time
import urllib.request
from pathlib import Path
from typing import Union

BASE_URL = "https://www.smarteragility.com"
DELAY = 0.15  # seconds between requests


def api_get(path: str) -> Union[dict, list]:
    """Fetch JSON from SmarterAgility API."""
    url = f"{BASE_URL}{path}"
    ts = int(time.time() * 1000)
    separator = "&" if "?" in path else "?"
    url = f"{url}{separator}_={ts}"

    req = urllib.request.Request(url, headers={"User-Agent": "ADW-Rating/1.0"})
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode("utf-8"))


# Map SA category names to our standard size names
SIZE_MAP = {
    "small": "Small",
    "s": "Small",
    "medium": "Medium",
    "m": "Medium",
    "intermediate": "Intermediate",
    "in": "Intermediate",
    "large": "Large",
    "l": "Large",
    "xs+small": "Small",
    "xs+s": "Small",
    "xs": "Small",
}

# Round types to skip (warm-up, test runs, etc.)
SKIP_ROUND_TYPES = {"test 1", "test 2", "test 3", "test", "warm up", "warmup"}

# Map SA round type to discipline
def classify_round(round_type: str) -> Union[tuple, None]:
    """Return (discipline, is_team_round) from SA round type string, or None if skipped."""
    t = round_type.lower()
    if t in SKIP_ROUND_TYPES:
        return None
    is_team = "team" in t
    if "jumping" in t:
        discipline = "Jumping"
    elif "agility" in t:
        discipline = "Agility"
    elif "final" in t:
        discipline = "Final"
    else:
        discipline = round_type.title()
    return discipline, is_team


def is_ranked_status(status) -> bool:
    """Smarter round status can be textual or numeric.

    Observed values:
      - "ranked" (string) for completed rounds
      - 50 (int) for completed rounds on some events (e.g. AWC 2025)
    """
    if isinstance(status, str):
        return status.strip().lower() == "ranked"
    if isinstance(status, int):
        return status == 50
    return False


def fetch_competition(hash_code: str, output_dir: Path, competition_name: str):
    """Download all rounds of a competition and save to CSV + JSON."""
    print(f"Fetching competition detail: {hash_code[:16]}...")
    comp = api_get(f"/sal/competition/{hash_code}")
    trial = comp["trial"]
    rounds = comp["rounds"]

    print(f"  Title: {trial['title']}")
    print(f"  Date: {trial['date']}")
    print(f"  Country: {trial['country_code']}")
    print(f"  Rounds: {len(rounds)}")

    # Also try to get round metadata via the live API
    round_meta = {}

    all_results = []
    total_runs = 0

    for i, rnd in enumerate(rounds):
        if not is_ranked_status(rnd.get("status")):
            print(f"  [{i+1}/{len(rounds)}] {rnd['label']} ({rnd['type']}, {rnd['category']}) — skipped (status: {rnd['status']})")
            continue

        time.sleep(DELAY)
        round_hash = rnd["hash"]
        try:
            round_resp = api_get(f"/sal/round/{round_hash}")
        except Exception as e:
            print(f"  [{i+1}/{len(rounds)}] {rnd['label']} ({rnd['type']}, {rnd['category']}): ERROR {e} — skipped")
            continue

        # The response may be a dict with 'rankedRuns' key, or a plain list
        if isinstance(round_resp, dict):
            runs = round_resp.get("rankedRuns", []) or round_resp.get("runs", [])
            # Extract course metadata from the round object if present
            rd = round_resp.get("round", {})
        else:
            runs = round_resp
            rd = {}

        meta = {}
        if rd:
            if rd.get("length"):
                meta["course_length"] = float(rd["length"])
            if rd.get("sct"):
                meta["sct"] = float(rd["sct"])
            if rd.get("mct"):
                meta["mct"] = float(rd["mct"])
            if rd.get("judge"):
                meta["judge"] = rd["judge"]
            if rd.get("judge_2"):
                meta["judge"] = f"{rd['judge']}, {rd['judge_2']}"

        size = SIZE_MAP.get(rnd["category"].lower(), rnd["category"].title())
        classified = classify_round(rnd["type"])
        if classified is None:
            print(f"  [{i+1}/{len(rounds)}] {rnd['label']} ({rnd['type']}, {rnd['category']}): skipped (test/warmup)")
            continue
        discipline, is_team = classified
        round_key = f"{rnd['label']}_{rnd['type']}_{rnd['category']}".lower().replace(" ", "_").replace("-", "_")

        for run in runs:
            # Skip hidden handlers (GDPR)
            if run.get("hide_handler") == 1:
                continue

            eliminated = bool(run.get("is_eliminated"))
            rank = run.get("ranking")
            if isinstance(rank, str) and rank.strip() == "":
                rank = None

            row = {
                "competition": competition_name,
                "round_key": round_key,
                "size": size,
                "discipline": discipline,
                "is_team_round": is_team,
                "rank": rank if not eliminated else None,
                "start_no": run.get("dorsal", ""),
                "handler": run.get("handler", ""),
                "dog": run.get("dog", ""),
                "breed": run.get("breed", ""),
                "country": run.get("country_name", ""),
                "faults": run.get("course_faults"),
                "refusals": run.get("refusals"),
                "time_faults": run.get("time_faults"),
                "total_faults": run.get("total_faults"),
                "time": run.get("course_time"),
                "speed": run.get("speed"),
                "eliminated": eliminated,
                "judge": meta.get("judge", ""),
                "sct": meta.get("sct"),
                "mct": meta.get("mct"),
                "course_length": meta.get("course_length"),
            }
            all_results.append(row)

        print(f"  [{i+1}/{len(rounds)}] {rnd['label']} ({rnd['type']}, {rnd['category']}): {len(runs)} runs")
        total_runs += len(runs)

    print(f"\nTotal runs fetched: {total_runs}")
    print(f"Total runs after filtering: {len(all_results)}")

    # Save raw API response for rounds
    output_dir.mkdir(parents=True, exist_ok=True)

    raw_path = output_dir / "sa_rounds.json"
    with open(raw_path, "w", encoding="utf-8") as f:
        json.dump({"trial": trial, "rounds": rounds}, f, ensure_ascii=False, indent=2)
    print(f"Raw rounds saved to: {raw_path}")

    # Save CSV
    slug = output_dir.name
    csv_path = output_dir / f"{slug}_results.csv"
    fieldnames = [
        "competition", "round_key", "size", "discipline", "is_team_round",
        "rank", "start_no", "handler", "dog", "breed", "country",
        "faults", "refusals", "time_faults", "total_faults", "time", "speed",
        "eliminated", "judge", "sct", "mct", "course_length",
    ]
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(all_results)
    print(f"CSV written to: {csv_path}")

    # Save JSON
    json_path = output_dir / f"{slug}_results.json"
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump(all_results, f, ensure_ascii=False, indent=2)
    print(f"JSON written to: {json_path}")

    # Summary
    print(f"\n--- Summary ---")
    from collections import Counter
    by_size = Counter(r["size"] for r in all_results)
    for size, count in sorted(by_size.items()):
        elim = sum(1 for r in all_results if r["size"] == size and r["eliminated"])
        print(f"  {size}: {count} runs ({elim} eliminated)")

    return len(all_results)


def main():
    if len(sys.argv) < 4:
        print(f"Usage: {sys.argv[0]} <hash_code> <output_dir> <competition_name>")
        sys.exit(1)

    hash_code = sys.argv[1]
    output_dir = Path(sys.argv[2])
    competition_name = sys.argv[3]

    fetch_competition(hash_code, output_dir, competition_name)


if __name__ == "__main__":
    main()
