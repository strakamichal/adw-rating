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

LIVE_WINDOW_DAYS = 730  # 2 years
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
# Based purely on top-3 placement percentage.
# factor = PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE * clamp01(top3_pct / PODIUM_BOOST_TARGET)
ENABLE_PODIUM_BOOST = True
PODIUM_BOOST_BASE = 0.85
PODIUM_BOOST_RANGE = 0.20
PODIUM_BOOST_TARGET = 50.0


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


def podium_boost_factor(finished_pct, top3_pct, top10_pct):
    quality_norm = max(0.0, min(1.0, top3_pct / PODIUM_BOOST_TARGET))
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
        return {}, None, None, {}

    latest_date = max(_parse_date(run["comp_date"]) for run in runs)
    cutoff_date = latest_date - timedelta(days=LIVE_WINDOW_DAYS)
    live_runs = [run for run in runs if _parse_date(run["comp_date"]) >= cutoff_date]

    by_size = defaultdict(list)
    for run in live_runs:
        by_size[run["size"]].append(run)

    all_ratings = {}
    # Per-competition stats: comp_dir -> {name, date, tier, teams_by_size, runs_total, finished_runs, total_entries}
    comp_stats = {}

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
            comp_info = base.COMPETITIONS[comp_dir]
            comp_name = comp_info["name"]

            if comp_dir not in comp_stats:
                comp_stats[comp_dir] = {
                    "name": comp_name,
                    "date": comp_info["date"],
                    "tier": comp_info["tier"],
                    "teams_by_size": {},
                    "team_ids_by_size": {},
                    "runs_total": 0,
                    "finished_runs_total": 0,
                    "total_entries": 0,
                }

            round_runs = defaultdict(list)
            for run in comp_runs[comp_dir]:
                round_runs[run["round_key"]].append(run)

            comp_teams_this_size = set()

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
                            "finished_runs": 0,
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
                        team_stats[team_id]["finished_runs"] += 1
                        if rank <= 3:
                            team_stats[team_id]["top3_runs"] += 1
                        if rank <= 10:
                            team_stats[team_id]["top10_runs"] += 1
                    if entry["comp_date"] >= team_stats[team_id]["last_comp_date"]:
                        team_stats[team_id]["last_comp"] = comp_name
                        team_stats[team_id]["last_comp_date"] = entry["comp_date"]

                # Track competition stats
                comp_stats[comp_dir]["runs_total"] += 1
                comp_stats[comp_dir]["total_entries"] += len(ranked_entries)
                comp_stats[comp_dir]["finished_runs_total"] += len(clean)
                for entry, rank in ranked_entries:
                    comp_teams_this_size.add(entry["team_id"])

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

            # Save unique teams for this size in this competition
            if comp_teams_this_size:
                comp_stats[comp_dir]["teams_by_size"][size] = len(comp_teams_this_size)
                comp_stats[comp_dir]["team_ids_by_size"][size] = comp_teams_this_size

        size_results = {}
        for team_id, rating in team_ratings.items():
            stats = team_stats[team_id]
            profile = profiles.get(team_id, {})
            num_runs = stats["num_runs"]
            finished_pct = round((stats["finished_runs"] / num_runs) * 100.0, 1) if num_runs else 0.0
            top3_pct = round((stats["top3_runs"] / num_runs) * 100.0, 1) if num_runs else 0.0
            top10_pct = round((stats["top10_runs"] / num_runs) * 100.0, 1) if num_runs else 0.0
            rating_base = live_rating(rating.mu, rating.sigma)
            quality_factor = podium_boost_factor(finished_pct, top3_pct, top10_pct) if ENABLE_PODIUM_BOOST else 1.0
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
                "finished_runs": stats["finished_runs"],
                "top3_runs": stats["top3_runs"],
                "top10_runs": stats["top10_runs"],
                "finished_pct": finished_pct,
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

    # Compute average rating per competition from final ratings
    for comp_dir, cs in comp_stats.items():
        ratings_sum = 0.0
        ratings_count = 0
        for size, team_ids in cs.get("team_ids_by_size", {}).items():
            if size in all_ratings:
                for tid in team_ids:
                    if tid in all_ratings[size]:
                        ratings_sum += all_ratings[size][tid]["rating"]
                        ratings_count += 1
        cs["avg_rating"] = round(ratings_sum / ratings_count, 1) if ratings_count else 0
        # Clean up team_ids (not needed in output)
        cs.pop("team_ids_by_size", None)

    return all_ratings, cutoff_date, latest_date, comp_stats


def write_csv_live(all_ratings):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_live_final.csv")

    with open(outpath, "w", newline="", encoding="utf-8") as csv_file:
        writer = csv.writer(csv_file)
        writer.writerow([
            "rank", "handler", "call_name", "registered_name", "size", "country",
            "mu", "sigma", "rating",
            "tier", "provisional", "num_runs",
            "finished_pct", "top3_pct",
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
                    team["finished_pct"],
                    team["top3_pct"],
                    team["last_comp"],
                ])

    print(f"\nCSV written to {outpath}")


def write_html_live(all_ratings, cutoff_date, latest_date, comp_stats=None):
    os.makedirs(base.OUTPUT_DIR, exist_ok=True)
    outpath = os.path.join(base.OUTPUT_DIR, "ratings_live_final.html")

    sizes = base.ordered_sizes(all_ratings.keys())
    tables = {}
    for size in sizes:
        sorted_teams = sorted(
            (team for team in all_ratings[size].values() if team["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING),
            key=lambda x: -x["rating"],
        )
        rows = []
        for rank, team in enumerate(sorted_teams, 1):
            provisional_badge = ""
            if team.get("provisional", False):
                provisional_badge = " <span class='prov-badge'>FEW RUNS</span>"
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
                f"<td class='num'>{team['finished_pct']:.1f}%</td>"
                f"<td class='num'>{team['top3_pct']:.1f}%</td>"
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
                        <th onclick="sortTable('table-{size}', 6, 'num')">Finished %</th>
                        <th onclick="sortTable('table-{size}', 7, 'num')">TOP3 %</th>
                        <th onclick="sortTable('table-{size}', 8, 'str')">Last Competition</th>
                    </tr>
                </thead>
                <tbody>
                    {tables[size]}
                </tbody>
            </table>
        </div>""")

    # --- Build competition stats rows ---
    comp_rows_html = ""
    if comp_stats:
        sorted_comps = sorted(comp_stats.values(), key=lambda c: c["date"], reverse=True)
        for cs in sorted_comps:
            tier_badge = ('<span class="tier-badge major">Major</span>'
                          if cs["tier"] == 1
                          else '<span class="tier-badge open">Open</span>')
            total_teams = sum(cs["teams_by_size"].values())
            sizes_detail = ", ".join(f"{s}: {n}" for s, n in sorted(cs["teams_by_size"].items()))
            finished_pct = round(cs["finished_runs_total"] / cs["total_entries"] * 100, 1) if cs["total_entries"] else 0
            avg_rating = cs.get("avg_rating", 0)
            comp_rows_html += (
                f"<tr>"
                f"<td>{base._esc(cs['name'])}</td>"
                f"<td>{cs['date']}</td>"
                f"<td>{tier_badge}</td>"
                f"<td class='num' title='{sizes_detail}'>{total_teams}</td>"
                f"<td class='num'>{cs['runs_total']}</td>"
                f"<td class='num'>{avg_rating:.0f}</td>"
                f"<td class='num'>{finished_pct:.1f}%</td>"
                f"</tr>\n"
            )

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
.nav-tabs {{ display: flex; gap: 0; margin-bottom: 24px; border-bottom: 2px solid #e5e7eb; }}
.nav-tab {{ padding: 10px 20px; border: none; background: none; cursor: pointer; font-size: 15px; font-weight: 500; color: #666; border-bottom: 2px solid transparent; margin-bottom: -2px; transition: all 0.2s; }}
.nav-tab:hover {{ color: #333; }}
.nav-tab.active {{ color: #2563eb; border-bottom-color: #2563eb; }}
.section {{ display: none; }}
.section.active {{ display: block; }}
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
.tier-badge {{ display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }}
.tier-badge.major {{ background: #fef3c7; color: #92400e; }}
.tier-badge.open {{ background: #e0e7ff; color: #3730a3; }}
.methodology {{ max-width: 800px; }}
.methodology h2 {{ margin: 28px 0 12px; font-size: 20px; color: #1e293b; }}
.methodology h2:first-child {{ margin-top: 0; }}
.methodology h3 {{ margin: 20px 0 8px; font-size: 16px; color: #334155; }}
.methodology p {{ margin: 8px 0; line-height: 1.6; color: #475569; }}
.methodology code {{ background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 13px; color: #0f172a; }}
.methodology ul {{ margin: 8px 0 8px 20px; line-height: 1.6; color: #475569; }}
.methodology .formula {{ background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin: 12px 0; font-family: 'SF Mono', Monaco, monospace; font-size: 14px; color: #1e293b; }}
.methodology .param-table {{ width: 100%; border-collapse: collapse; margin: 12px 0; }}
.methodology .param-table th {{ background: #f8f9fa; padding: 8px 12px; text-align: left; font-size: 13px; border-bottom: 2px solid #e5e7eb; }}
.methodology .param-table td {{ padding: 8px 12px; border-bottom: 1px solid #f0f0f0; font-size: 14px; }}
</style>
</head>
<body>
    <h1>ADW Live Rating</h1>
    <p class="subtitle">Data from {cutoff_date} to {latest_date} ({LIVE_WINDOW_DAYS} days window), min {MIN_RUNS_FOR_LIVE_RANKING} runs to qualify.</p>

    <div class="nav-tabs">
        <button class="nav-tab active" onclick="showSection('rankings', this)">Rankings</button>
        <button class="nav-tab" onclick="showSection('competitions', this)">Competitions ({len(comp_stats) if comp_stats else 0})</button>
        <button class="nav-tab" onclick="showSection('methodology', this)">How it works</button>
    </div>

    <!-- RANKINGS SECTION -->
    <div id="section-rankings" class="section active">
        <div class="filters">
            <div class="search-box">
                <input id="search" type="text" placeholder="Search handler, dog, country..." oninput="filterRows()">
            </div>
        </div>

        <div class="tabs">
            {"".join(tab_buttons)}
        </div>

        {"".join(tab_contents)}
    </div>

    <!-- COMPETITIONS SECTION -->
    <div id="section-competitions" class="section">
        <table class="rating-table" id="table-competitions">
            <thead>
                <tr>
                    <th onclick="sortTable('table-competitions', 0, 'str')">Competition</th>
                    <th onclick="sortTable('table-competitions', 1, 'str')">Date</th>
                    <th onclick="sortTable('table-competitions', 2, 'str')">Tier</th>
                    <th onclick="sortTable('table-competitions', 3, 'num')">Teams</th>
                    <th onclick="sortTable('table-competitions', 4, 'num')">Rounds</th>
                    <th onclick="sortTable('table-competitions', 5, 'num')">Avg Rating</th>
                    <th onclick="sortTable('table-competitions', 6, 'num')">Finished %</th>
                </tr>
            </thead>
            <tbody>
                {comp_rows_html}
            </tbody>
        </table>
    </div>

    <!-- METHODOLOGY SECTION -->
    <div id="section-methodology" class="section">
        <div class="methodology">
            <h2>How ADW Live Rating works</h2>
            <p>ADW Live Rating is a ranking system for agility teams (handler + dog) based on results from international competitions. It uses the <strong>OpenSkill (Plackett-Luce)</strong> statistical model, a modern alternative to Elo or Glicko — unlike those systems, it can work with full run rankings (not just win/loss) and is designed for multi-participant competitions.</p>

            <h2>Core concept</h2>
            <p>Each team has two internal parameters:</p>
            <ul>
                <li><strong>mu (μ)</strong> — estimate of the team's true skill. Higher = better.</li>
                <li><strong>sigma (σ)</strong> — uncertainty measure. New teams have high sigma; it decreases with more runs.</li>
            </ul>
            <p>After each run, the model compares the actual result with the expected ranking and adjusts mu and sigma for all participants.</p>

            <h3>Sigma decay</h3>
            <p>After each processed run, sigma is multiplied by a factor of <code>{LIVE_SIGMA_DECAY}</code>. This means confidence in the team's rating grows with more data. Minimum sigma is <code>{base.SIGMA_MIN}</code>.</p>

            <h2>Public rating calculation</h2>
            <div class="formula">
                rating_base = {DISPLAY_BASE} + {DISPLAY_SCALE} × (μ − {RATING_SIGMA_MULTIPLIER} × σ)<br>
                rating = rating_base × quality_factor
            </div>

            <h3>Podium boost (quality factor)</h3>
            <p>The public rating is adjusted based on the quality of a team's results:</p>
            <div class="formula">
                quality_factor = {PODIUM_BOOST_BASE:.2f} + {PODIUM_BOOST_RANGE:.2f} × clamp(top3% / {PODIUM_BOOST_TARGET:.0f})
            </div>
            <p>Quality factor ranges from <code>{PODIUM_BOOST_BASE:.2f}</code> (no top-3 finishes) to <code>{PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE:.2f}</code> (frequent podium placements). Teams that consistently finish in the top 3 receive a significant bonus, while teams without podium results have their rating reduced.</p>

            <h2>Competition weighting (tier system)</h2>
            <p>Competitions are divided into two tiers:</p>
            <ul>
                <li><strong>Major (tier 1)</strong> — AWC, EO, JOAWC/SOAWC — runs carry <code>{MAJOR_EVENT_WEIGHT}×</code> weight</li>
                <li><strong>Open (tier 2)</strong> — other international competitions — standard <code>1.0×</code> weight</li>
            </ul>
            <p>Higher weight for Major events means results from these prestigious competitions have a greater impact on rating changes.</p>

            <h2>Skill tiers</h2>
            <table class="param-table">
                <tr><th>Tier</th><th>Condition</th><th>Color</th></tr>
                <tr><td><strong>Elite</strong></td><td>Top {base.ELITE_TOP_PERCENT*100:.0f}% in category</td><td style="border-left:3px solid #f59e0b; padding-left:12px;">gold</td></tr>
                <tr><td><strong>Champion</strong></td><td>Top {base.CHAMPION_TOP_PERCENT*100:.0f}%</td><td style="border-left:3px solid #8b5cf6; padding-left:12px;">purple</td></tr>
                <tr><td><strong>Expert</strong></td><td>Top {base.EXPERT_TOP_PERCENT*100:.0f}%</td><td style="border-left:3px solid #3b82f6; padding-left:12px;">blue</td></tr>
                <tr><td><strong>Competitor</strong></td><td>Everyone else</td><td style="border-left:3px solid #10b981; padding-left:12px;">green</td></tr>
            </table>

            <h2>Few runs status</h2>
            <p>A team with <code>σ ≥ {LIVE_PROVISIONAL_SIGMA_THRESHOLD}</code> is marked as <span class="prov-badge">FEW RUNS</span> — this means the system doesn't yet have enough data for a reliable estimate. The rating will stabilize with more runs.</p>

            <h2>Live window and minimum runs</h2>
            <ul>
                <li>Only runs from the last <strong>{LIVE_WINDOW_DAYS} days</strong> (~{LIVE_WINDOW_DAYS / 365:.1f} years) from the latest competition in the dataset are counted.</li>
                <li>A minimum of <strong>{MIN_RUNS_FOR_LIVE_RANKING} runs</strong> within this window is required to appear in the rankings.</li>
                <li>Minimum participants per run for it to count: <strong>{base.MIN_FIELD_SIZE}</strong></li>
            </ul>

            <h2>System parameters</h2>
            <table class="param-table">
                <tr><th>Parameter</th><th>Value</th><th>Description</th></tr>
                <tr><td><code>LIVE_WINDOW_DAYS</code></td><td>{LIVE_WINDOW_DAYS}</td><td>Time window for counting runs</td></tr>
                <tr><td><code>MIN_RUNS</code></td><td>{MIN_RUNS_FOR_LIVE_RANKING}</td><td>Minimum runs to appear in rankings</td></tr>
                <tr><td><code>SIGMA_DECAY</code></td><td>{LIVE_SIGMA_DECAY}</td><td>Uncertainty reduction factor per run</td></tr>
                <tr><td><code>SIGMA_MIN</code></td><td>{base.SIGMA_MIN}</td><td>Minimum sigma (floor)</td></tr>
                <tr><td><code>PROV_THRESHOLD</code></td><td>{LIVE_PROVISIONAL_SIGMA_THRESHOLD}</td><td>Sigma threshold for provisional status</td></tr>
                <tr><td><code>DISPLAY_BASE</code></td><td>{DISPLAY_BASE}</td><td>Public rating base</td></tr>
                <tr><td><code>DISPLAY_SCALE</code></td><td>{DISPLAY_SCALE}</td><td>Scaling factor</td></tr>
                <tr><td><code>MAJOR_WEIGHT</code></td><td>{MAJOR_EVENT_WEIGHT}</td><td>Major event weight multiplier</td></tr>
                <tr><td><code>PODIUM_BASE</code></td><td>{PODIUM_BOOST_BASE}</td><td>Minimum quality factor</td></tr>
                <tr><td><code>PODIUM_RANGE</code></td><td>{PODIUM_BOOST_RANGE}</td><td>Quality factor range</td></tr>
                <tr><td><code>MIN_FIELD_SIZE</code></td><td>{base.MIN_FIELD_SIZE}</td><td>Minimum participants per run</td></tr>
            </table>
        </div>
    </div>

<script>
let currentTab = '{sizes[0] if sizes else ""}';

function showSection(sectionId, buttonEl) {{
    document.querySelectorAll('.section').forEach(s => s.classList.remove('active'));
    document.getElementById('section-' + sectionId).classList.add('active');
    document.querySelectorAll('.nav-tab').forEach(btn => btn.classList.remove('active'));
    if (buttonEl) buttonEl.classList.add('active');
}}

function showTab(size, buttonEl) {{
    currentTab = size;
    document.querySelectorAll('.tab-content').forEach(tab => tab.style.display = 'none');
    document.getElementById('tab-' + size).style.display = 'block';
    document.querySelectorAll('.tab-btn').forEach(btn => btn.classList.remove('active'));
    if (buttonEl) {{
        buttonEl.classList.add('active');
    }}
    filterRows();
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
    const table = document.getElementById('table-' + currentTab);
    if (!table) return;
    const rows = table.querySelectorAll('tbody tr');
    rows.forEach(row => {{
        row.style.display = row.textContent.toLowerCase().includes(q) ? '' : 'none';
    }});
}}
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

    all_ratings, cutoff_date, latest_date, comp_stats = calculate_live_ratings(runs, profiles)
    total_teams = sum(len(size_map) for size_map in all_ratings.values())
    print(f"\nTotal unique teams rated (inside live window): {total_teams}")

    write_csv_live(all_ratings)
    write_html_live(all_ratings, cutoff_date, latest_date, comp_stats)
    print("Done!")


if __name__ == "__main__":
    main()
