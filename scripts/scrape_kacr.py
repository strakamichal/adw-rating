#!/usr/bin/env python3
"""Scrape competition results from kacr.info and save as CSV."""

import csv
import os
import re
import sys
import time

import requests
from bs4 import BeautifulSoup

BASE_URL = "https://kacr.info"
DELAY = 1.0  # seconds between requests

COMPETITIONS = {
    "moravia-open-2024": 4738,
    "moravia-open-2025": 4925,
    "prague-agility-party-2024": 4730,
    "prague-agility-party-2025": 5211,
}

# Fixed column names by position (table has no headers)
COLUMN_NAMES = [
    "rank", "prukaz", "handler", "dog", "chb", "odm",
    "tb_time", "tb_total", "time", "speed", "status",
]


def fetch(url):
    """Fetch a URL with delay and return BeautifulSoup."""
    print(f"  GET {url}")
    time.sleep(DELAY)
    resp = requests.get(url, timeout=30)
    resp.raise_for_status()
    return BeautifulSoup(resp.text, "html.parser")


def get_competition_runs(competition_id):
    """Get list of (run_id, run_description, day) from a competition page."""
    soup = fetch(f"{BASE_URL}/competitions/{competition_id}")
    runs = []
    for a in soup.find_all("a", href=re.compile(r"/runs/\d+")):
        href = a["href"]
        run_id = int(href.rstrip("/").split("/")[-1])
        desc = a.get_text(strip=True)

        # Find day heading by walking up the DOM
        day = ""
        parent = a.parent
        while parent:
            prev = parent.find_previous_sibling(["h2", "h3", "h4"])
            if prev:
                day = prev.get_text(strip=True)
                break
            parent = parent.parent

        runs.append((run_id, desc, day))
    return runs


def parse_run_results(run_id):
    """Parse results table from a run page. Returns (metadata, rows)."""
    soup = fetch(f"{BASE_URL}/runs/{run_id}")

    metadata = {"run_id": run_id}

    # Title
    title_el = soup.find("h1")
    if title_el:
        metadata["title"] = title_el.get_text(strip=True)

    # Extract run parameters from page text
    text = soup.get_text()
    for pattern, key in [
        (r"[Ss]tandardní čas[:\s]*([\d.]+)", "standard_time"),
        (r"[Mm]aximální čas[:\s]*([\d.]+)", "max_time"),
        (r"[Dd]élka[:\s]*([\d.]+)", "course_length"),
        (r"[Pp]řekáž[ek]*[:\s]*(\d+)", "obstacles"),
        (r"[Rr]ychlost[:\s]*([\d.]+)", "required_speed"),
    ]:
        m = re.search(pattern, text)
        if m:
            metadata[key] = m.group(1)

    # Judge
    judge_link = soup.find("a", href=re.compile(r"/judges/\d+"))
    if judge_link:
        metadata["judge"] = judge_link.get_text(strip=True)

    # Parse results table
    table = soup.find("table")
    if not table:
        return metadata, []

    rows = []
    for tr in table.find_all("tr"):
        cells = tr.find_all("td")
        if not cells:
            continue

        cell_texts = [c.get_text(strip=True) for c in cells]

        # Skip empty rows (no rank = header/separator)
        if not cell_texts[0]:
            continue

        row = {}
        for i, val in enumerate(cell_texts):
            col_name = COLUMN_NAMES[i] if i < len(COLUMN_NAMES) else f"col_{i}"
            row[col_name] = val

        # Extract handler_id and dog_id from links
        handler_link = tr.find("a", href=re.compile(r"/handlers/\d+"))
        dog_link = tr.find("a", href=re.compile(r"/dogs/\d+"))
        if handler_link:
            row["handler_id"] = handler_link["href"].rstrip("/").split("/")[-1]
        if dog_link:
            row["dog_id"] = dog_link["href"].rstrip("/").split("/")[-1]

        rows.append(row)

    return metadata, rows


def scrape_competition(name, competition_id, output_dir):
    """Scrape all runs for a competition and save results."""
    print(f"\n{'='*60}")
    print(f"Scraping: {name} (ID: {competition_id})")
    print(f"{'='*60}")

    runs = get_competition_runs(competition_id)
    print(f"Found {len(runs)} runs")

    all_results = []

    for run_id, run_desc, day in runs:
        print(f"  Run {run_id}: {run_desc} ({day})")
        metadata, rows = parse_run_results(run_id)
        print(f"    -> {len(rows)} results")

        for row in rows:
            row["competition"] = name
            row["competition_id"] = competition_id
            row["run_id"] = run_id
            row["run_description"] = run_desc
            row["run_day"] = day
            row["judge"] = metadata.get("judge", "")
            row["standard_time"] = metadata.get("standard_time", "")
            row["max_time"] = metadata.get("max_time", "")
            row["course_length"] = metadata.get("course_length", "")
            row["obstacles"] = metadata.get("obstacles", "")
            all_results.append(row)

    if not all_results:
        print(f"WARNING: No results found for {name}")
        return 0

    # Column order for CSV
    fieldnames = [
        "competition", "competition_id", "run_id", "run_description", "run_day",
        "judge", "standard_time", "max_time", "course_length", "obstacles",
        "rank", "prukaz", "handler_id", "handler", "dog_id", "dog",
        "chb", "odm", "tb_time", "tb_total", "time", "speed", "status",
    ]
    # Add any extra columns found
    for row in all_results:
        for key in row:
            if key not in fieldnames:
                fieldnames.append(key)

    csv_path = os.path.join(output_dir, f"{name}.csv")
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(all_results)

    print(f"  => Saved {len(all_results)} rows to {csv_path}")
    return len(all_results)


def main():
    output_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "data", "imports")
    os.makedirs(output_dir, exist_ok=True)

    names = sys.argv[1:] if len(sys.argv) > 1 else list(COMPETITIONS.keys())

    total = 0
    for name in names:
        if name not in COMPETITIONS:
            print(f"Unknown competition: {name}")
            continue
        total += scrape_competition(name, COMPETITIONS[name], output_dir)

    print(f"\nDone! Total: {total} result rows across {len(names)} competitions.")


if __name__ == "__main__":
    main()
