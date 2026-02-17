#!/usr/bin/env python3
"""
Download and parse AWC 2025 results from official PDFs into CSV/JSON.
Requires: pdftotext (from poppler)
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

# All individual result PDFs (we skip team-total PDFs, only parse individual runs)
RESULT_FILES = {
    # Team Jumping
    "team_jumping_intermediate_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000480-38bc838bcb/Results%20Team%20Jumping%20IM%20Ind.pdf",
    "team_jumping_large_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000484-3cefc3ceff/Results%20Team%20Jumping%20Large%20Ind.pdf",
    "team_jumping_small_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000491-cb3e3cb3e4/Results%20Team%20Jumping%20Small%20Ind.pdf",
    "team_jumping_medium_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000495-44db444db5/Results%20Team%20Jumping%20Medium%20Ind.pdf",
    # Team Agility
    "team_agility_intermediate_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000501-a5f23a5f25/2_Results%20Team%20Agility%20IM%20ind.pdf",
    "team_agility_large_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000506-16eeb16eec/3_Results%20Team%20Agility%20L%20ind.pdf",
    "team_agility_small_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000514-6ecc36ecc5/1_Results%20Team%20Agility%20S%20Ind.pdf",
    "team_agility_medium_ind": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000519-e3487e3488/2_Results%20Team%20Agility%20M%20ind.pdf",
    # Individual Jumping
    "ind_jumping_intermediate": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000524-1fb421fb43/3_Results%20Individuell%20Jumping%20Intermediate.pdf",
    "ind_jumping_small": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000540-1c30d1c30e/2_Resultat%20Individuell%20Jumping%20Small.pdf",
    "ind_jumping_medium": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000545-d22f6d22f8/3_Results%20Individuell%20Jumping%20Medium.pdf",
    "ind_jumping_large": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000539-19fa019fa1/1_Results%20Individuell%20Jumping%20Large.pdf",
    # Individual Agility
    "ind_agility_intermediate": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000550-2036520367/4_Result%20individuell%20agility%20intermediate.pdf",
    "ind_agility_small": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000560-19b7a19b7c/2_Results%20Individuell%20Agility%20Small.pdf",
    "ind_agility_medium": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000556-5b5ff5b600/1_Results%20Individuell%20Agility%20Medium.pdf",
    "ind_agility_large": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000564-f056ff0571/3_results_ind_agility_large.pdf",
}

# Combined results (Individual Jumping + Agility)
COMBINED_FILES = {
    "ind_combined_intermediate": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000551-91f1491f16/4_resultat_combined%20individuell%20Intermediate.pdf",
    "ind_combined_small": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000561-e3a34e3a36/2_Results%20Ind%20S%20Combined.pdf",
    "ind_combined_medium": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000557-a3770a3772/1_Results%20Ind%20Agility%20M%20Combined.pdf",
    "ind_combined_large": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000565-6196561967/3_results_ind_combined_large.pdf",
}

# Team combined results
TEAM_COMBINED_FILES = {
    "team_combined_intermediate": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000503-2c5d22c5d4/2_Results%20Teams%20Combined%20IM.pdf",
    "team_combined_small": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000516-1094b1094f/1_Results%20Team%20Agility%20S%20Combined.pdf",
    "team_combined_medium": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000521-5b2c05b2c2/2_Results%20Team%20Agility%20M%20Combined.pdf",
    "team_combined_large": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000508-5a03b5a03d/3_Results%20Team%20Agility%20L%20Combined.pdf",
}

# Team total results
TEAM_TOTAL_FILES = {
    "team_jumping_intermediate_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000481-695f3695f6/Results%20Team%20Jumping%20IM%20Teams.pdf",
    "team_jumping_large_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000485-6fcbf6fcc1/Results%20Team%20Jumping%20Large%20Total.pdf",
    "team_jumping_small_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000492-27e9727e99/Resultat%20Team%20Jumping%20Small%20team.pdf",
    "team_jumping_medium_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000496-d20b9d20bb/Results%20Team%20Jumping%20Medium%20Teams.pdf",
    "team_agility_intermediate_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000502-0c0ce0c0cf/2_Results%20Team%20Agility%20IM%20Teams.pdf",
    "team_agility_large_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000507-9080390805/3_Results%20Team%20Agility%20L%20Teams.pdf",
    "team_agility_small_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000515-e30cae30cc/1_Results%20Team%20Agility%20S%20Team.pdf",
    "team_agility_medium_teams": "https://33f45a6374.clvaw-cdnwnd.com/b634add4590b8a37735a9a7dc8abf9c9/200000520-5549a5549c/2_Results%20Team%20Agility%20M%20Team.pdf",
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
    m = re.search(r'Judge:\s*(.+?)(?:\n|$)', text)
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
    """Parse individual result rows from pdftotext -layout output."""
    results = []

    lines = text.split('\n')

    # Merge continuation lines (lines that start with lots of spaces and contain
    # text that is a continuation of the dog name or handler name)
    merged_lines = []
    for line in lines:
        stripped = line.rstrip()
        if not stripped:
            merged_lines.append('')
            continue
        # Continuation line: starts with many spaces and no rank/number at the start
        if len(stripped) > 0 and len(line) - len(line.lstrip()) > 10 and not re.match(r'\s{0,8}\d', line):
            if merged_lines:
                merged_lines[-1] = merged_lines[-1].rstrip() + ' ' + stripped.strip()
                continue
        merged_lines.append(stripped)

    for line in merged_lines:
        # Try to match a result line with faults/time
        m = re.match(
            r'\s*(\d+)\s+'           # Rank
            r'(\d+)\s+'              # Start number
            r'(.+?)\s{2,}'           # Handler (followed by 2+ spaces)
            r'(.+?)\s{2,}'           # Dog (followed by 2+ spaces)
            r'(.+?)\s{2,}'           # Breed (followed by 2+ spaces)
            r'([A-Z]{3})\s+'         # Country code
            r'(\d+)\s+'              # Faults (F)
            r'(\d+)\s+'              # Refusals (R)
            r'([\d.]+)\s+'           # Time faults (T)
            r'([\d.]+)\s+'           # Total faults
            r'([\d.]+)\s+'           # Time
            r'([\d.]+)',             # Speed (m/s)
            line
        )
        if m:
            results.append({
                'rank': int(m.group(1)),
                'start_no': int(m.group(2)),
                'handler': m.group(3).strip(),
                'dog': m.group(4).strip(),
                'breed': m.group(5).strip(),
                'country': m.group(6),
                'faults': int(m.group(7)),
                'refusals': int(m.group(8)),
                'time_faults': float(m.group(9)),
                'total_faults': float(m.group(10)),
                'time': float(m.group(11)),
                'speed': float(m.group(12)),
                'eliminated': False,
            })
            continue

        # Try eliminated line (no rank, just number)
        m = re.match(
            r'\s*(\d+)\s+'           # Start number (no rank)
            r'(.+?)\s{2,}'           # Handler
            r'(.+?)\s{2,}'           # Dog
            r'(.+?)\s{2,}'           # Breed
            r'([A-Z]{3})\s+'         # Country
            r'Eliminated',
            line
        )
        if m:
            results.append({
                'rank': None,
                'start_no': int(m.group(1)),
                'handler': m.group(2).strip(),
                'dog': m.group(3).strip(),
                'breed': m.group(4).strip(),
                'country': m.group(5),
                'faults': None,
                'refusals': None,
                'time_faults': None,
                'total_faults': None,
                'time': None,
                'speed': None,
                'eliminated': True,
            })

    return results


# Known breed names for fixing misparse where handler+dog merge
KNOWN_BREEDS = [
    'Australian Kelpie', 'Australian Shepherd', 'Belgian Shepherd Dog',
    'Border Collie', 'Border Terrier', 'Cairn Terrier', 'Chinese Crested Dog',
    'Cocker Spaniel', 'Croatian Sheepdog', 'Dutch Shepherd Dog',
    'German Spitz', 'Hungarian Pumi', 'Jack Russel Terrier', 'Japanese Spitz',
    'Kromfohrländer', 'Miniature American Shepherd', 'Miniature Pinscher',
    'Miniature Schnauzer', 'Mudi', 'Nova Scotia Duck Tolling Retriever',
    'Papillon', 'Parson Russell Terrier', 'Pembroke Welsh Corgi', 'Poodle',
    'Pyrenean Sheepdog', 'Rough Collie', 'Shetland Sheepdog',
    'Spanish Water Dog', 'Bohemian Shepherd Dog', 'Cocker Spaniel (English)',
    'Miniature American', 'Belgian Shepherd Dog (Groenendael)',
]


def fix_misparse(row: dict) -> dict:
    """Fix rows where breed is empty because handler+dog merged.
    Case 1: 'dog' field IS the breed (handler contains handler+dog merged)
    Case 2: 'dog' field ends with the breed name (breed appended to dog name)
    """
    if row['breed']:
        return row

    dog_val = row['dog']

    # Case 1: dog field is entirely a breed name
    for breed in KNOWN_BREEDS:
        if dog_val == breed or dog_val.startswith(breed):
            row['breed'] = breed
            row['dog'] = ''  # handler+dog merged, can't split reliably
            return row

    # Case 2: breed is appended at the end of dog name
    for breed in sorted(KNOWN_BREEDS, key=len, reverse=True):  # longest first
        if dog_val.endswith(breed):
            row['breed'] = breed
            row['dog'] = dog_val[:-(len(breed))].strip()
            return row

    return row


# Fix truncated breed names from PDF line wrapping
BREED_FIXES = {
    'Australian': 'Australian Shepherd',
    'Shetland': 'Shetland Sheepdog',
    'Croatian': 'Croatian Sheepdog',
    'Miniature American': 'Miniature American Shepherd',
    'Nova Scotia Duck Tolling': 'Nova Scotia Duck Tolling Retriever',
    'Spanish Water': 'Spanish Water Dog',
    'Belgian Shepherd': 'Belgian Shepherd Dog',
}


def fix_breed(breed: str) -> str:
    return BREED_FIXES.get(breed, breed)


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

    print("Downloading individual result PDFs...")
    for name, url in RESULT_FILES.items():
        pdf_path = download_pdf(name, url)
        text = pdf_to_text(pdf_path)

        meta = parse_metadata(text)
        rows = parse_individual_results(text)

        size, discipline, is_team = extract_size_and_discipline(name)

        for row in rows:
            fix_misparse(row)
            row['breed'] = fix_breed(row['breed'])
            row['competition'] = 'AWC 2025'
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

    # Also download combined and team files for reference
    print("\nDownloading combined result PDFs...")
    for name, url in {**COMBINED_FILES, **TEAM_COMBINED_FILES, **TEAM_TOTAL_FILES}.items():
        download_pdf(name, url)

    print(f"\nTotal individual runs parsed: {len(all_results)}")

    # Write CSV
    csv_path = BASE_DIR / "awc2025_results.csv"
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
    print(f"CSV written to: {csv_path}")

    # Write JSON
    json_path = BASE_DIR / "awc2025_results.json"
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
