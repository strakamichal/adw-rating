#!/usr/bin/env python3
"""
Final live leaderboard variant with one public rating number.

Design:
- use only recent runs (configured window from latest competition date in dataset),
- require minimum run count inside the same live window,
- output one leaderboard with one rating column.

Outputs:
  - output/ratings_live_final.csv
  - output/ratings_live_final.html
"""

import csv
import os
from collections import defaultdict
from datetime import datetime, timedelta

from openskill.models import PlackettLuce

import calculate_rating as base


# ---------------------------------------------------------------------------
# Live config
# ---------------------------------------------------------------------------

LIVE_WINDOW_DAYS = 913  # 2.5 years
MIN_RUNS_FOR_LIVE_RANKING = 5
LIVE_SIGMA_DECAY = 0.99
LIVE_PROVISIONAL_SIGMA_THRESHOLD = 7.8

# Major-event boost (AWC/EO/JOAWC tier-1 comps).
# Keep this moderate to avoid over-amplifying single events.
ENABLE_MAJOR_EVENT_WEIGHTING = True
MAJOR_EVENT_WEIGHT = 1.20

# Public rating formula:
# rating = DISPLAY_BASE + DISPLAY_SCALE * (mu - RATING_SIGMA_MULTIPLIER * sigma)
RATING_SIGMA_MULTIPLIER = 1.0
DISPLAY_BASE = base.DISPLAY_BASE
DISPLAY_SCALE = base.DISPLAY_SCALE

# Podium boost: quality post-adjustment applied to public rating.
# quality_score = 0.45*clean_pct + 0.35*top10_pct + 0.20*min(100, top3_pct*3)
# factor = PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE * clamp01(quality_score / PODIUM_BOOST_TARGET)
ENABLE_PODIUM_BOOST = True
PODIUM_BOOST_BASE = 0.82
PODIUM_BOOST_RANGE = 0.18
PODIUM_BOOST_TARGET = 70.0


def _parse_date(date_str):
    return datetime.strptime(date_str, "%Y-%m-%d").date()


def _percentile_threshold(values, percentile):
    if not values:
        return float("inf")
    sorted_values = sorted(values)
    idx = int((len(sorted_values) - 1) * percentile)
    return sorted_values[idx]


def live_rating(mu, sigma):
    return DISPLAY_BASE + DISPLAY_SCALE * (mu - RATING_SIGMA_MULTIPLIER * sigma)


def podium_boost_factor(clean_pct, top3_pct, top10_pct):
    top3_signal = min(100.0, top3_pct * 3.0)
    quality_score = (0.45 * clean_pct) + (0.35 * top10_pct) + (0.20 * top3_signal)
    quality_norm = max(0.0, min(1.0, quality_score / PODIUM_BOOST_TARGET))
    return PODIUM_BOOST_BASE + (PODIUM_BOOST_RANGE * quality_norm)


def is_live_provisional(sigma):
    return sigma >= LIVE_PROVISIONAL_SIGMA_THRESHOLD


def _compute_tier_thresholds(all_ratings):
    thresholds_by_size = {}
    for size in sorted(all_ratings.keys()):
        ranked = [
            team for team in all_ratings[size].values()
            if team["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING
        ]
        scores = [team["rating"] for team in ranked]
        thresholds_by_size[size] = {
            "elite_min": _percentile_threshold(scores, 1.0 - base.ELITE_TOP_PERCENT),
            "champion_min": _percentile_threshold(scores, 1.0 - base.CHAMPION_TOP_PERCENT),
            "expert_min": _percentile_threshold(scores, 1.0 - base.EXPERT_TOP_PERCENT),
        }
    return thresholds_by_size


def calculate_live_ratings(runs, profiles):
    """Calculate one live rating from runs inside the configured time window."""
    if not runs:
        return {}, None, None

    latest_date = max(_parse_date(run["comp_date"]) for run in runs)
    cutoff_date = latest_date - timedelta(days=LIVE_WINDOW_DAYS)
    live_runs = [run for run in runs if _parse_date(run["comp_date"]) >= cutoff_date]

    by_size = defaultdict(list)
    for run in live_runs:
        by_size[run["size"]].append(run)

    all_ratings = {}

    for size in sorted(by_size.keys()):
        size_runs = by_size[size]
        print(f"\n--- {size} ({len(size_runs)} live-window runs) ---")

        model = PlackettLuce()
        team_ratings = {}
        team_stats = {}

        comp_runs = defaultdict(list)
        for run in size_runs:
            comp_runs[run["comp_dir"]].append(run)

        sorted_comps = sorted(comp_runs.keys(), key=lambda c: base.COMPETITIONS[c]["date"])

        for comp_dir in sorted_comps:
            comp_name = base.COMPETITIONS[comp_dir]["name"]

            round_runs = defaultdict(list)
            for run in comp_runs[comp_dir]:
                round_runs[run["round_key"]].append(run)

            for round_key in sorted(round_runs.keys(), key=base.natural_sort_key):
                entries = round_runs[round_key]

                seen = set()
                unique_entries = []
                for entry in entries:
                    if entry["team_id"] not in seen:
                        seen.add(entry["team_id"])
                        unique_entries.append(entry)
                entries = unique_entries

                if len(entries) < base.MIN_FIELD_SIZE:
                    continue

                clean = [e for e in entries if not e["eliminated"] and e["rank"] is not None]
                elim = [e for e in entries if e["eliminated"]]
                clean.sort(key=lambda e: e["rank"])

                ranked_entries = []
                for idx, entry in enumerate(clean):
                    ranked_entries.append((entry, idx + 1))

                last_rank = len(clean) + 1
                for entry in elim:
                    ranked_entries.append((entry, last_rank))

                if len(ranked_entries) < base.MIN_FIELD_SIZE:
                    continue

                teams = []
                ranks = []
                entry_order = []

                for entry, rank in ranked_entries:
                    team_id = entry["team_id"]
                    if team_id not in team_ratings:
                        team_ratings[team_id] = model.rating()
                        team_stats[team_id] = {
                            "num_runs": 0,
                            "clean_runs": 0,
                            "top3_runs": 0,
                            "top10_runs": 0,
                            "last_comp": "",
                            "last_comp_date": "",
                        }

                    teams.append([team_ratings[team_id]])
                    ranks.append(rank)
                    entry_order.append(team_id)

                    team_stats[team_id]["num_runs"] += 1
                    if not entry["eliminated"] and entry["rank"] is not None:
                        team_stats[team_id]["clean_runs"] += 1
                        if rank <= 3:
                            team_stats[team_id]["top3_runs"] += 1
                        if rank <= 10:
                            team_stats[team_id]["top10_runs"] += 1
                    if entry["comp_date"] >= team_stats[team_id]["last_comp_date"]:
                        team_stats[team_id]["last_comp"] = comp_name
                        team_stats[team_id]["last_comp_date"] = entry["comp_date"]

                tier = entries[0]["comp_tier"]
                tier_weight = MAJOR_EVENT_WEIGHT if (ENABLE_MAJOR_EVENT_WEIGHTING and tier == 1) else 1.0
                weights = None
                if tier_weight != 1.0:
                    weights = [[tier_weight] for _ in entry_order]

                result = model.rate(teams, ranks=ranks, weights=weights)

                for idx, team_id in enumerate(entry_order):
                    new_rating = result[idx][0]
                    new_rating.sigma = max(base.SIGMA_MIN, new_rating.sigma * LIVE_SIGMA_DECAY)
                    team_ratings[team_id] = new_rating

        size_results = {}
        for team_id, rating in team_ratings.items():
            stats = team_stats[team_id]
            profile = profiles.get(team_id, {})
            num_runs = stats["num_runs"]
            clean_pct = round((stats["clean_runs"] / num_runs) * 100.0, 1) if num_runs else 0.0
            top3_pct = round((stats["top3_runs"] / num_runs) * 100.0, 1) if num_runs else 0.0
            top10_pct = round((stats["top10_runs"] / num_runs) * 100.0, 1) if num_runs else 0.0
            rating_base = live_rating(rating.mu, rating.sigma)
            quality_factor = podium_boost_factor(clean_pct, top3_pct, top10_pct) if ENABLE_PODIUM_BOOST else 1.0
            size_results[team_id] = {
                "mu": rating.mu,
                "sigma": rating.sigma,
                "rating": round(rating_base * quality_factor, 1),
                "rating_base": round(rating_base, 1),
                "quality_factor": round(quality_factor, 4),
                "handler": profile.get("handler_display", ""),
                "dog": profile.get("dog_display", ""),
                "call_name": profile.get("call_name", ""),
                "registered_name": profile.get("registered_name", ""),
                "country": profile.get("country", ""),
                "num_runs": num_runs,
                "clean_runs": stats["clean_runs"],
                "top3_runs": stats["top3_runs"],
                "top10_runs": stats["top10_runs"],
                "clean_pct": clean_pct,
                "top3_pct": top3_pct,
                "top10_pct": top10_pct,
                "last_comp": stats["last_comp"],
            }

        sorted_teams = sorted(size_results.items(), key=lambda x: -x[1]["rating"])
        print(f"  {len(sorted_teams)} unique teams rated")
        if sorted_teams:
            top = sorted_teams[0][1]
            print(f"  Top: {top['handler']} / {top['dog']} ({top['country']}) — {top['rating']}")

        all_ratings[size] = size_results

    tier_thresholds = _compute_tier_thresholds(all_ratings)
    for size in sorted(all_ratings.keys()):
        thresholds = tier_thresholds[size]
        for team in all_ratings[size].values():
            team["skill_tier"] = base.skill_tier_label(team["rating"], thresholds)
            team["provisional"] = is_live_provisional(team["sigma"])

    return all_ratings, cutoff_date, latest_date


def write_csv_live(all_ratings):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_live_final.csv")

    with open(outpath, "w", newline="", encoding="utf-8") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow([
            "rank", "handler", "call_name", "registered_name", "size", "country",
            "mu", "sigma", "rating",
            "tier", "provisional", "num_runs",
            "clean_pct", "top3_pct", "top10_pct",
            "last_competition",
        ])

        for size in sorted(all_ratings.keys()):
            sorted_teams = sorted(
                (team for team in all_ratings[size].values() if team["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING),
                key=lambda x: -x["rating"],
            )
            for rank, team in enumerate(sorted_teams, 1):
                writer.writerow([
                    rank,
                    team["handler"],
                    team["call_name"],
                    team["registered_name"],
                    size,
                    team["country"],
                    round(team["mu"], 4),
                    round(team["sigma"], 4),
                    team["rating"],
                    team.get("skill_tier", "Competitor"),
                    str(team.get("provisional", False)).lower(),
                    team["num_runs"],
                    team["clean_pct"],
                    team["top3_pct"],
                    team["top10_pct"],
                    team["last_comp"],
                ])

    print(f"\nCSV written to {outpath}")


def write_html_live(all_ratings, cutoff_date, latest_date):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_live_final.html")

    sizes = base.ordered_sizes(all_ratings.keys())
    tables = {}
    countries_by_size = {}

    for size in sizes:
        sorted_teams = sorted(
            (team for team in all_ratings[size].values() if team["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING),
            key=lambda x: -x["rating"],
        )
        countries_by_size[size] = sorted({team["country"] for team in sorted_teams if team["country"]})
        rows = []
        for rank, team in enumerate(sorted_teams, 1):
            provisional_badge = ""
            if team.get("provisional", False):
                provisional_badge = " <span class='prov-badge'>PROV</span>"
            handler_cell = f"{base._esc(team['handler'])}{provisional_badge}"

            call = base._esc(team.get("call_name", ""))
            reg = base._esc(team.get("registered_name", ""))
            if reg and call:
                dog_cell = f"<strong>{call}</strong><br><span class='reg-name'>{reg}</span>"
            elif call:
                dog_cell = f"<strong>{call}</strong>"
            elif reg:
                dog_cell = f"<span class='reg-name'>{reg}</span>"
            else:
                dog_cell = ""

            rows.append(
                f"<tr class='tier-{team.get('skill_tier', 'Competitor').lower()}'>"
                f"<td>{rank}</td>"
                f"<td>{handler_cell}</td>"
                f"<td>{dog_cell}</td>"
                f"<td>{base._esc(team['country'])}</td>"
                f"<td class='num'>{team['rating']:.0f}</td>"
                f"<td class='num'>{team['num_runs']}</td>"
                f"<td class='num'>{team['clean_pct']:.1f}%</td>"
                f"<td class='num'>{team['top3_pct']:.1f}%</td>"
                f"<td class='num'>{team['top10_pct']:.1f}%</td>"
                f"<td>{base._esc(team['last_comp'])}</td>"
                f"</tr>"
            )

        tables[size] = "\n".join(rows)

    tab_buttons = []
    for idx, size in enumerate(sizes):
        count = sum(1 for team in all_ratings[size].values() if team["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING)
        active = " active" if idx == 0 else ""
        tab_buttons.append(
            f'<button class="tab-btn{active}" onclick="showTab(\'{size}\', this)">'
            f'{size} <span class="count">({count})</span></button>'
        )

    tab_contents = []
    for idx, size in enumerate(sizes):
        display = "block" if idx == 0 else "none"
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
                        <th onclick="sortTable('table-{size}', 6, 'num')">Clean %</th>
                        <th onclick="sortTable('table-{size}', 7, 'num')">TOP3 %</th>
                        <th onclick="sortTable('table-{size}', 8, 'num')">TOP10 %</th>
                        <th onclick="sortTable('table-{size}', 9, 'str')">Last Competition</th>
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
<title>ADW Live Rating</title>
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
.filters {{ display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }}
.search-box input {{ padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 14px; width: 320px; max-width: 100%; }}
.country-box select {{ padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 14px; background: #fff; min-width: 180px; }}
</style>
</head>
<body>
    <h1>ADW Live Rating</h1>
    <p class="subtitle">Single public rating. Runs from {cutoff_date} to {latest_date} (last {LIVE_WINDOW_DAYS} days), min {MIN_RUNS_FOR_LIVE_RANKING} runs. Base formula: mu - {RATING_SIGMA_MULTIPLIER}*sigma, sigma decay {LIVE_SIGMA_DECAY}, major tier-1 weight {MAJOR_EVENT_WEIGHT if ENABLE_MAJOR_EVENT_WEIGHTING else 1.0}x. Podium boost: rating * ({PODIUM_BOOST_BASE:.2f}..{PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE:.2f}) by quality mix (clean/top10/top3). PROV if sigma >= {LIVE_PROVISIONAL_SIGMA_THRESHOLD}.</p>

    <div class="filters">
        <div class="search-box">
            <input id="search" type="text" placeholder="Search handler, dog, country..." oninput="filterRows()">
        </div>
        <div class="country-box">
            <select id="country-filter" onchange="filterRows()">
                <option value="">All countries</option>
            </select>
        </div>
    </div>

    <div class="tabs">
        {"".join(tab_buttons)}
    </div>

    {"".join(tab_contents)}

<script>
let currentTab = '{sizes[0] if sizes else ""}';
const countriesBySize = {countries_by_size};

function showTab(size, buttonEl) {{
    currentTab = size;
    document.querySelectorAll('.tab-content').forEach(tab => tab.style.display = 'none');
    document.getElementById('tab-' + size).style.display = 'block';
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
    if (buttonEl) {{
        buttonEl.classList.add('active');
    }}
    updateCountryFilterOptions();
    filterRows();
}}

function updateCountryFilterOptions() {{
    const select = document.getElementById('country-filter');
    const previous = select.value;
    while (select.options.length > 1) select.remove(1);
    const options = countriesBySize[currentTab] || [];
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
        let va = a.cells[colIdx].textContent.trim();
        let vb = b.cells[colIdx].textContent.trim();
        if (type === 'num') {{
            va = parseFloat(va) || 0;
            vb = parseFloat(vb) || 0;
            return asc ? va - vb : vb - va;
        }}
        return asc ? va.localeCompare(vb) : vb.localeCompare(va);
    }});
    rows.forEach(row => tbody.appendChild(row));
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

    with open(outpath, "w", encoding="utf-8") as html_file:
        html_file.write(html)

    print(f"HTML written to {outpath}")


def main():
    print("Running final live leaderboard variant...")
    print(
        "LIVE_WINDOW_DAYS="
        f"{LIVE_WINDOW_DAYS}, MIN_RUNS_FOR_LIVE_RANKING={MIN_RUNS_FOR_LIVE_RANKING}, "
        f"LIVE_SIGMA_DECAY={LIVE_SIGMA_DECAY}, LIVE_PROVISIONAL_SIGMA_THRESHOLD={LIVE_PROVISIONAL_SIGMA_THRESHOLD}, "
        f"RATING_SIGMA_MULTIPLIER={RATING_SIGMA_MULTIPLIER}, "
        f"ENABLE_MAJOR_EVENT_WEIGHTING={ENABLE_MAJOR_EVENT_WEIGHTING}, MAJOR_EVENT_WEIGHT={MAJOR_EVENT_WEIGHT}, "
        f"ENABLE_PODIUM_BOOST={ENABLE_PODIUM_BOOST}, PODIUM_BOOST_BASE={PODIUM_BOOST_BASE}, "
        f"PODIUM_BOOST_RANGE={PODIUM_BOOST_RANGE}, PODIUM_BOOST_TARGET={PODIUM_BOOST_TARGET}"
    )

    runs = base.load_all_runs()
    profiles = base.build_team_profiles(runs)

    all_ratings, cutoff_date, latest_date = calculate_live_ratings(runs, profiles)
    total_teams = sum(len(size_map) for size_map in all_ratings.values())
    print(f"\nTotal unique teams rated (inside live window): {total_teams}")

    write_csv_live(all_ratings)
    write_html_live(all_ratings, cutoff_date, latest_date)
    print("Done!")


if __name__ == "__main__":
    main()
