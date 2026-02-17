#!/usr/bin/env python3
"""
WOW variant: ignore eliminated runs in rating updates.

Idea:
- Only clean (non-eliminated, ranked) results affect rating updates.
- Eliminated results are completely ignored in OpenSkill updates.
- Ranking is based on these clean-only updates.

Outputs:
  - output/ratings_wow.csv
  - output/ratings_wow.html
"""

import csv
import os
from collections import defaultdict

from openskill.models import PlackettLuce

import calculate_rating as base


# Minimum clean runs shown in output leaderboard.
MIN_CLEAN_RUNS_FOR_RANKING = 4


def _percentile_threshold(values, percentile):
    if not values:
        return float("inf")
    sorted_values = sorted(values)
    idx = int((len(sorted_values) - 1) * percentile)
    return sorted_values[idx]


def _compute_tier_thresholds(all_ratings):
    thresholds_by_size = {}
    for size in sorted(all_ratings.keys()):
        ranked = [
            team for team in all_ratings[size].values()
            if team["num_runs"] >= MIN_CLEAN_RUNS_FOR_RANKING
        ]
        scores = [team["displayed_rating"] for team in ranked]
        thresholds_by_size[size] = {
            "elite_min": _percentile_threshold(scores, 1.0 - base.ELITE_TOP_PERCENT),
            "champion_min": _percentile_threshold(scores, 1.0 - base.CHAMPION_TOP_PERCENT),
            "expert_min": _percentile_threshold(scores, 1.0 - base.EXPERT_TOP_PERCENT),
        }
    return thresholds_by_size


def calculate_ratings_wow(runs, profiles):
    by_size = defaultdict(list)
    for run in runs:
        by_size[run["size"]].append(run)

    all_ratings = {}

    for size in sorted(by_size.keys()):
        size_runs = by_size[size]
        print(f"\n--- {size} ({len(size_runs)} runs) ---")

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

                # WOW mode: keep only clean results, ignore eliminated entries entirely.
                clean = [e for e in entries if not e["eliminated"] and e["rank"] is not None]
                if len(clean) < base.MIN_FIELD_SIZE:
                    continue
                clean.sort(key=lambda e: e["rank"])

                teams = []
                ranks = []
                entry_order = []

                for idx, entry in enumerate(clean):
                    team_id = entry["team_id"]
                    if team_id not in team_ratings:
                        team_ratings[team_id] = model.rating()
                        team_stats[team_id] = {
                            "num_runs": 0,
                            "last_comp": "",
                            "last_comp_date": "",
                        }

                    teams.append([team_ratings[team_id]])
                    ranks.append(idx + 1)
                    entry_order.append(team_id)

                    team_stats[team_id]["num_runs"] += 1
                    if entry["comp_date"] >= team_stats[team_id]["last_comp_date"]:
                        team_stats[team_id]["last_comp"] = comp_name
                        team_stats[team_id]["last_comp_date"] = entry["comp_date"]

                tier = entries[0]["comp_tier"]
                tier_weight = base.TIER_WEIGHTS.get(tier, 1.0)
                weights = None
                if base.ENABLE_TIER_WEIGHTING and tier_weight != 1.0:
                    weights = [[tier_weight] for _ in entry_order]

                result = model.rate(teams, ranks=ranks, weights=weights)

                for idx, team_id in enumerate(entry_order):
                    new_rating = result[idx][0]
                    new_rating.sigma = max(base.SIGMA_MIN, new_rating.sigma * base.SIGMA_DECAY)
                    team_ratings[team_id] = new_rating

        size_results = {}
        for team_id, rating in team_ratings.items():
            stats = team_stats[team_id]
            profile = profiles.get(team_id, {})
            size_results[team_id] = {
                "mu": rating.mu,
                "sigma": rating.sigma,
                "displayed_rating": round(base.displayed_rating(rating.mu, rating.sigma), 1),
                "handler": profile.get("handler_display", ""),
                "dog": profile.get("dog_display", ""),
                "call_name": profile.get("call_name", ""),
                "registered_name": profile.get("registered_name", ""),
                "country": profile.get("country", ""),
                "num_runs": stats["num_runs"],
                "last_comp": stats["last_comp"],
            }

        sorted_teams = sorted(size_results.items(), key=lambda x: -x[1]["displayed_rating"])
        print(f"  {len(sorted_teams)} unique teams rated")
        if sorted_teams:
            top = sorted_teams[0][1]
            print(f"  Top: {top['handler']} / {top['dog']} ({top['country']}) — {top['displayed_rating']}")

        all_ratings[size] = size_results

    tier_thresholds = _compute_tier_thresholds(all_ratings)
    for size in sorted(all_ratings.keys()):
        thresholds = tier_thresholds[size]
        for team in all_ratings[size].values():
            team["skill_tier"] = base.skill_tier_label(team["displayed_rating"], thresholds)
            team["provisional"] = base.is_provisional(team["sigma"])

    return all_ratings


def write_csv_wow(all_ratings):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_wow.csv")

    with open(outpath, "w", newline="", encoding="utf-8") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow([
            "rank", "handler", "call_name", "registered_name", "size", "country",
            "mu", "sigma", "displayed_rating",
            "tier", "provisional", "num_runs", "last_competition",
        ])

        for size in sorted(all_ratings.keys()):
            sorted_teams = sorted(
                (team for team in all_ratings[size].values() if team["num_runs"] >= MIN_CLEAN_RUNS_FOR_RANKING),
                key=lambda x: -x["displayed_rating"],
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
                    team["displayed_rating"],
                    team.get("skill_tier", "Competitor"),
                    str(team.get("provisional", False)).lower(),
                    team["num_runs"],
                    team["last_comp"],
                ])

    print(f"\nCSV written to {outpath}")


def write_html_wow(all_ratings):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_wow.html")

    sizes = base.ordered_sizes(all_ratings.keys())

    tables = {}
    countries_by_size = {}
    for size in sizes:
        sorted_teams = sorted(
            (t for t in all_ratings[size].values() if t["num_runs"] >= MIN_CLEAN_RUNS_FOR_RANKING),
            key=lambda x: -x["displayed_rating"],
        )
        countries_by_size[size] = sorted({t["country"] for t in sorted_teams if t["country"]})
        rows = []
        for rank, team in enumerate(sorted_teams, 1):
            rating = team["displayed_rating"]
            tier_label = team.get("skill_tier", "Competitor")
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
                f"<tr class='tier-{tier_label.lower()}'>"
                f"<td>{rank}</td>"
                f"<td>{handler_cell}</td>"
                f"<td>{dog_cell}</td>"
                f"<td>{base._esc(team['country'])}</td>"
                f"<td class='num'>{rating:.0f}</td>"
                f"<td class='num'>{team['num_runs']}</td>"
                f"<td>{base._esc(team['last_comp'])}</td>"
                f"</tr>"
            )
        tables[size] = "\n".join(rows)

    tab_buttons = []
    for idx, size in enumerate(sizes):
        count = sum(1 for t in all_ratings[size].values() if t["num_runs"] >= MIN_CLEAN_RUNS_FOR_RANKING)
        active = " active" if idx == 0 else ""
        tab_buttons.append(
            f'<button class="tab-btn{active}" onclick="showTab(\'{size}\')">'
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
                        <th onclick="sortTable('table-{size}', 5, 'num')">Clean Runs</th>
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
<title>ADW Rating — WOW Variant</title>
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
<h1>ADW Rating — WOW Variant</h1>
<p class="subtitle">OpenSkill (Plackett-Luce) · DIS ignored in updates · minimum {MIN_CLEAN_RUNS_FOR_RANKING} clean runs for leaderboard</p>

<div class="legend">
    <div class="legend-item"><div class="legend-color" style="background:#f59e0b"></div> Elite (top 2% per size)</div>
    <div class="legend-item"><div class="legend-color" style="background:#8b5cf6"></div> Champion (next 8%)</div>
    <div class="legend-item"><div class="legend-color" style="background:#3b82f6"></div> Expert (next 20%)</div>
    <div class="legend-item"><div class="legend-color" style="background:#10b981"></div> Competitor (remaining)</div>
    <div class="legend-item"><span class="prov-badge">PROV</span> Provisional (sigma ≥ {base.PROVISIONAL_SIGMA_THRESHOLD})</div>
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

    with open(outpath, "w", encoding="utf-8") as html_file:
        html_file.write(html)

    print(f"HTML written to {outpath}")


if __name__ == "__main__":
    print("Running WOW variant (ELIM ignored in updates).")
    print(f"MIN_CLEAN_RUNS_FOR_RANKING={MIN_CLEAN_RUNS_FOR_RANKING}")

    runs = base.load_all_runs()
    profiles = base.build_team_profiles(runs)
    all_ratings = calculate_ratings_wow(runs, profiles)

    total_teams = sum(len(size_map) for size_map in all_ratings.values())
    print(f"\nTotal unique teams across all sizes: {total_teams}")

    write_csv_wow(all_ratings)
    write_html_wow(all_ratings)
    print("\nDone!")
