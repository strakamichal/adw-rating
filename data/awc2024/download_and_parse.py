#!/usr/bin/env python3
"""
Download and parse AWC 2024 results from official PDFs into CSV/JSON.
Requires: pdftotext (from poppler)

NOTE: 3 PDFs are image-based and cannot be parsed with pdftotext:
  - Agility-Inter-Ranking.pdf (ind_agility_intermediate)
  - TeamJumping-Inter-Results.pdf (team_jumping_intermediate_ind)
  - TeamJumping-Medium-Results.pdf (team_jumping_medium_ind)
These require OCR (tesseract) and are skipped for now.
"""

import csv
import json
import os
import re
import subprocess
import urllib.request
from pathlib import Path

BASE_DIR = Path(__file__).parent
PDF_DIR = BASE_DIR / "pdf"
PDF_DIR.mkdir(exist_ok=True)

BASE_URL = "https://awc2024agility.be/wp-content/uploads/2024/10"

# Individual result PDFs (individual ranking per run)
RESULT_FILES = {
    # Team Jumping (individual results)
    "team_jumping_large_ind": f"{BASE_URL}/TeamJumping-Large-Ranking-Individual.pdf",
    "team_jumping_small_ind": f"{BASE_URL}/TeamJumping-Small-Results-2.pdf",
    # "team_jumping_intermediate_ind": f"{BASE_URL}/TeamJumping-Inter-Results.pdf",  # IMAGE-BASED
    # "team_jumping_medium_ind": f"{BASE_URL}/TeamJumping-Medium-Results.pdf",  # IMAGE-BASED
    # Team Agility (individual results)
    "team_agility_large_ind": f"{BASE_URL}/TeamAgility-Large-Ranking-individual.pdf",
    "team_agility_intermediate_ind": f"{BASE_URL}/TeamAgility-intermediate-Ranking-Individual.pdf",
    "team_agility_medium_ind": f"{BASE_URL}/TeamAgility-medium-Ranking-Individual.pdf",
    "team_agility_small_ind": f"{BASE_URL}/TeamAgility-Small-Ranking-individual.pdf",
    # Individual Jumping
    "ind_jumping_large": f"{BASE_URL}/Jumping-Large-Ranking.pdf",
    "ind_jumping_intermediate": f"{BASE_URL}/Jumping-Intermediate-Ranking.pdf",
    "ind_jumping_medium": f"{BASE_URL}/Jumping-Medium-Ranking.pdf",
    "ind_jumping_small": f"{BASE_URL}/Jumping-Small-Ranking.pdf",
    # Individual Agility
    "ind_agility_large": f"{BASE_URL}/Agility-Large-Ranking.pdf",
    # "ind_agility_intermediate": f"{BASE_URL}/Agility-Inter-Ranking.pdf",  # IMAGE-BASED
    "ind_agility_medium": f"{BASE_URL}/Agility-Medium-Ranking.pdf",
    "ind_agility_small": f"{BASE_URL}/Agility-Small-Ranking.pdf",
}


def download_pdf(name: str, url: str) -> Path:
    """Download a PDF if not already cached."""
    path = PDF_DIR / f"{name}.pdf"
    if path.exists():
        print(f"  [cached] {name}")
        return path
    print(f"  [downloading] {name}")
    urllib.request.urlretrieve(url, path)
    return path


def pdf_to_text(pdf_path: Path) -> str:
    """Convert PDF to text using pdftotext with layout preservation."""
    result = subprocess.run(
        ["pdftotext", "-layout", str(pdf_path), "-"],
        capture_output=True, text=True
    )
    return result.stdout


def parse_metadata(text: str) -> dict:
    """Extract round metadata (ring, discipline, size, judge, SCT, MCT, length)."""
    meta = {}

    # Ring / discipline / size: e.g. "Ring 1 • Jumping • Large" or "Ring 1 • Team-Jumping • Large"
    m = re.search(r'Ring\s+(\d+)\s*[•·]\s*([\w-]+)\s*[•·]\s*(\w+)', text)
    if m:
        meta['ring'] = int(m.group(1))
        meta['discipline'] = m.group(2).strip()
        meta['size'] = m.group(3).strip()

    # Judge
    m = re.search(r'Judge(?:s)?:\s*(.+?)(?:\n|$)', text)
    if m:
        meta['judge'] = m.group(1).strip()

    # SCT, MCT, Length
    m = re.search(r'SCT:\s*([\d.]+)\s*s', text)
    if m:
        meta['sct'] = float(m.group(1))

    m = re.search(r'MCT:\s*([\d.]+)\s*s', text)
    if m:
        meta['mct'] = float(m.group(1))

    m = re.search(r'Length:\s*([\d.]+)\s*m', text)
    if m:
        meta['course_length'] = float(m.group(1))

    # Participants count
    m = re.search(r'#\s*Participants:\s*(\d+)', text)
    if m:
        meta['participants'] = int(m.group(1))

    return meta


def parse_individual_results(text: str) -> list[dict]:
    """Parse individual result rows from pdftotext -layout output.

    AWC 2024 format has no breed column (unlike AWC 2025).
    Columns: Rank, No, Handler, Dog, Country, F, R, T, Tot, Time, m/s
    """
    results = []
    lines = text.split('\n')

    # Merge continuation lines
    merged_lines = []
    for line in lines:
        stripped = line.rstrip()
        if not stripped:
            merged_lines.append('')
            continue
        if len(stripped) > 0 and len(line) - len(line.lstrip()) > 10 and not re.match(r'\s{0,8}\d', line):
            if merged_lines:
                merged_lines[-1] = merged_lines[-1].rstrip() + ' ' + stripped.strip()
                continue
        merged_lines.append(stripped)

    for line in merged_lines:
        # Match a result line: Rank No Handler Dog Country F R T Tot Time Speed
        m = re.match(
            r'\s*(\d+)\s+'           # Rank
            r'(\d+)\s+'              # Start number
            r'(.+?)\s{2,}'          # Handler (followed by 2+ spaces)
            r'(.+?)\s+'             # Dog (followed by spaces)
            r'([A-Z]{3})\s+'        # Country code
            r'(\d+)\s+'             # Faults (F)
            r'(\d+)\s+'             # Refusals (R)
            r'([\d.]+)\s+'          # Time faults (T)
            r'([\d.]+)\s+'          # Total faults
            r'([\d.]+)\s+'          # Time
            r'([\d.]+)',            # Speed (m/s)
            line
        )
        if m:
            results.append({
                'rank': int(m.group(1)),
                'start_no': int(m.group(2)),
                'handler': m.group(3).strip(),
                'dog': m.group(4).strip(),
                'breed': '',
                'country': m.group(5),
                'faults': int(m.group(6)),
                'refusals': int(m.group(7)),
                'time_faults': float(m.group(8)),
                'total_faults': float(m.group(9)),
                'time': float(m.group(10)),
                'speed': float(m.group(11)),
                'eliminated': False,
            })
            continue

        # Eliminated line: No Handler Dog Country Eliminated
        m = re.match(
            r'\s*(\d+)\s+'           # Start number (no rank)
            r'(.+?)\s{2,}'          # Handler
            r'(.+?)\s+'             # Dog
            r'([A-Z]{3})\s+'        # Country
            r'Eliminated',
            line
        )
        if m:
            results.append({
                'rank': None,
                'start_no': int(m.group(1)),
                'handler': m.group(2).strip(),
                'dog': m.group(3).strip(),
                'breed': '',
                'country': m.group(4),
                'faults': None,
                'refusals': None,
                'time_faults': None,
                'total_faults': None,
                'time': None,
                'speed': None,
                'eliminated': True,
            })

    return results


def extract_size_and_discipline(name: str) -> tuple[str, str, bool]:
    """Extract size category, discipline and team flag from the file key name."""
    is_team = 'team_' in name

    size = 'unknown'
    for s in ['small', 'medium', 'intermediate', 'large']:
        if s in name:
            size = s.capitalize()
            break

    if 'jumping' in name:
        discipline = 'Jumping'
    elif 'agility' in name:
        discipline = 'Agility'
    else:
        discipline = 'Unknown'

    return size, discipline, is_team


def main():
    all_results = []

    print("Downloading and parsing AWC 2024 result PDFs...")
    for name, url in RESULT_FILES.items():
        pdf_path = download_pdf(name, url)
        text = pdf_to_text(pdf_path)

        if not text.strip():
            print(f"  {name}: SKIPPED (image-based PDF, no text extracted)")
            continue

        meta = parse_metadata(text)
        rows = parse_individual_results(text)

        size, discipline, is_team = extract_size_and_discipline(name)

        for row in rows:
            row['competition'] = 'AWC 2024'
            row['round_key'] = name
            row['size'] = meta.get('size', size)
            row['discipline'] = meta.get('discipline', discipline).replace('Team-', '')
            row['is_team_round'] = is_team
            row['judge'] = meta.get('judge', '')
            row['sct'] = meta.get('sct')
            row['mct'] = meta.get('mct')
            row['course_length'] = meta.get('course_length')

        print(f"  {name}: {len(rows)} results parsed (meta: {meta.get('discipline', '?')} {meta.get('size', '?')})")
        all_results.extend(rows)

    print(f"\nTotal individual runs parsed: {len(all_results)}")

    # Note missing rounds
    missing = [
        'team_jumping_intermediate_ind',
        'team_jumping_medium_ind',
        'ind_agility_intermediate',
    ]
    print(f"\nMissing rounds (image-based PDFs, need OCR): {', '.join(missing)}")

    # Write CSV
    csv_path = BASE_DIR / "awc2024_results.csv"
    fieldnames = [
        'competition', 'round_key', 'size', 'discipline', 'is_team_round',
        'rank', 'start_no', 'handler', 'dog', 'breed', 'country',
        'faults', 'refusals', 'time_faults', 'total_faults', 'time', 'speed',
        'eliminated', 'judge', 'sct', 'mct', 'course_length',
    ]
    with open(csv_path, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(all_results)
    print(f"\nCSV written to: {csv_path}")

    # Write JSON
    json_path = BASE_DIR / "awc2024_results.json"
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(all_results, f, ensure_ascii=False, indent=2)
    print(f"JSON written to: {json_path}")

    # Summary
    print("\n--- Summary by round ---")
    from collections import Counter
    by_round = Counter(r['round_key'] for r in all_results)
    for key, count in sorted(by_round.items()):
        elim = sum(1 for r in all_results if r['round_key'] == key and r['eliminated'])
        print(f"  {key}: {count} runs ({elim} eliminated)")


if __name__ == '__main__':
    main()
