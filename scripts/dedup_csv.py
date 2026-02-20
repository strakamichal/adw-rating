#!/usr/bin/env python3
"""Deduplicate competition result CSV files.

Groups rows by (round_key, handler, dog) and keeps the best result per group:
- Prefer rows with actual results (non-empty total_faults AND time) over empty ones
- Among rows with results, prefer lower numeric rank
- Among eliminated/empty rows, keep just one

Output is sorted by round_key, then numeric rank (empty ranks at end), then handler.
"""

import csv
import sys
from pathlib import Path


def has_result(row):
    """Return True if the row has actual results (non-empty total_faults AND time)."""
    return bool(row["total_faults"].strip()) and bool(row["time"].strip())


def sort_key(row):
    """Sort key: round_key, numeric rank (empty at end), handler."""
    rank = row["rank"].strip()
    try:
        rank_num = int(rank)
    except (ValueError, TypeError):
        rank_num = 999999
    return (row["round_key"], rank_num, row["handler"])


def pick_best(rows):
    """From a group of duplicate rows, pick the best one."""
    if len(rows) == 1:
        return rows[0]

    with_results = [r for r in rows if has_result(r)]
    if not with_results:
        return rows[0]

    def rank_value(r):
        try:
            return int(r["rank"])
        except (ValueError, TypeError):
            return 999999

    with_results.sort(key=rank_value)
    return with_results[0]


def dedup_file(filepath):
    """Deduplicate a single CSV file in-place."""
    path = Path(filepath)
    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        fieldnames = reader.fieldnames
        rows = list(reader)

    original_count = len(rows)

    # Group by (round_key, handler, dog)
    groups = {}
    for row in rows:
        key = (row["round_key"], row["handler"], row["dog"])
        groups.setdefault(key, []).append(row)

    # Pick best from each group
    deduped = [pick_best(group) for group in groups.values()]
    deduped.sort(key=sort_key)

    removed = original_count - len(deduped)

    # Write back
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(deduped)

    print(f"{path.name}: {original_count} -> {len(deduped)} rows ({removed} duplicates removed)")
    return original_count, len(deduped), removed


def main():
    if len(sys.argv) < 2:
        print("Usage: dedup_csv.py <file1.csv> [file2.csv ...]", file=sys.stderr)
        sys.exit(1)

    total_removed = 0
    for filepath in sys.argv[1:]:
        _, _, removed = dedup_file(filepath)
        total_removed += removed

    if len(sys.argv) > 2:
        print(f"\nTotal duplicates removed: {total_removed}")


if __name__ == "__main__":
    main()
