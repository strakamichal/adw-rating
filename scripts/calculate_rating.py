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
    "joawc_soawc_2025":            {"date": "2025-07-09", "tier": 1, "name": "JOAWC/SOAWC 2025"},
    "proseccup_2024":              {"date": "2024-01-19", "tier": 2, "name": "ProsecCup 2024"},
    "proseccup_2025":              {"date": "2025-01-17", "tier": 2, "name": "ProsecCup 2025"},
    "alpine_agility_open_2024":    {"date": "2024-06-07", "tier": 2, "name": "Alpine Agility Open 2024"},
    "alpine_agility_open_2025":    {"date": "2025-06-06", "tier": 2, "name": "Alpine Agility Open 2025"},
    "austrian_agility_open_2025":  {"date": "2025-06-13", "tier": 2, "name": "Austrian Agility Open 2025"},
    "border_collie_classic_2024":  {"date": "2024-07-26", "tier": 2, "name": "Border Collie Classic 2024"},
    "dutch_open_2025":             {"date": "2025-07-03", "tier": 2, "name": "Dutch Open 2025"},
    "fmbb_2024":                   {"date": "2024-04-25", "tier": 2, "name": "FMBB World Championship 2024"},
    "fmbb_2025":                   {"date": "2025-05-06", "tier": 2, "name": "FMBB World Championship 2025"},
    "helvetic_agility_masters_2025": {"date": "2025-08-15", "tier": 2, "name": "Helvetic Agility Masters 2025"},
    "hungarian_open_2024":         {"date": "2024-02-23", "tier": 2, "name": "Hungarian Open 2024"},
    "hungarian_open_2025":         {"date": "2025-02-21", "tier": 2, "name": "Hungarian Open 2025"},
    "midsummer_dog_sports_festival_2024": {"date": "2024-06-19", "tier": 2, "name": "Midsummer Dog Sports Festival 2024"},
    "midsummer_dog_sports_festival_2025": {"date": "2025-06-19", "tier": 2, "name": "Midsummer Dog Sports Festival 2025"},
    "nordic_agility_championship_2024": {"date": "2024-08-17", "tier": 2, "name": "Nordic Agility Championship 2024"},
    "nordic_agility_championship_2025": {"date": "2025-08-22", "tier": 2, "name": "Nordic Agility Championship 2025"},
    "norwegian_open_2024":         {"date": "2024-10-11", "tier": 2, "name": "Norwegian Open 2024"},
    "norwegian_open_2025":         {"date": "2025-10-10", "tier": 2, "name": "Norwegian Open 2025"},
    "slovenian_open_2025":         {"date": "2025-06-27", "tier": 2, "name": "Slovenian Open 2025"},
    "lotw_i_2025_2026":            {"date": "2025-11-01", "tier": 2, "name": "Lord of the Winter I. 2025/2026"},
    "lotw_ii_2025_2026":           {"date": "2025-12-06", "tier": 2, "name": "Lord of the Winter II. 2025/2026"},
    "lotw_iii_2025_2026":          {"date": "2026-02-07", "tier": 2, "name": "Lord of the Winter III. 2025/2026"},
}

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA_DIR = os.path.join(BASE_DIR, "data")
OUTPUT_DIR = os.path.join(BASE_DIR, "output")

MIN_FIELD_SIZE = 6
MIN_RUNS_FOR_RANKING = 5  # teams with fewer runs are excluded from output
# Include team rounds only when they represent an individual run
# (team ranking is derived from summed individual performances).
TEAM_DISCIPLINES_INCLUDED = {"Agility", "Jumping", "Final"}

# Tier weights: optional event importance boost passed into OpenSkill's
# weights parameter. Disabled by default to keep MVP logic simple.
ENABLE_TIER_WEIGHTING = False
TIER_WEIGHTS = {1: 2.0, 2: 1.5, 3: 1.0, 4: 0.7}

# Sigma decay: force sigma convergence after each run.
# PlackettLuce sigma barely decreases in large fields, so we manually
# decay it to simulate Glicko-2-like convergence.
SIGMA_DECAY = 0.95      # multiply sigma by this after each run
SIGMA_MIN = 1.5          # floor — sigma never goes below this

# Provisional status: uncertainty-driven, not score-driven.
PROVISIONAL_SIGMA_THRESHOLD = 4.0

# Skill tier distribution (per size, among teams shown in rankings).
ELITE_TOP_PERCENT = 0.02
CHAMPION_TOP_PERCENT = 0.10  # Elite + Champion
EXPERT_TOP_PERCENT = 0.30    # Elite + Champion + Expert

# Preferred size tab order in the UI.
SIZE_TAB_ORDER = ["Large", "Intermediate", "Medium", "Small"]

# Display formula: displayed_rating = BASE + SCALE * (mu - 3*sigma)
# This preserves OpenSkill's ordinal semantics while mapping to a user-friendly scale.
DISPLAY_BASE = 1000
DISPLAY_SCALE = 40


def displayed_rating(mu, sigma):
    return DISPLAY_BASE + DISPLAY_SCALE * (mu - 3.0 * sigma)


def is_provisional(sigma):
    return sigma >= PROVISIONAL_SIGMA_THRESHOLD


def _percentile_threshold(values, percentile):
    """Return value at percentile p (0..1) using deterministic nearest-rank index."""
    if not values:
        return float("inf")
    sorted_values = sorted(values)
    idx = int((len(sorted_values) - 1) * percentile)
    return sorted_values[idx]


def compute_tier_thresholds(all_ratings):
    """Compute per-size skill tier cutoffs from displayed rating percentiles."""
    thresholds_by_size = {}

    for size in sorted(all_ratings.keys()):
        ranked = [
            t for t in all_ratings[size].values()
            if t["num_runs"] >= MIN_RUNS_FOR_RANKING
        ]
        scores = [t["displayed_rating"] for t in ranked]

        thresholds_by_size[size] = {
            "elite_min": _percentile_threshold(scores, 1.0 - ELITE_TOP_PERCENT),
            "champion_min": _percentile_threshold(scores, 1.0 - CHAMPION_TOP_PERCENT),
            "expert_min": _percentile_threshold(scores, 1.0 - EXPERT_TOP_PERCENT),
        }

    return thresholds_by_size


def skill_tier_label(rating, thresholds):
    if rating >= thresholds["elite_min"]:
        return "Elite"
    if rating >= thresholds["champion_min"]:
        return "Champion"
    if rating >= thresholds["expert_min"]:
        return "Expert"
    return "Competitor"


def natural_sort_key(value):
    """Sort strings in human order: ..._2 before ..._10."""
    parts = re.split(r"(\d+)", value or "")
    key = []
    for part in parts:
        if part.isdigit():
            key.append(int(part))
        else:
            key.append(part)
    return key


def ordered_sizes(size_keys):
    """Sort size labels by preferred UI order, then alphabetically for unknown labels."""
    idx = {size: i for i, size in enumerate(SIZE_TAB_ORDER)}
    return sorted(size_keys, key=lambda s: (idx.get(s, len(idx)), s))


# ---------------------------------------------------------------------------
# Name normalization
# ---------------------------------------------------------------------------

# Manual dog name aliases: normalized_call_name -> canonical_call_name
DOG_ALIASES = {
    "sayonara": "seeya",
    "sayonara seeya": "seeya",
    "black swan": "chilli",
    "finrod frances": "cis",
    "pszenik": "psenik",
}

# Tokens frequently found in exports as technical suffixes, not real call names.
# Example: "SHEPWORLD I WANT IT ALL (cp)" where "(cp)" is metadata noise.
NON_CALL_SUFFIXES = {
    "cp",
}

# Manual registered name -> call name mapping (when data never provides
# the call name in parentheses/quotes for a given registered name)
REGISTERED_TO_CALL = {
    "night magic alfa fortuna": "beat",
    "olympia misty highland": "olinka",
    "frisky fantine z certovy kazatelny": "fanta",
    "shepworld i want it all": "katniss",
    "shepworld i'm on fire": "brant",
    "finrod frances wonderfull dream": "cis",
    "a3ch finrod frances wonderfull dream": "cis",
    "finrod frances wonderfull dream cis": "cis",
    "a3ch libby granting pleasure": "psenik",
    "libby granting pleasure": "psenik",
    "clever and fast of youwentis": "carrie",
    "clever and fastvof youwentis": "carrie",
    "never never land sayonara": "seeya",
    "black swan gates of heaven": "chilli",
    "galactic breez almondjoy": "twist",
    'a3ch flank "ray" ballarat': "ray",
    "a3ch farid fort fox": "shani",
}

# Manual registered-name typo fixes: normalized_variant -> canonical_registered_name
REGISTERED_NAME_ALIASES = {
    "clever and fastvof youwentis": "clever and fast of youwentis",
    "nnl sayonara": "never never land sayonara",
    "never never land s'sayonara": "never never land sayonara",
}

# Manual display form overrides for normalized call names.
CALL_NAME_DISPLAY = {
    "chilli": "Chilli",
    "cis": "Cis",
    "psenik": "Pšeník",
    "ray": "Ray",
}

# Manual handler aliases: normalized_handler -> canonical_normalized_handler
HANDLER_ALIASES = {
    "katka tercova": "katerina tercova",
}

# Manual handler display overrides: canonical_normalized_handler -> display name
HANDLER_DISPLAY_OVERRIDES = {
    "katerina tercova": "Kateřina Terčová",
    "golab iwona": "Iwona Gołąb",
    "petra vyplelova": "Petra Vyplelová",
}


# Characters that NFKD decomposition doesn't handle (single codepoints
# without a base+combining decomposition).
_EXTRA_TRANSLITERATION = str.maketrans({
    "ł": "l", "Ł": "L",
    "đ": "d", "Đ": "D",
    "ø": "o", "Ø": "O",
})


def strip_diacritics(s):
    """Remove diacritics: 'Diviš' -> 'Divis', 'Gołąb' -> 'Golab'."""
    s = s.translate(_EXTRA_TRANSLITERATION)
    nfkd = unicodedata.normalize("NFKD", s)
    return "".join(c for c in nfkd if not unicodedata.combining(c))


def normalize_registered_name(name):
    """Normalize registered name and fix known source typos."""
    normalized = re.sub(r"\s+", " ", name.strip().lower())
    return REGISTERED_NAME_ALIASES.get(normalized, normalized)


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
    normalized = " ".join(sorted(parts))
    return HANDLER_ALIASES.get(normalized, normalized)


def parse_dog_name(dog_name):
    """Parse dog name into (call_name, registered_name).

    Returns:
      call_name: normalized short name (lowercased, diacritics stripped)
      registered_name: normalized full registered name without call name suffix
                       (lowercased, diacritics stripped), or "" if only call name

    Examples:
      'Shepworld I'm On Fire (Brant)' -> ('brant', 'shepworld i'm on fire')
      'Finrod Frances "Cis"'          -> ('cis', 'finrod frances')
      'A3Ch Finrod Frances ...'       -> ('', 'a3ch finrod frances ...')
      'Day'                           -> ('day', '')
    """
    dog_name = strip_diacritics(dog_name).strip()
    # Normalize smart/curly quotes to ASCII
    dog_name = dog_name.replace("\u201C", '"').replace("\u201D", '"')
    dog_name = dog_name.replace("\u2018", "'").replace("\u2019", "'")

    # Try to extract from parentheses at end: "... (CallName)"
    match = re.search(r"\(([^)]+)\)\s*$", dog_name)
    if match:
        call = match.group(1).strip().lower()
        if call in NON_CALL_SUFFIXES:
            # Ignore technical suffixes and parse the base dog string.
            dog_name = dog_name[:match.start()].strip()
        else:
            call = DOG_ALIASES.get(call, call)
            reg = normalize_registered_name(dog_name[:match.start()])
            # Only keep registered name if it's longer than the call name
            if len(reg.split()) <= 2:
                reg = ""
            return call, reg

    # Try to extract from quotes: '... "CallName"'
    match = re.search(r'"([^"]+)"\s*$', dog_name)
    if match:
        call = match.group(1).strip().lower()
        if call in NON_CALL_SUFFIXES:
            dog_name = dog_name[:match.start()].strip()
        else:
            call = DOG_ALIASES.get(call, call)
            reg = normalize_registered_name(dog_name[:match.start()])
            if len(reg.split()) <= 2:
                reg = ""
            return call, reg

    # No explicit call name marker
    normalized = normalize_registered_name(dog_name)
    normalized = DOG_ALIASES.get(normalized, normalized)
    # Short name (1-2 words) → treat as call name, no registered name
    if len(normalized.split()) <= 2:
        return normalized, ""
    # Long name without marker → check manual registered-to-call mapping
    mapped_call = REGISTERED_TO_CALL.get(normalized)
    if mapped_call:
        return mapped_call, normalized
    # Otherwise treat as registered name only, no call name
    return "", normalized


def make_team_id(handler, dog):
    """Create a normalized team identity from handler and dog names."""
    h = normalize_handler(handler)
    if dog:
        call, reg = parse_dog_name(dog)
        # Prefer call name for ID; fall back to registered name
        d = call if call else reg
    else:
        d = ""
    return f"{h}|||{d}"


def _extract_call_name_from_handler_blob(handler_blob):
    """Extract call name from a combined handler+dog string.

    Supports both well-formed "... (Call)" and damaged "... (Call" tails.
    Returns empty string when extraction is not reliable.
    """
    if not handler_blob:
        return ""

    # Prefer the last complete "(...)" group anywhere in the string.
    all_parens = re.findall(r"\(([^)]+)\)", handler_blob)
    if all_parens:
        candidate = all_parens[-1]
    else:
        # Fallback for malformed inputs missing trailing ")".
        tail = re.search(r"\(([^()]*)\s*$", handler_blob)
        if not tail:
            return ""
        candidate = tail.group(1)

    candidate = re.sub(r"[^\w\s'\-’]", "", candidate, flags=re.UNICODE)
    candidate = re.sub(r"\s+", " ", candidate).strip()
    if not candidate:
        return ""
    # Call names are typically short; longer tails are likely parsing noise.
    if len(candidate.split()) > 3:
        return ""
    return candidate


# ---------------------------------------------------------------------------
# Data loading
# ---------------------------------------------------------------------------

def load_all_runs():
    """Load all CSV files and attach competition metadata.

    Team rounds are included only for individual-run disciplines
    (Agility/Jumping/Final). Aggregate team-only rows (e.g. Unknown) are skipped.
    """
    runs = []
    csv_files = sorted(glob.glob(os.path.join(DATA_DIR, "*", "*_results.csv")))
    skipped_no_identity = 0
    skipped_team_rounds = 0
    recovered_handler_from_id = 0
    recovered_identity_from_start_no = 0

    for filepath in csv_files:
        comp_dir = os.path.basename(os.path.dirname(filepath))
        if comp_dir == "_downloads":
            continue
        if comp_dir not in COMPETITIONS:
            print(f"WARNING: unknown competition dir '{comp_dir}', skipping")
            continue

        comp_meta = COMPETITIONS[comp_dir]

        with open(filepath, newline="", encoding="utf-8") as f:
            rows = list(csv.DictReader(f))

        # File-local recovery map: handler_id -> best known handler text.
        # We only trust rows that already have a dog in a dedicated dog column.
        handler_by_id = {}
        identity_by_start_no = {}
        ambiguous_start_no = set()
        for row in rows:
            hid = row.get("handler_id", "").strip()
            h = row.get("handler", "").strip()
            d = row.get("dog", "").strip()
            if not hid or not h or not d:
                continue
            current = handler_by_id.get(hid)
            if current is None or len(h.split()) < len(current.split()):
                handler_by_id[hid] = h

            start_no = row.get("start_no", "").strip()
            if start_no:
                tid = make_team_id(h, d)
                existing = identity_by_start_no.get(start_no)
                if existing and existing["team_id"] != tid:
                    ambiguous_start_no.add(start_no)
                else:
                    identity_by_start_no[start_no] = {"handler": h, "dog": d, "team_id": tid}

        for start_no in ambiguous_start_no:
            identity_by_start_no.pop(start_no, None)

        for row in rows:
            is_team_round = row.get("is_team_round", "").strip().lower() == "true"
            discipline = row.get("discipline", "").strip()
            if is_team_round and discipline not in TEAM_DISCIPLINES_INCLUDED:
                skipped_team_rounds += 1
                continue

            raw_handler = row.get("handler", "").strip()
            handler = raw_handler
            dog = row.get("dog", "").strip()
            handler_id = row.get("handler_id", "").strip()
            start_no = row.get("start_no", "").strip()

            # Recover handler if source row has broken "handler+dog" blob.
            mapped_handler = handler_by_id.get(handler_id, "")
            if mapped_handler and handler != mapped_handler:
                handler = mapped_handler
                recovered_handler_from_id += 1

            # Strong recovery path: copy full identity from the same start number
            # in the same file when available (AWC exports often have this issue).
            start_identity = identity_by_start_no.get(start_no)
            if start_identity and (not handler or not dog):
                if handler != start_identity["handler"] or dog != start_identity["dog"]:
                    handler = start_identity["handler"]
                    dog = start_identity["dog"]
                    recovered_identity_from_start_no += 1

            # If dog is missing, try extracting call name from the handler blob.
            if not dog and raw_handler:
                call_name = _extract_call_name_from_handler_blob(raw_handler)
                if call_name:
                    # Accept fallback only when handler came from trusted map,
                    # or when raw handler looks like a plain personal name.
                    if mapped_handler or len(raw_handler.split()) <= 4:
                        dog = call_name

            if not handler or not dog:
                skipped_no_identity += 1
                continue

            team_id = make_team_id(handler, dog)

                # Parse rank
            rank_str = row.get("rank", "").strip()
            try:
                rank = int(rank_str)
            except (ValueError, KeyError):
                rank = None

            eliminated = row.get("eliminated", "") == "True"
            # Treat DIS/DSQ rank as eliminated even if eliminated flag is False
            if rank is None and rank_str.upper() in ("DIS", "DSQ", "NFC", "RET", "WD"):
                eliminated = True

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
    if skipped_team_rounds:
        print(f"Skipped {skipped_team_rounds} non-individual team-round runs")
    if recovered_handler_from_id:
        print(f"Recovered {recovered_handler_from_id} handler names via handler_id map")
    if recovered_identity_from_start_no:
        print(f"Recovered {recovered_identity_from_start_no} identities via start_no map")
    print(f"Loaded {len(runs)} individual runs from {len(csv_files)} files")

    # --- Fuzzy dog name merging ---
    # For each handler, find dog name variants that should be the same dog.
    # E.g., "day" and "daylight neverending force" for the same handler.
    runs = _merge_dog_variants(runs)

    return runs


def _merge_dog_variants(runs):
    """Merge team_ids where the same handler has multiple dog name variants
    that refer to the same dog.

    Uses two strategies:
    1. Call name prefix: short call name (1-2 words) matches the start of a
       longer dog name part for the same handler.
    2. Registered name overlap: collect (handler, registered_name) from all runs.
       If two team_ids for the same handler share a similar registered name,
       merge the one without a call name into the one with a call name.
    """
    # --- Collect per-handler dog info from runs ---
    # handler -> set of dog_id parts (from team_id)
    handler_dogs = defaultdict(set)
    # (handler, dog_id) -> set of registered names seen
    handler_dog_regnames = defaultdict(set)

    for run in runs:
        h, d = run["team_id"].split("|||", 1)
        handler_dogs[h].add(d)
        # Parse the raw dog field to get registered name
        raw_dog = run.get("dog", "").strip()
        if raw_dog:
            _, reg = parse_dog_name(raw_dog)
            if reg:
                handler_dog_regnames[(h, d)].add(reg)

    merge_map = {}

    for handler in sorted(handler_dogs.keys()):
        dogs = sorted(handler_dogs[handler], key=lambda d: (len(d.split()), d))
        if len(dogs) < 2:
            continue

        # Strategy 1: call name prefix match
        for i, short in enumerate(dogs):
            short_words = short.split()
            if len(short_words) > 2 or not short:
                continue
            for long in dogs[i + 1:]:
                long_words = long.split()
                if len(long_words) <= len(short_words):
                    continue
                if long_words[0].startswith(short_words[0]):
                    long_tid = f"{handler}|||{long}"
                    short_tid = f"{handler}|||{short}"
                    if long_tid not in merge_map:
                        merge_map[long_tid] = short_tid

        # Strategy 2: registered name overlap
        # For each pair of dog_ids, check if their registered names overlap
        for i, d1 in enumerate(dogs):
            for d2 in dogs[i + 1:]:
                tid1 = f"{handler}|||{d1}"
                tid2 = f"{handler}|||{d2}"
                # Skip if already merged
                if tid1 in merge_map or tid2 in merge_map:
                    continue
                regs1 = handler_dog_regnames.get((handler, d1), set())
                regs2 = handler_dog_regnames.get((handler, d2), set())
                if not regs1 or not regs2:
                    continue
                if _registered_names_match(regs1, regs2):
                    # Prefer the team_id with the shorter dog part (call name)
                    if len(d1.split()) <= len(d2.split()):
                        merge_map[tid2] = tid1
                    else:
                        merge_map[tid1] = tid2

        # Strategy 3: call name appears as a word in registered name
        # e.g., call="crocodile" matches reg="a3ch crocodile yabalute"
        for i, d1 in enumerate(dogs):
            w1 = d1.split()
            if len(w1) != 1 or not d1:
                continue  # only single-word call names
            for d2 in dogs:
                if d1 == d2:
                    continue
                tid1 = f"{handler}|||{d1}"
                tid2 = f"{handler}|||{d2}"
                if tid1 in merge_map or tid2 in merge_map:
                    continue
                # Check if d1 (call name) appears as a word in the registered
                # names associated with d2
                regs2 = handler_dog_regnames.get((handler, d2), set())
                for reg in regs2:
                    if d1 in reg.split():
                        merge_map[tid2] = tid1
                        break

    # Resolve transitive merges: A->B, B->C => A->C
    for tid in list(merge_map):
        target = merge_map[tid]
        while target in merge_map:
            target = merge_map[target]
        merge_map[tid] = target

    if merge_map:
        merged_count = 0
        for run in runs:
            if run["team_id"] in merge_map:
                run["team_id"] = merge_map[run["team_id"]]
                merged_count += 1
        print(f"Merged {merged_count} runs across {len(merge_map)} dog name variants")

    return runs


def _registered_names_match(regs1, regs2):
    """Check if two sets of registered names likely refer to the same dog.

    Matches if any pair shares >=3 consecutive words starting from the
    beginning of at least one name (prefix match). This avoids false
    positives from shared kennel name suffixes like "from Malibo Land".
    """
    for r1 in regs1:
        w1 = r1.split()
        for r2 in regs2:
            w2 = r2.split()
            # Check prefix overlap: words matching from start of both names
            prefix_match = 0
            for a, b in zip(w1, w2):
                if a == b:
                    prefix_match += 1
                else:
                    break
            if prefix_match >= 3:
                return True

            # Also check if one name starts from the beginning of the other
            # at some offset (e.g., "A3Ch Finrod Frances..." vs "Finrod Frances...")
            for offset in range(1, min(3, len(w1))):
                match = 0
                for a, b in zip(w1[offset:], w2):
                    if a == b:
                        match += 1
                    else:
                        break
                if match >= 3:
                    return True
            for offset in range(1, min(3, len(w2))):
                match = 0
                for a, b in zip(w1, w2[offset:]):
                    if a == b:
                        match += 1
                    else:
                        break
                if match >= 3:
                    return True
    return False


# ---------------------------------------------------------------------------
# Team profile aggregation
# ---------------------------------------------------------------------------

def build_team_profiles(runs):
    """Aggregate best metadata for each team across all runs.

    Returns dict: team_id -> {
        handler_display: str,   # canonical "First Last"
        call_name: str,         # short call name (e.g. "Day")
        registered_name: str,   # full registered name (e.g. "Daylight Neverending Force")
        dog_display: str,       # combined for display
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
        call_name, registered_name = _best_dog_names(data["dogs"])
        dog_display = _format_dog_display(call_name, registered_name)
        profiles[tid] = {
            "handler_display": _best_handler_display(data["handlers"]),
            "call_name": call_name,
            "registered_name": registered_name,
            "dog_display": dog_display,
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
        chosen = f"{first} {last}"
        return HANDLER_DISPLAY_OVERRIDES.get(normalize_handler(chosen), chosen)

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
    chosen = top_candidates[0]
    return HANDLER_DISPLAY_OVERRIDES.get(normalize_handler(chosen), chosen)


def _best_dog_names(dogs):
    """Extract the best call name and registered name from all dog string variants.

    Returns (call_name, registered_name) — both in original case (best available).
    """
    call_names = defaultdict(int)   # normalized -> count
    reg_names = defaultdict(int)    # normalized -> count
    call_display = {}               # normalized -> best display form
    reg_display = {}                # normalized -> best display form

    for raw in dogs:
        raw = raw.strip()
        if not raw:
            continue
        call, reg = parse_dog_name(raw)
        if call:
            call_names[call] += 1
            # Keep the display form with most diacritics
            existing = call_display.get(call, "")
            raw_call = _extract_raw_call_name(raw)
            if raw_call and (not existing or _diacritics_score(raw_call) > _diacritics_score(existing)):
                call_display[call] = raw_call
        if reg:
            reg_names[reg] += 1
            raw_reg = _extract_raw_registered_name(raw)
            if raw_reg:
                existing = reg_display.get(reg, "")
                if not existing or _diacritics_score(raw_reg) > _diacritics_score(existing):
                    reg_display[reg] = raw_reg

    # Pick the most common call name
    best_call = ""
    best_call_display = ""
    if call_names:
        best_call = max(call_names, key=call_names.get)
        best_call_display = CALL_NAME_DISPLAY.get(
            best_call,
            call_display.get(best_call, best_call.title()),
        )

    # Pick the longest registered name (most complete)
    best_reg = ""
    best_reg_display = ""
    if reg_names:
        best_reg = max(reg_names, key=lambda r: len(r))
        best_reg_display = reg_display.get(best_reg, best_reg)

    return best_call_display, best_reg_display


def _extract_raw_call_name(dog_str):
    """Extract call name in original case from a dog string."""
    match = re.search(r"\(([^)]+)\)\s*$", dog_str)
    if match:
        candidate = match.group(1).strip()
        if candidate.lower() not in NON_CALL_SUFFIXES:
            return candidate
        # Technical suffix — fall through to parse the base string
        dog_str = dog_str[:match.start()].strip()
    match = re.search(r'"([^"]+)"\s*$', dog_str)
    if match:
        candidate = match.group(1).strip()
        if candidate.lower() not in NON_CALL_SUFFIXES:
            return candidate
        dog_str = dog_str[:match.start()].strip()
    # If it's a short name (1-2 words), it is the call name
    if len(dog_str.split()) <= 2:
        return dog_str.strip()
    return ""


def _extract_raw_registered_name(dog_str):
    """Extract registered name in original case from a dog string."""
    # Strip call name suffix
    reg = re.sub(r'\s*\([^)]+\)\s*$', '', dog_str).strip()
    reg = re.sub(r'\s*"[^"]+"\s*$', '', reg).strip()
    if len(reg.split()) > 2:
        return reg
    return ""


def _diacritics_score(s):
    """Count non-ASCII characters — prefer strings with original diacritics."""
    return sum(1 for c in s if ord(c) > 127)


def _format_dog_display(call_name, registered_name):
    """Format dog display string from call name and registered name."""
    if registered_name and call_name:
        return f"{registered_name} ({call_name})"
    if registered_name:
        return registered_name
    if call_name:
        return call_name
    return ""


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

            for round_key in sorted(round_runs.keys(), key=natural_sort_key):
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

                # Optional tier weighting via OpenSkill's native "weights" parameter.
                tier = entries[0]["comp_tier"]
                tier_weight = TIER_WEIGHTS.get(tier, 1.0)
                weights = None
                if ENABLE_TIER_WEIGHTING and tier_weight != 1.0:
                    weights = [[tier_weight] for _ in entry_order]

                result = model.rate(teams, ranks=ranks, weights=weights)

                # Apply sigma decay
                for i, tid in enumerate(entry_order):
                    new_rating = result[i][0]

                    # Force sigma convergence
                    new_rating.sigma = max(SIGMA_MIN, new_rating.sigma * SIGMA_DECAY)
                    team_ratings[tid] = new_rating

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
                "handler": profile.get("handler_display", ""),
                "dog": profile.get("dog_display", ""),
                "call_name": profile.get("call_name", ""),
                "registered_name": profile.get("registered_name", ""),
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

    # Add per-size percentile skill tiers + provisional badge flag.
    tier_thresholds = compute_tier_thresholds(all_ratings)
    for size in sorted(all_ratings.keys()):
        thresholds = tier_thresholds[size]
        for team in all_ratings[size].values():
            team["skill_tier"] = skill_tier_label(team["displayed_rating"], thresholds)
            team["provisional"] = is_provisional(team["sigma"])

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
            "rank", "handler", "call_name", "registered_name", "size", "country",
            "mu", "sigma", "displayed_rating",
            "tier", "provisional", "num_runs", "last_competition",
        ])

        for size in sorted(all_ratings.keys()):
            sorted_teams = sorted(
                (t for t in all_ratings[size].values() if t["num_runs"] >= MIN_RUNS_FOR_RANKING),
                key=lambda x: -x["displayed_rating"],
            )
            for i, team in enumerate(sorted_teams, 1):
                writer.writerow([
                    i,
                    team["handler"],
                    team["call_name"],
                    team["registered_name"],
                    size,
                    team["country"],
                    round(team["mu"], 4),
                    round(team["sigma"], 4),
                    team["displayed_rating"],
                    team.get("skill_tier", "Competitor"),
                    str(team.get("provisional", False)).lower(),
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

    sizes = ordered_sizes(all_ratings.keys())

    # Build table rows per size
    tables = {}
    countries_by_size = {}
    for size in sizes:
        sorted_teams = sorted(
            (t for t in all_ratings[size].values() if t["num_runs"] >= MIN_RUNS_FOR_RANKING),
            key=lambda x: -x["displayed_rating"],
        )
        countries_by_size[size] = sorted({t["country"] for t in sorted_teams if t["country"]})
        rows = []
        for i, team in enumerate(sorted_teams, 1):
            rating = team["displayed_rating"]
            tl = team.get("skill_tier", "Competitor")
            provisional_badge = ""
            if team.get("provisional", False):
                provisional_badge = " <span class='prov-badge'>PROV</span>"
            handler_cell = f"{_esc(team['handler'])}{provisional_badge}"
            # Dog cell: call name bold, registered name small
            call = _esc(team.get("call_name", ""))
            reg = _esc(team.get("registered_name", ""))
            if reg and call:
                dog_cell = f"<strong>{call}</strong><br><span class='reg-name'>{reg}</span>"
            elif call:
                dog_cell = f"<strong>{call}</strong>"
            elif reg:
                dog_cell = f"<span class='reg-name'>{reg}</span>"
            else:
                dog_cell = ""
            rows.append(
                f"<tr class='tier-{tl.lower()}'>"
                f"<td>{i}</td>"
                f"<td>{handler_cell}</td>"
                f"<td>{dog_cell}</td>"
                f"<td>{_esc(team['country'])}</td>"
                f"<td class='num'>{rating:.0f}</td>"
                f"<td class='num'>{team['num_runs']}</td>"
                f"<td>{_esc(team['last_comp'])}</td>"
                f"</tr>"
            )
        tables[size] = "\n".join(rows)

    # Tab buttons
    tab_buttons = []
    for i, size in enumerate(sizes):
        count = sum(1 for t in all_ratings[size].values() if t["num_runs"] >= MIN_RUNS_FOR_RANKING)
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
                        <th onclick="sortTable('table-{size}', 5, 'num')">Runs</th>
                        <th onclick="sortTable('table-{size}', 6, 'str')">Last Competition</th>
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
.reg-name {{ font-size: 11px; color: #888; }}
.prov-badge {{ display: inline-block; margin-left: 6px; padding: 1px 4px; border-radius: 4px; background: #eef2ff; color: #334155; font-size: 10px; font-weight: 700; letter-spacing: 0.2px; vertical-align: middle; }}
.legend {{ display: flex; gap: 16px; margin-bottom: 16px; flex-wrap: wrap; font-size: 13px; }}
.legend-item {{ display: flex; align-items: center; gap: 4px; }}
.legend-color {{ width: 12px; height: 12px; border-radius: 2px; }}
.filters {{ display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }}
.search-box {{ margin-bottom: 0; }}
.search-box input {{ padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 14px; width: 300px; max-width: 100%; }}
.country-box select {{ padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 14px; background: #fff; min-width: 180px; }}
</style>
</head>
<body>
<h1>ADW Rating — Dry Run</h1>
<p class="subtitle">OpenSkill (Plackett-Luce) · {len(sizes)} size categories · percentile skill tiers per size</p>

<div class="legend">
    <div class="legend-item"><div class="legend-color" style="background:#f59e0b"></div> Elite (top 2% per size)</div>
    <div class="legend-item"><div class="legend-color" style="background:#8b5cf6"></div> Champion (next 8%)</div>
    <div class="legend-item"><div class="legend-color" style="background:#3b82f6"></div> Expert (next 20%)</div>
    <div class="legend-item"><div class="legend-color" style="background:#10b981"></div> Competitor (remaining)</div>
    <div class="legend-item"><span class="prov-badge">PROV</span> Provisional (sigma ≥ {PROVISIONAL_SIGMA_THRESHOLD})</div>
</div>

<div class="filters">
<div class="search-box">
    <input type="text" id="search" placeholder="Search handler or dog..." oninput="filterRows()">
</div>
<div class="country-box">
    <select id="country-filter" onchange="filterRows()"></select>
</div>
</div>

<div class="tabs">
    {"".join(tab_buttons)}
</div>

{"".join(tab_contents)}

<script>
let currentTab = '{sizes[0]}';
const countryOptionsBySize = {countries_by_size};

function showTab(size) {{
    document.querySelectorAll('.tab-content').forEach(el => el.style.display = 'none');
    document.querySelectorAll('.tab-btn').forEach(el => el.classList.remove('active'));
    document.getElementById('tab-' + size).style.display = 'block';
    event.target.closest('.tab-btn').classList.add('active');
    currentTab = size;
    updateCountryFilterOptions();
    filterRows();
}}

function updateCountryFilterOptions() {{
    const select = document.getElementById('country-filter');
    if (!select) return;

    const previous = select.value || '';
    const options = countryOptionsBySize[currentTab] || [];

    select.innerHTML = '';
    const allOption = document.createElement('option');
    allOption.value = '';
    allOption.textContent = 'All countries';
    select.appendChild(allOption);

    options.forEach(country => {{
        const option = document.createElement('option');
        option.value = country;
        option.textContent = country;
        select.appendChild(option);
    }});

    if (previous && options.includes(previous)) {{
        select.value = previous;
    }} else {{
        select.value = '';
    }}
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
    const country = document.getElementById('country-filter').value;
    const table = document.getElementById('table-' + currentTab);
    if (!table) return;
    const rows = table.querySelectorAll('tbody tr');
    rows.forEach(row => {{
        const text = row.textContent.toLowerCase();
        const rowCountry = row.cells[3] ? row.cells[3].textContent.trim() : '';
        const matchesText = text.includes(q);
        const matchesCountry = !country || rowCountry === country;
        row.style.display = (matchesText && matchesCountry) ? '' : 'none';
    }});
}}

updateCountryFilterOptions();
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
