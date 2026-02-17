#!/usr/bin/env python3
"""
OCR and parse the 3 image-based AWC 2024 PDFs that pdftotext cannot handle.
Requires: tesseract, pdf2image, pytesseract, Pillow
"""

import csv
import json
import re
from pathlib import Path
from typing import Optional

from pdf2image import convert_from_path
import pytesseract

BASE_DIR = Path(__file__).parent
PDF_DIR = BASE_DIR / "pdf"

VALID_COUNTRIES = {
    'AUT', 'BEL', 'BGR', 'BRA', 'CAN', 'CHE', 'CHL', 'CHN', 'COL', 'CRI',
    'CZE', 'DEU', 'DNK', 'ECU', 'ESP', 'EST', 'FIN', 'FRA', 'GBR', 'GRC',
    'HRV', 'HUN', 'IRL', 'ISR', 'ITA', 'JPN', 'LTU', 'LUX', 'LVA', 'MEX',
    'MYS', 'NLD', 'NOR', 'POL', 'PRT', 'SGP', 'SLV', 'SVK', 'SVN', 'SWE',
    'THA', 'UKR', 'URY', 'USA', 'VEN', 'ZAF',
}

# OCR commonly misreads these
COUNTRY_FIXES = {
    'DITA': 'ITA', 'pita': 'ITA', '(ITA': 'ITA', ')ITA': 'ITA',
    'MEST': 'EST', 'REST': 'EST', 'Est': 'EST', 'eEST': 'EST',
    'swe': 'SWE', 'svN': 'SVN', 'svn': 'SVN',
    'pFRA': 'FRA', '(FRA': 'FRA',
    '(IRL': 'IRL',
    'pire': 'IRL',  # not sure but seems like IRL
}

OCR_FILES = {
    "team_jumping_intermediate_ind": {
        "pdf": "TeamJumping-Inter-Results.pdf",
    },
    "team_jumping_medium_ind": {
        "pdf": "TeamJumping-Medium-Results.pdf",
    },
    "ind_agility_intermediate": {
        "pdf": "Agility-Inter-Ranking.pdf",
    },
}


def ocr_pdf(pdf_path: Path) -> str:
    images = convert_from_path(str(pdf_path), dpi=400)
    pages = []
    for img in images:
        text = pytesseract.image_to_string(img, config='--psm 6')
        pages.append(text)
    return '\n'.join(pages)


def fix_ocr_digit(s: str) -> str:
    """Fix OCR misreads of digits: O->0, Q->0, etc."""
    return s.replace('O', '0').replace('Q', '0').replace('l', '1')


def find_country(text: str) -> Optional[str]:
    """Find a valid country code in text, handling OCR artifacts."""
    # First try exact 3-letter uppercase matches
    for m in re.finditer(r'[A-Z]{3}', text):
        if m.group() in VALID_COUNTRIES:
            return m.group()
    # Try case-insensitive
    for m in re.finditer(r'[A-Za-z]{3}', text):
        upper = m.group().upper()
        if upper in VALID_COUNTRIES:
            return upper
    # Try known OCR fixes
    for wrong, right in COUNTRY_FIXES.items():
        if wrong in text:
            return right
    return None


def parse_metadata(text: str) -> dict:
    meta = {}
    m = re.search(r'Judge[s]?:\s*(.+?)(?:\n|$)', text)
    if m:
        meta['judge'] = m.group(1).strip()
    m = re.search(r'SCT:\s*([\d.]+)\s*s', text)
    if m:
        meta['sct'] = float(m.group(1))
    m = re.search(r'MCT:\s*([\d.]+)\s*s', text)
    if m:
        meta['mct'] = float(m.group(1))
    m = re.search(r'Length:\s*([\d.]+)\s*m', text)
    if m:
        meta['course_length'] = float(m.group(1))
    m = re.search(r'#\s*Participants:\s*(\d+)', text)
    if m:
        meta['participants'] = int(m.group(1))
    return meta


def parse_result_line(line: str) -> Optional[dict]:
    """Parse a single result line, working from both ends.

    Strategy: find the numeric fields at the end (speed, time, total, T, R, F)
    then extract rank/no from the start, and handler+dog+country from the middle.
    """
    line = line.strip()
    if not line:
        return None

    # Skip headers and junk
    skip_patterns = ['AWC 2024', 'Belgian Agility', 'Ring 1', 'Judge:', 'SCT:', 'MCT:',
                     'Participants', 'R. No. Handler', 'Country', 'Time m/s',
                     'AGILITY', 'CHAMPIONSHIP', 'BELGIUM', 'GALICA', 'Smarter',
                     'Designer', 'Journal', 'Secretary', 'WORLD', 'SENTOWER',
                     'F. R.', 'Tot.']
    if any(kw in line for kw in skip_patterns):
        return None
    # Skip "Results" only as a standalone word (not in dog names)
    if re.match(r'\s*Results\s*$', line):
        return None

    # Handle eliminated lines
    if 'Eliminated' in line or 'eliminated' in line:
        # Extract start number from the beginning
        m = re.match(r'\s*(\d+)\s+(.+?)\s+Eliminated', line, re.IGNORECASE)
        if not m:
            return None
        start_no = int(m.group(1))
        middle = m.group(2).strip()
        country = find_country(middle)

        # Remove country and garbage from middle to get handler+dog
        handler, dog = split_handler_dog(middle, country)

        return {
            'rank': None, 'start_no': start_no,
            'handler': handler, 'dog': dog, 'breed': '',
            'country': country or '',
            'faults': None, 'refusals': None, 'time_faults': None,
            'total_faults': None, 'time': None, 'speed': None,
            'eliminated': True,
        }

    # Strip trailing non-numeric text that got appended from continuation lines
    # e.g. "... 5.23 My)" -> "... 5.23"
    cleaned = re.sub(r'(\d+\.\d+)\s+[A-Za-z)]+\)?\s*$', r'\1', line)
    if cleaned == line:
        cleaned = line
    # "0)" -> "0", "¢)" -> "0" (OCR adds parens/symbols to digits)
    cleaned = re.sub(r'(\d)[)}\]]+', r'\1', cleaned)
    cleaned = re.sub(r'[¢(]\)', '0', cleaned)
    # ") 0" -> "0 0" (stray paren before digit)
    cleaned = re.sub(r'\)\s+(\d)', r'0 \1', cleaned)
    # "0-0:" -> "0 0" (OCR merges digits with dash/colon)
    cleaned = re.sub(r'(\d)[-:]+(\d)', r'\1 \2', cleaned)
    # Remove trailing colon after digit: "0:" -> "0"
    cleaned = re.sub(r'(\d):', r'\1', cleaned)
    # "#1" -> "1" (hash before digit)
    cleaned = re.sub(r'#(\d)', r'\1', cleaned)
    # "1." followed by space and decimal -> "1 " (trailing period on integer, not a decimal)
    # Match: digit, period, space, then a decimal number (N.NN)
    cleaned = re.sub(r'(\d)\.\s+(\d+\.\d+)', r'\1 \2', cleaned)
    # "(0.00" -> "0.00" (paren before decimal)
    cleaned = re.sub(r'[«(]+(\d+\.\d+)', r'\1', cleaned)
    # "= " or "= «(" before digits in numeric area -> space
    cleaned = re.sub(r'=\s*[«(]*(\d)', r' \1', cleaned)
    # "0iTA" -> "ITA" (digit merged with country code)
    cleaned = re.sub(r'(\d)([a-z]?[A-Z]{2,3})\b', r'\1 \2', cleaned)
    # Handle "«(0" pattern that becomes just "0" (missing the F column)
    # If after cleaning we have only 5 numbers, prepend a 0 for faults
    # This is handled by the 5-number fallback below

    # Try to extract the numeric tail: F R T Tot Time Speed
    m = re.search(
        r'([0-9OQ]+)\s+'          # F (faults)
        r'([0-9OQ]+)\s+'          # R (refusals)
        r'([0-9OQ.,]+)\s+'        # T (time faults)
        r'([0-9OQ.,]+)\s+'        # Tot (total faults)
        r'([0-9OQ.,]+)\s+'        # Time
        r'([0-9OQ.,]+)\s*$',      # Speed
        cleaned
    )

    if not m:
        # Try even more flexible: handle remaining artifacts
        cleaned2 = re.sub(r'[#=+\-—*>()]+\s*(\d)', r' \1', cleaned)
        cleaned2 = re.sub(r'(\d)\s*[#=+*()]+\s*(\d)', r'\1 \2', cleaned2)

        m = re.search(
            r'([0-9OQ]+)\s+'
            r'([0-9OQ]+)\s+'
            r'([0-9OQ.,]+)\s+'
            r'([0-9OQ.,]+)\s+'
            r'([0-9OQ.,]+)\s+'
            r'([0-9OQ.,]+)\s*$',
            cleaned2
        )
        if not m:
            # Try 5-number pattern (F column was lost/merged)
            m5 = re.search(
                r'([0-9OQ]+)\s+'          # R (refusals) — was first after lost F
                r'([0-9OQ.,]+)\s+'
                r'([0-9OQ.,]+)\s+'
                r'([0-9OQ.,]+)\s+'
                r'([0-9OQ.,]+)\s*$',
                cleaned2 if 'cleaned2' in dir() else cleaned
            )
            if m5:
                # Assume F=0 was lost
                class FakeMatch:
                    def __init__(self, groups):
                        self._groups = groups
                    def group(self, i):
                        return self._groups[i-1]
                    def start(self):
                        return m5.start()
                m = FakeMatch(['0'] + [m5.group(i) for i in range(1, 6)])
            else:
                return None

    # Parse numeric values
    def parse_num(s: str) -> float:
        s = fix_ocr_digit(s).replace(',', '.')
        return float(s)

    try:
        faults = int(fix_ocr_digit(m.group(1)))
        refusals = int(fix_ocr_digit(m.group(2)))
        time_faults = parse_num(m.group(3))
        total_faults = parse_num(m.group(4))
        time_val = parse_num(m.group(5))
        speed = parse_num(m.group(6))
    except (ValueError, IndexError):
        return None

    # Get the part before the numeric tail (use cleaned version since m was matched against it)
    source = cleaned
    prefix = source[:m.start()].strip()

    # Extract rank and start_no from the beginning
    # Could be "rank no handler..." or just "no handler..." (for continuation pages)
    num_match = re.match(r'(\d+)\.?\s+(\d+)\s+(.+)', prefix)
    if num_match:
        rank = int(num_match.group(1))
        start_no = int(num_match.group(2))
        middle = num_match.group(3).strip()
    else:
        # Just one number at start
        num_match = re.match(r'(\d+)\s+(.+)', prefix)
        if num_match:
            # Could be start_no without rank, but check if second token is also a number
            rest = num_match.group(2)
            num2 = re.match(r'(\d+)\s+(.+)', rest)
            if num2:
                rank = int(num_match.group(1))
                start_no = int(num2.group(1))
                middle = num2.group(2).strip()
            else:
                rank = None
                start_no = int(num_match.group(1))
                middle = rest.strip()
        else:
            return None

    country = find_country(middle)
    handler, dog = split_handler_dog(middle, country)

    return {
        'rank': rank, 'start_no': start_no,
        'handler': handler, 'dog': dog, 'breed': '',
        'country': country or '',
        'faults': faults, 'refusals': refusals,
        'time_faults': time_faults, 'total_faults': total_faults,
        'time': time_val, 'speed': speed,
        'eliminated': False,
    }


def split_handler_dog(middle: str, country: Optional[str]) -> tuple:
    """Split middle section into handler and dog, removing country and garbage."""
    if country:
        # Remove everything from the country code to the end
        # Find the country code (case insensitive)
        pattern = re.compile(re.escape(country), re.IGNORECASE)
        # Find last occurrence
        matches = list(pattern.finditer(middle))
        if matches:
            handler_dog = middle[:matches[-1].start()].rstrip()
        else:
            # Try removing from the end
            handler_dog = middle
        # Also try direct: find any 3-letter uppercase sequence = country near end
        for m in re.finditer(r'[A-Z]{3}', middle):
            if m.group().upper() == country:
                handler_dog = middle[:m.start()].rstrip()
                break
    else:
        handler_dog = middle

    # Clean up trailing garbage (emoji artifacts, symbols)
    handler_dog = re.sub(r'[\s\W]*$', '', handler_dog)
    # Also clean leading garbage
    handler_dog = re.sub(r'^[~=\-]+\s*', '', handler_dog)

    # Split on 2+ spaces
    parts = re.split(r'\s{2,}', handler_dog, maxsplit=1)
    if len(parts) == 2:
        return parts[0].strip(), parts[1].strip()

    # Try to find dog name by looking for parenthesized call name
    paren = re.search(r'\(([^)]+)\)\s*$', handler_dog)
    if paren:
        # Find where the dog name likely starts - look for the capital letter
        # after handler name with a space
        # Heuristic: handler is the first "word group" before the dog's registered name
        # This is imperfect but better than nothing
        pass

    return handler_dog, ''


def extract_size_and_discipline(name: str) -> tuple:
    is_team = 'team_' in name
    size = 'unknown'
    for s in ['small', 'medium', 'intermediate', 'large']:
        if s in name:
            size = s.capitalize()
            break
    discipline = 'Jumping' if 'jumping' in name else 'Agility' if 'agility' in name else 'Unknown'
    return size, discipline, is_team


def main():
    all_new_results = []

    for round_key, info in OCR_FILES.items():
        pdf_path = PDF_DIR / info['pdf']
        if not pdf_path.exists():
            print(f"  [missing] {pdf_path}")
            continue

        print(f"\nProcessing {round_key} ({info['pdf']})...")
        text = ocr_pdf(pdf_path)

        meta = parse_metadata(text)
        lines = text.split('\n')

        # Merge continuation lines (lines that don't start with a number)
        merged = []
        for line in lines:
            stripped = line.strip()
            if not stripped:
                continue
            if merged and not re.match(r'\d', stripped) and not any(
                kw in stripped for kw in ['AWC', 'Belgian', 'Ring', 'Judge', 'SCT',
                                          'Participants', 'Handler', 'R.', 'F.', 'Tot.',
                                          'Results', 'AGILITY', 'WORLD', 'BELGIUM',
                                          'GALICA', 'Smarter', 'Designer']
            ):
                merged[-1] = merged[-1] + ' ' + stripped
            else:
                merged.append(stripped)

        rows = []
        for line in merged:
            result = parse_result_line(line)
            if result:
                rows.append(result)

        size, discipline, is_team = extract_size_and_discipline(round_key)

        for row in rows:
            row['competition'] = 'AWC 2024'
            row['round_key'] = round_key
            row['size'] = size
            row['discipline'] = discipline
            row['is_team_round'] = is_team
            row['judge'] = meta.get('judge', '')
            row['sct'] = meta.get('sct')
            row['mct'] = meta.get('mct')
            row['course_length'] = meta.get('course_length')

        expected = meta.get('participants')
        elim_count = sum(1 for r in rows if r['eliminated'])
        non_elim = sum(1 for r in rows if not r['eliminated'])
        print(f"  Parsed: {len(rows)} results ({non_elim} ranked, {elim_count} eliminated)")
        if expected:
            print(f"  Expected: {expected} participants")
            if len(rows) != expected:
                print(f"  WARNING: mismatch! Missing {expected - len(rows)} entries")

        no_country = [r for r in rows if not r['country']]
        if no_country:
            print(f"  WARNING: {len(no_country)} entries without country code")
            for r in no_country:
                print(f"    - #{r['start_no']} {r['handler']} {r['dog']}")

        all_new_results.extend(rows)

    print(f"\n=== Total new results from OCR: {len(all_new_results)} ===")

    # Load existing CSV and merge
    csv_path = BASE_DIR / "awc2024_results.csv"
    fieldnames = [
        'competition', 'round_key', 'size', 'discipline', 'is_team_round',
        'rank', 'start_no', 'handler', 'dog', 'breed', 'country',
        'faults', 'refusals', 'time_faults', 'total_faults', 'time', 'speed',
        'eliminated', 'judge', 'sct', 'mct', 'course_length',
    ]

    existing_results = []
    if csv_path.exists():
        with open(csv_path, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                existing_results.append(row)

    # Remove old OCR entries for these rounds
    new_round_keys = {rk for rk in OCR_FILES.keys()}
    existing_filtered = [r for r in existing_results if r['round_key'] not in new_round_keys]
    print(f"Existing (non-OCR): {len(existing_filtered)}")

    # Convert new results for CSV
    for row in all_new_results:
        row['is_team_round'] = str(row['is_team_round'])
        row['eliminated'] = str(row['eliminated'])
        row['rank'] = '' if row['rank'] is None else row['rank']
        for field in ['faults', 'refusals', 'time_faults', 'total_faults', 'time', 'speed']:
            if row[field] is None:
                row[field] = ''

    merged = existing_filtered + all_new_results
    print(f"Merged total: {len(merged)}")

    with open(csv_path, 'w', newline='', encoding='utf-8') as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(merged)
    print(f"CSV updated: {csv_path}")

    json_path = BASE_DIR / "awc2024_results.json"
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(merged, f, ensure_ascii=False, indent=2)
    print(f"JSON updated: {json_path}")


if __name__ == '__main__':
    main()
