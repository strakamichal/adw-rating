#!/usr/bin/env python3
"""
Live-focused variant:
- applies inactivity sigma inflation during chronological processing,
- marks active teams by runs in the last 12 months,
- outputs both active leaderboard and form (last 12 months) leaderboard.

Outputs:
  - output/ratings_live_variant_active.csv
  - output/ratings_live_variant_active.html
  - output/ratings_live_variant_form12m.csv
  - output/ratings_live_variant_form12m.html
"""

import csv
import math
import os
from collections import defaultdict
from datetime import datetime, timedelta

from openskill.models import PlackettLuce

import calculate_rating as base


# ---------------------------------------------------------------------------
# Variant config
# ---------------------------------------------------------------------------

# Team is active in live leaderboard only if it has at least this many runs
# in the last 12 months from the latest date in the loaded dataset.
MIN_RUNS_IN_ACTIVE_WINDOW = 3
ACTIVE_WINDOW_DAYS = 365

# Inactivity inflation for sigma (calendar gap between competitions).
# Applied before each competition for teams that already have a rating.
INACTIVITY_TAU = 0.50
SIGMA_MAX = 25.0 / 3.0  # OpenSkill default initial sigma

# Form leaderboard includes only recent runs and uses this minimum run count.
MIN_FORM_RUNS_FOR_RANKING = 3


def _parse_date(date_str):
    return datetime.strptime(date_str, "%Y-%m-%d").date()


def _percentile_threshold(values, percentile):
    if not values:
        return float("inf")
    sorted_values = sorted(values)
    idx = int((len(sorted_values) - 1) * percentile)
    return sorted_values[idx]


def _compute_tier_thresholds(all_ratings, min_runs_threshold):
    thresholds_by_size = {}
    for size in sorted(all_ratings.keys()):
        ranked = [
            team for team in all_ratings[size].values()
            if team["num_runs"] >= min_runs_threshold
        ]
        scores = [team["displayed_rating"] for team in ranked]
        thresholds_by_size[size] = {
            "elite_min": _percentile_threshold(scores, 1.0 - base.ELITE_TOP_PERCENT),
            "champion_min": _percentile_threshold(scores, 1.0 - base.CHAMPION_TOP_PERCENT),
            "expert_min": _percentile_threshold(scores, 1.0 - base.EXPERT_TOP_PERCENT),
        }
    return thresholds_by_size


def _inflate_sigma_for_gap(rating, last_run_date, current_date):
    """Inflate sigma for inactivity gap between two dates."""
    if not last_run_date:
        return
    gap_days = (current_date - last_run_date).days
    if gap_days <= 30:
        return
    months = gap_days / 30.0
    rating.sigma = min(SIGMA_MAX, math.sqrt((rating.sigma * rating.sigma) + (INACTIVITY_TAU * INACTIVITY_TAU * months)))


def _annotate_activity(size_results, reference_date):
    """Add runs_12m + active_12m flags to teams within one size map."""
    cutoff = reference_date - timedelta(days=ACTIVE_WINDOW_DAYS)
    for team in size_results.values():
        run_dates = team.pop("_run_dates", [])
        runs_12m = sum(1 for d in run_dates if d >= cutoff)
        team["num_runs_12m"] = runs_12m
        team["active_12m"] = runs_12m >= MIN_RUNS_IN_ACTIVE_WINDOW


def calculate_ratings_variant(
    runs,
    profiles,
    reference_date,
    *,
    apply_inactivity_inflation,
    min_runs_for_tiers,
):
    """
    Calculate OpenSkill ratings for variant mode.

    Returns dict: size -> {team_id: payload}
    """
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
            comp_date = _parse_date(base.COMPETITIONS[comp_dir]["date"])

            if apply_inactivity_inflation:
                for team_id, rating in team_ratings.items():
                    _inflate_sigma_for_gap(rating, team_stats[team_id]["last_run_date"], comp_date)

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
                            "last_comp": "",
                            "last_comp_date": "",
                            "last_run_date": None,
                            "run_dates": [],
                        }

                    teams.append([team_ratings[team_id]])
                    ranks.append(rank)
                    entry_order.append(team_id)

                    team_stats[team_id]["num_runs"] += 1
                    team_stats[team_id]["run_dates"].append(comp_date)
                    team_stats[team_id]["last_run_date"] = comp_date
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
                "_run_dates": list(stats["run_dates"]),
            }

        _annotate_activity(size_results, reference_date)

        sorted_teams = sorted(size_results.items(), key=lambda x: -x[1]["displayed_rating"])
        print(f"  {len(sorted_teams)} unique teams rated")
        if sorted_teams:
            top = sorted_teams[0][1]
            print(f"  Top: {top['handler']} / {top['dog']} ({top['country']}) — {top['displayed_rating']}")

        all_ratings[size] = size_results

    tier_thresholds = _compute_tier_thresholds(all_ratings, min_runs_for_tiers)
    for size in sorted(all_ratings.keys()):
        thresholds = tier_thresholds[size]
        for team in all_ratings[size].values():
            team["skill_tier"] = base.skill_tier_label(team["displayed_rating"], thresholds)
            team["provisional"] = base.is_provisional(team["sigma"])

    return all_ratings


def _write_csv(all_ratings, out_filename, *, eligibility_fn):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, out_filename)

    with open(outpath, "w", newline="", encoding="utf-8") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow([
            "rank", "handler", "call_name", "registered_name", "size", "country",
            "mu", "sigma", "displayed_rating",
            "tier", "provisional", "num_runs", "num_runs_12m", "active_12m", "last_competition",
        ])

        for size in sorted(all_ratings.keys()):
            sorted_teams = sorted(
                (team for team in all_ratings[size].values() if eligibility_fn(team)),
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
                    team["num_runs_12m"],
                    str(team["active_12m"]).lower(),
                    team["last_comp"],
                ])

    print(f"\nCSV written to {outpath}")


def _write_html(all_ratings, out_filename, title, subtitle, *, eligibility_fn, include_active_cols):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, out_filename)

    sizes = base.ordered_sizes(all_ratings.keys())
    tables = {}
    countries_by_size = {}

    for size in sizes:
        sorted_teams = sorted(
            (team for team in all_ratings[size].values() if eligibility_fn(team)),
            key=lambda x: -x["displayed_rating"],
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

            base_cells = (
                f"<td>{rank}</td>"
                f"<td>{handler_cell}</td>"
                f"<td>{dog_cell}</td>"
                f"<td>{base._esc(team['country'])}</td>"
                f"<td class='num'>{team['displayed_rating']:.0f}</td>"
                f"<td class='num'>{team['num_runs']}</td>"
            )
            if include_active_cols:
                extra_cells = (
                    f"<td class='num'>{team['num_runs_12m']}</td>"
                    f"<td>{'yes' if team['active_12m'] else 'no'}</td>"
                )
            else:
                extra_cells = ""

            rows.append(
                f"<tr class='tier-{team.get('skill_tier', 'Competitor').lower()}'>"
                f"{base_cells}{extra_cells}<td>{base._esc(team['last_comp'])}</td>"
                f"</tr>"
            )

        tables[size] = "\n".join(rows)

    tab_buttons = []
    for idx, size in enumerate(sizes):
        count = sum(1 for team in all_ratings[size].values() if eligibility_fn(team))
        active = " active" if idx == 0 else ""
        tab_buttons.append(
            f'<button class="tab-btn{active}" onclick="showTab(\'{size}\')">'
            f'{size} <span class="count">({count})</span></button>'
        )

    th_active = ""
    if include_active_cols:
        th_active = (
            "<th onclick=\"sortTable('table-{size}', 6, 'num')\">Runs 12m</th>"
            "<th onclick=\"sortTable('table-{size}', 7, 'str')\">Active</th>"
        )

    tab_contents = []
    for idx, size in enumerate(sizes):
        display = "block" if idx == 0 else "none"
        if include_active_cols:
            last_col = 8
        else:
            last_col = 6
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
                        {th_active.format(size=size) if include_active_cols else ""}
                        <th onclick="sortTable('table-{size}', {last_col}, 'str')">Last Competition</th>
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
<title>{base._esc(title)}</title>
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
    <h1>{base._esc(title)}</h1>
    <p class="subtitle">{base._esc(subtitle)}</p>

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

function showTab(size) {{
    currentTab = size;
    document.querySelectorAll('.tab-content').forEach(tab => tab.style.display = 'none');
    document.getElementById('tab-' + size).style.display = 'block';
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
    event.target.classList.add('active');
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


def _write_html_compare(live_all_ratings, form_all_ratings):
    """Single HTML output with mode switch: Active ranking vs Form ranking."""
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_live_variant_compare.html")

    sizes = base.ordered_sizes(set(live_all_ratings.keys()) | set(form_all_ratings.keys()))

    # Pre-build rows for each size/mode.
    rows_by_size_mode = {"active": {}, "form": {}}
    countries_by_size_mode = {"active": {}, "form": {}}

    for size in sizes:
        live_size = live_all_ratings.get(size, {})
        form_size = form_all_ratings.get(size, {})
        team_ids = set(live_size.keys()) | set(form_size.keys())

        active_rows_data = []
        form_rows_data = []

        for team_id in team_ids:
            live_team = live_size.get(team_id)
            form_team = form_size.get(team_id)
            meta = live_team or form_team
            if not meta:
                continue

            active_eligible = bool(
                live_team
                and live_team["num_runs"] >= base.MIN_RUNS_FOR_RANKING
                and live_team.get("active_12m", False)
            )
            form_eligible = bool(
                form_team
                and form_team["num_runs"] >= MIN_FORM_RUNS_FOR_RANKING
            )

            active_rating = live_team["displayed_rating"] if live_team else None
            form_rating = form_team["displayed_rating"] if form_team else None
            rating_delta = None
            if active_rating is not None and form_rating is not None:
                rating_delta = form_rating - active_rating

            common = {
                "handler": meta["handler"],
                "call_name": meta.get("call_name", ""),
                "registered_name": meta.get("registered_name", ""),
                "country": meta["country"],
                "last_comp": (live_team or form_team).get("last_comp", ""),
                "num_runs_total": live_team["num_runs"] if live_team else 0,
                "num_runs_12m": live_team["num_runs_12m"] if live_team else 0,
                "num_runs_form": form_team["num_runs"] if form_team else 0,
                "active_rating": active_rating,
                "form_rating": form_rating,
                "rating_delta": rating_delta,
                "active_tier": live_team.get("skill_tier", "Competitor") if live_team else "Competitor",
                "form_tier": form_team.get("skill_tier", "Competitor") if form_team else "Competitor",
                "active_prov": live_team.get("provisional", False) if live_team else False,
                "form_prov": form_team.get("provisional", False) if form_team else False,
            }

            if active_eligible:
                active_rows_data.append(common)
            if form_eligible:
                form_rows_data.append(common)

        active_rows_data.sort(key=lambda x: -(x["active_rating"] if x["active_rating"] is not None else -999999))
        form_rows_data.sort(key=lambda x: -(x["form_rating"] if x["form_rating"] is not None else -999999))

        def _dog_cell(row):
            call = base._esc(row.get("call_name", ""))
            reg = base._esc(row.get("registered_name", ""))
            if reg and call:
                return f"<strong>{call}</strong><br><span class='reg-name'>{reg}</span>"
            if call:
                return f"<strong>{call}</strong>"
            if reg:
                return f"<span class='reg-name'>{reg}</span>"
            return ""

        def _fmt_rating(value):
            if value is None:
                return "-"
            return f"{value:.0f}"

        def _fmt_delta(value):
            if value is None:
                return "-"
            sign = "+" if value >= 0 else ""
            return f"{sign}{value:.0f}"

        active_rows = []
        for rank, row in enumerate(active_rows_data, 1):
            prov_badge = " <span class='prov-badge'>PROV</span>" if row["active_prov"] else ""
            active_rows.append(
                f"<tr class='tier-{row['active_tier'].lower()}'>"
                f"<td>{rank}</td>"
                f"<td>{base._esc(row['handler'])}{prov_badge}</td>"
                f"<td>{_dog_cell(row)}</td>"
                f"<td>{base._esc(row['country'])}</td>"
                f"<td class='num'>{_fmt_rating(row['active_rating'])}</td>"
                f"<td class='num'>{_fmt_rating(row['form_rating'])}</td>"
                f"<td class='num'>{_fmt_delta(row['rating_delta'])}</td>"
                f"<td class='num'>{row['num_runs_total']}</td>"
                f"<td class='num'>{row['num_runs_12m']}</td>"
                f"<td class='num'>{row['num_runs_form']}</td>"
                f"<td>{base._esc(row['last_comp'])}</td>"
                f"</tr>"
            )

        form_rows = []
        for rank, row in enumerate(form_rows_data, 1):
            prov_badge = " <span class='prov-badge'>PROV</span>" if row["form_prov"] else ""
            form_rows.append(
                f"<tr class='tier-{row['form_tier'].lower()}'>"
                f"<td>{rank}</td>"
                f"<td>{base._esc(row['handler'])}{prov_badge}</td>"
                f"<td>{_dog_cell(row)}</td>"
                f"<td>{base._esc(row['country'])}</td>"
                f"<td class='num'>{_fmt_rating(row['active_rating'])}</td>"
                f"<td class='num'>{_fmt_rating(row['form_rating'])}</td>"
                f"<td class='num'>{_fmt_delta(row['rating_delta'])}</td>"
                f"<td class='num'>{row['num_runs_total']}</td>"
                f"<td class='num'>{row['num_runs_12m']}</td>"
                f"<td class='num'>{row['num_runs_form']}</td>"
                f"<td>{base._esc(row['last_comp'])}</td>"
                f"</tr>"
            )

        rows_by_size_mode["active"][size] = "\n".join(active_rows)
        rows_by_size_mode["form"][size] = "\n".join(form_rows)
        countries_by_size_mode["active"][size] = sorted({row["country"] for row in active_rows_data if row["country"]})
        countries_by_size_mode["form"][size] = sorted({row["country"] for row in form_rows_data if row["country"]})

    # Buttons
    size_buttons = []
    for idx, size in enumerate(sizes):
        active = " active" if idx == 0 else ""
        size_buttons.append(
            f'<button class="tab-btn{active}" onclick="showTab(\'{size}\', this)">{size}</button>'
        )

    mode_buttons = [
        '<button class="mode-btn active" onclick="setMode(\'active\', this)">Active Ranking</button>',
        '<button class="mode-btn" onclick="setMode(\'form\', this)">Form Ranking (12m)</button>',
    ]

    # Content blocks: one table per mode+size
    content_blocks = []
    for mode in ("active", "form"):
        for idx, size in enumerate(sizes):
            display = "block" if (mode == "active" and idx == 0) else "none"
            content_blocks.append(f"""
        <div id="tab-{mode}-{size}" class="tab-content mode-{mode}" style="display:{display}">
            <table class="rating-table" id="table-{mode}-{size}">
                <thead>
                    <tr>
                        <th onclick="sortTable('table-{mode}-{size}', 0, 'num')">#</th>
                        <th onclick="sortTable('table-{mode}-{size}', 1, 'str')">Handler</th>
                        <th onclick="sortTable('table-{mode}-{size}', 2, 'str')">Dog</th>
                        <th onclick="sortTable('table-{mode}-{size}', 3, 'str')">Country</th>
                        <th onclick="sortTable('table-{mode}-{size}', 4, 'num')">Active Rating</th>
                        <th onclick="sortTable('table-{mode}-{size}', 5, 'num')">Form Rating</th>
                        <th onclick="sortTable('table-{mode}-{size}', 6, 'num')">Delta</th>
                        <th onclick="sortTable('table-{mode}-{size}', 7, 'num')">Runs Total</th>
                        <th onclick="sortTable('table-{mode}-{size}', 8, 'num')">Runs 12m</th>
                        <th onclick="sortTable('table-{mode}-{size}', 9, 'num')">Form Runs</th>
                        <th onclick="sortTable('table-{mode}-{size}', 10, 'str')">Last Competition</th>
                    </tr>
                </thead>
                <tbody>
                    {rows_by_size_mode[mode].get(size, "")}
                </tbody>
            </table>
        </div>""")

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>ADW Rating - Live Variant Compare</title>
<style>
* {{ margin: 0; padding: 0; box-sizing: border-box; }}
body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f5f5f5; color: #333; padding: 20px; }}
h1 {{ margin-bottom: 4px; }}
.subtitle {{ color: #666; margin-bottom: 16px; font-size: 14px; }}
.mode-switch {{ display: flex; gap: 8px; margin-bottom: 12px; flex-wrap: wrap; }}
.mode-btn {{ padding: 8px 14px; border: 1px solid #ddd; background: #fff; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 600; }}
.mode-btn.active {{ background: #111827; color: #fff; border-color: #111827; }}
.tabs {{ display: flex; gap: 8px; margin-bottom: 16px; flex-wrap: wrap; }}
.tab-btn {{ padding: 8px 16px; border: 1px solid #ddd; background: #fff; border-radius: 6px; cursor: pointer; font-size: 14px; font-weight: 500; }}
.tab-btn.active {{ background: #2563eb; color: #fff; border-color: #2563eb; }}
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
    <h1>ADW Rating - Live Variant (Compare)</h1>
    <p class="subtitle">Single view for comparison. Switch ranking mode between Active and Form (12m). Delta = Form - Active.</p>

    <div class="mode-switch">
        {"".join(mode_buttons)}
    </div>

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
        {"".join(size_buttons)}
    </div>

    {"".join(content_blocks)}

<script>
let currentMode = 'active';
let currentTab = '{sizes[0] if sizes else ""}';
const countriesBySizeMode = {countries_by_size_mode};

function updateVisibleTable() {{
    document.querySelectorAll('.tab-content').forEach(el => el.style.display = 'none');
    const id = 'tab-' + currentMode + '-' + currentTab;
    const target = document.getElementById(id);
    if (target) target.style.display = 'block';
}}

function showTab(size, btn) {{
    currentTab = size;
    document.querySelectorAll('.tab-btn').forEach(el => el.classList.remove('active'));
    if (btn) btn.classList.add('active');
    updateVisibleTable();
    updateCountryFilterOptions();
    filterRows();
}}

function setMode(mode, btn) {{
    currentMode = mode;
    document.querySelectorAll('.mode-btn').forEach(el => el.classList.remove('active'));
    if (btn) btn.classList.add('active');
    updateVisibleTable();
    updateCountryFilterOptions();
    filterRows();
}}

function updateCountryFilterOptions() {{
    const select = document.getElementById('country-filter');
    const previous = select.value;
    while (select.options.length > 1) select.remove(1);
    const options = (countriesBySizeMode[currentMode] && countriesBySizeMode[currentMode][currentTab]) || [];
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
    if (!table) return;
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
    const table = document.getElementById('table-' + currentMode + '-' + currentTab);
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

updateVisibleTable();
updateCountryFilterOptions();
</script>
</body>
</html>"""

    with open(outpath, "w", encoding="utf-8") as html_file:
        html_file.write(html)

    print(f"HTML written to {outpath}")


def main():
    print("Running live variant: active filter + inactivity sigma inflation + form 12m output")
    print(
        f"ACTIVE_WINDOW_DAYS={ACTIVE_WINDOW_DAYS}, MIN_RUNS_IN_ACTIVE_WINDOW={MIN_RUNS_IN_ACTIVE_WINDOW}, "
        f"INACTIVITY_TAU={INACTIVITY_TAU}, SIGMA_MAX={SIGMA_MAX:.4f}"
    )

    runs = base.load_all_runs()
    profiles = base.build_team_profiles(runs)
    if not runs:
        print("No runs loaded. Exiting.")
        return

    latest_date = max(_parse_date(run["comp_date"]) for run in runs)
    cutoff_12m = latest_date - timedelta(days=ACTIVE_WINDOW_DAYS)
    form_runs = [run for run in runs if _parse_date(run["comp_date"]) >= cutoff_12m]

    print(f"Latest competition date in dataset: {latest_date}")
    print(f"Form window starts at: {cutoff_12m} (inclusive)")
    print(f"Form runs: {len(form_runs)} / {len(runs)}")

    print("\n[1/2] Calculating full-history ratings with inactivity sigma inflation...")
    live_all_ratings = calculate_ratings_variant(
        runs,
        profiles,
        latest_date,
        apply_inactivity_inflation=True,
        min_runs_for_tiers=base.MIN_RUNS_FOR_RANKING,
    )

    print("\n[2/2] Calculating form-only ratings (last 12 months)...")
    form_all_ratings = calculate_ratings_variant(
        form_runs,
        profiles,
        latest_date,
        apply_inactivity_inflation=False,
        min_runs_for_tiers=MIN_FORM_RUNS_FOR_RANKING,
    )

    _write_csv(
        live_all_ratings,
        "ratings_live_variant_active.csv",
        eligibility_fn=lambda team: team["num_runs"] >= base.MIN_RUNS_FOR_RANKING and team["active_12m"],
    )
    _write_html(
        live_all_ratings,
        "ratings_live_variant_active.html",
        "ADW Rating - Live Variant (Active Teams)",
        (
            f"OpenSkill (Plackett-Luce) | inactivity sigma inflation (tau={INACTIVITY_TAU}) | "
            f"active = >= {MIN_RUNS_IN_ACTIVE_WINDOW} runs in last {ACTIVE_WINDOW_DAYS} days"
        ),
        eligibility_fn=lambda team: team["num_runs"] >= base.MIN_RUNS_FOR_RANKING and team["active_12m"],
        include_active_cols=True,
    )

    _write_csv(
        form_all_ratings,
        "ratings_live_variant_form12m.csv",
        eligibility_fn=lambda team: team["num_runs"] >= MIN_FORM_RUNS_FOR_RANKING,
    )
    _write_html(
        form_all_ratings,
        "ratings_live_variant_form12m.html",
        "ADW Rating - Live Variant (Form 12m)",
        (
            f"OpenSkill (Plackett-Luce) | runs from last {ACTIVE_WINDOW_DAYS} days only | "
            f"min {MIN_FORM_RUNS_FOR_RANKING} runs"
        ),
        eligibility_fn=lambda team: team["num_runs"] >= MIN_FORM_RUNS_FOR_RANKING,
        include_active_cols=False,
    )

    _write_html_compare(live_all_ratings, form_all_ratings)

    total_live = sum(len(size_map) for size_map in live_all_ratings.values())
    total_form = sum(len(size_map) for size_map in form_all_ratings.values())
    print(f"\nTotal rated teams (live calc): {total_live}")
    print(f"Total rated teams (form calc): {total_form}")
    print("Done!")


if __name__ == "__main__":
    main()
