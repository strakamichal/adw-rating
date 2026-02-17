#!/usr/bin/env python3
"""Scrape competition results from agigames.cz (formerly dogco.cz) and save as CSV.

agigames.cz table structure (4 columns per row):
  [0] Rank: medal image (1-3) or number like "5."
  [1] Handler + dog: combined cell with link to /tv_me.php?tid=N
      - handler name in bold <a> tag
      - [group-number] prefix e.g. [14-460]
      - size + class e.g. "L A3"
      - kennel name in italic
      - dog name after <br/>
  [2] Faults: errors, refusals, total penalty (separated by <br/>)
  [3] Time + speed: time in seconds and speed in m/s (separated by <br/>)
"""

import csv
import os
import re
import sys
import time

import requests
from bs4 import BeautifulSoup

BASE_URL = "http://new.agigames.cz"
DELAY = 1.0

COMPETITIONS = {
    "prague-agility-party-2024": {
        "zid": 25,
    },
}


def fetch(url):
    """Fetch a URL with delay and return BeautifulSoup."""
    print(f"  GET {url}")
    time.sleep(DELAY)
    resp = requests.get(url, timeout=30, allow_redirects=True)
    resp.raise_for_status()
    return BeautifulSoup(resp.text, "html.parser")


def get_competition_runs(zid):
    """Get list of (bid, description, day) from competition home page."""
    soup = fetch(f"{BASE_URL}/tv_home.php?zid={zid}")
    runs = []
    current_day = ""

    # Walk through all elements to track day headers and run links
    for el in soup.find_all(True):
        if el.name in ("h2", "h3", "h4"):
            text = el.get_text(strip=True)
            if re.search(r"\d{2}\.\d{2}\.\d{4}", text):
                current_day = text
        elif el.name == "a" and el.get("href"):
            href = el["href"]
            m = re.search(r"bid=(\d+)", href)
            if m and "results" in href:
                bid = int(m.group(1))
                desc = el.get_text(strip=True)
                runs.append((bid, desc, current_day))

    return runs


def parse_run_results(zid, bid):
    """Parse results from a single run page. Returns (metadata, rows)."""
    soup = fetch(f"{BASE_URL}/tv_results.php?zid={zid}&bid={bid}")

    metadata = {"zid": zid, "bid": bid}

    # Extract metadata from page text
    text = soup.get_text()
    for pattern, key in [
        (r"SCT[:\s]*([\d.]+)", "standard_time"),
        (r"MCT[:\s]*([\d.]+)", "max_time"),
        (r"(\d+)\s*překáž", "obstacles"),
        (r"(\d+)\s*obstacles", "obstacles"),
        (r"[Dd]élka[:\s]*([\d.]+)", "course_length"),
    ]:
        m = re.search(pattern, text)
        if m:
            metadata[key] = m.group(1)

    # Judge - find from page structure
    judge_match = re.search(r"[Rr]ozhodčí[:\s]*([^\n]+)", text)
    if not judge_match:
        judge_match = re.search(r"[Jj]udge[:\s]*([^\n]+)", text)
    if judge_match:
        metadata["judge"] = judge_match.group(1).strip()

    # Parse results table
    table = soup.find("table")
    if not table:
        return metadata, []

    rows = []
    for tr in table.find_all("tr"):
        cells = tr.find_all("td")
        if len(cells) < 4:
            continue

        # --- Column 0: Rank ---
        rank_cell = cells[0]
        rank_text = rank_cell.get_text(strip=True).rstrip(".")
        # Medal images for top 3 have no text, detect from img
        if not rank_text:
            medal_img = rank_cell.find("img", src=re.compile(r"medal_\d"))
            if medal_img:
                m = re.search(r"medal_(\d)", medal_img["src"])
                rank_text = m.group(1) if m else ""

        # --- Column 1: Handler + Dog ---
        info_cell = cells[1]

        # Team ID from link
        team_id = ""
        handler_link = info_cell.find("a", href=re.compile(r"tid=\d+"))
        if handler_link:
            m = re.search(r"tid=(\d+)", handler_link["href"])
            if m:
                team_id = m.group(1)

        # Handler name from bold link text
        handler = ""
        if handler_link:
            handler = handler_link.get_text(strip=True)
            # Remove [group-number] prefix
            handler = re.sub(r"^\[\d+-\d+\]\s*", "", handler)

        # Start number
        start_num = ""
        num_span = info_cell.find("span", title=re.compile(r"skupina|závodní"))
        if num_span:
            m = re.search(r"\[(\d+-\d+)\]", num_span.get_text())
            if m:
                start_num = m.group(1)

        # Size + class (e.g. "L A3")
        size_class = ""
        info_text = info_cell.get_text()
        sc_match = re.search(r"\b(XS|S|M|L|I)\s+(A[123])\b", info_text)
        if sc_match:
            size_class = sc_match.group(0)

        # Country flag
        country = ""
        flag_img = info_cell.find("img", class_="vlajka")
        if flag_img and flag_img.get("src"):
            m = re.search(r"/([a-z]{2})\.(?:svg|png)", flag_img["src"])
            if m:
                country = m.group(1).upper()

        # Dog name - text after <br/> in the cell, typically in last span
        dog = ""
        br = info_cell.find("br")
        if br and br.next_sibling:
            # Get text after the <br/>
            dog_span = br.find_next("span")
            if dog_span:
                dog = dog_span.get_text(strip=True)
                # Remove breed icon text artifacts
                dog = re.sub(r"^\s*", "", dog).strip()

        # Kennel name (italic)
        kennel = ""
        italic = info_cell.find("span", style=re.compile(r"font-style:\s*italic"))
        if italic:
            kennel = italic.get_text(strip=True)

        # --- Column 2: Faults ---
        faults_cell = cells[2]
        faults_text = faults_cell.get_text(separator="|", strip=True)
        # Parse: errors | refusals | total penalty
        faults_parts = [p.strip() for p in faults_text.split("|")]
        chb = faults_parts[0] if len(faults_parts) > 0 else ""
        odm = faults_parts[1] if len(faults_parts) > 1 else ""
        tb_total = faults_parts[2] if len(faults_parts) > 2 else ""
        # Clean emoji
        tb_total = tb_total.replace("👌", "0")

        # --- Column 3: Time + Speed ---
        time_cell = cells[3]
        time_val = ""
        speed_val = ""
        time_text = time_cell.get_text()
        t_match = re.search(r"([\d.]+)\s*sec", time_text)
        if t_match:
            time_val = t_match.group(1)
        s_match = re.search(r"([\d.]+)\s*m/s", time_text)
        if s_match:
            speed_val = s_match.group(1)

        # --- Status (DIS etc.) ---
        status = ""
        # Check for DIS in the row
        tr_text = tr.get_text()
        if "DIS" in tr_text:
            status = "DIS"
        elif rank_text:
            status = ""  # ranked = completed

        row = {
            "rank": rank_text,
            "start_num": start_num,
            "handler": handler,
            "dog": dog,
            "kennel": kennel,
            "size_class": size_class,
            "country": country,
            "team_id": team_id,
            "chb": chb,
            "odm": odm,
            "tb_total": tb_total,
            "time": time_val,
            "speed": speed_val,
            "status": status,
        }
        rows.append(row)

    return metadata, rows


def scrape_competition(name, config, output_dir):
    """Scrape all runs for a competition and save results."""
    zid = config["zid"]
    print(f"\n{'='*60}")
    print(f"Scraping: {name} (zid: {zid})")
    print(f"{'='*60}")

    runs = get_competition_runs(zid)
    print(f"Found {len(runs)} runs")

    if not runs:
        print("WARNING: No runs found!")
        return 0

    all_results = []

    for bid, run_desc, day in runs:
        print(f"  Run bid={bid}: {run_desc} ({day})")
        metadata, rows = parse_run_results(zid, bid)
        print(f"    -> {len(rows)} results")

        for row in rows:
            row["competition"] = name
            row["bid"] = bid
            row["run_description"] = run_desc
            row["run_day"] = day
            row["judge"] = metadata.get("judge", "")
            row["standard_time"] = metadata.get("standard_time", "")
            row["max_time"] = metadata.get("max_time", "")
            row["course_length"] = metadata.get("course_length", "")
            row["obstacles"] = metadata.get("obstacles", "")
            all_results.append(row)

    if not all_results:
        print(f"WARNING: No results for {name}")
        return 0

    fieldnames = [
        "competition", "bid", "run_description", "run_day",
        "judge", "standard_time", "max_time", "course_length", "obstacles",
        "rank", "start_num", "team_id", "handler", "country",
        "dog", "kennel", "size_class",
        "chb", "odm", "tb_total", "time", "speed", "status",
    ]

    csv_path = os.path.join(output_dir, "results.csv")
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(all_results)

    print(f"  => Saved {len(all_results)} rows to {csv_path}")
    return len(all_results)


def main():
    base_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "data")

    names = sys.argv[1:] if len(sys.argv) > 1 else list(COMPETITIONS.keys())

    total = 0
    for name in names:
        if name not in COMPETITIONS:
            print(f"Unknown competition: {name}")
            continue
        output_dir = os.path.join(base_dir, name)
        os.makedirs(output_dir, exist_ok=True)
        total += scrape_competition(name, COMPETITIONS[name], output_dir)

    print(f"\nDone! Total: {total} rows.")


if __name__ == "__main__":
    main()
