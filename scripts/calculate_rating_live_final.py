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

# Cross-size normalization: z-score each size to a common scale
NORMALIZE_ACROSS_SIZES = True
NORM_TARGET_MEAN = 1500.0
NORM_TARGET_STD = 150.0

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
                            "prev_mu": None,
                            "prev_sigma": None,
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

                # Snapshot current ratings before update (for trend arrows)
                for team_id in entry_order:
                    team_stats[team_id]["prev_mu"] = team_ratings[team_id].mu
                    team_stats[team_id]["prev_sigma"] = team_ratings[team_id].sigma

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
            prev_mu = stats.get("prev_mu")
            prev_sigma = stats.get("prev_sigma")
            prev_rating = None
            if prev_mu is not None and prev_sigma is not None:
                prev_rating_base = live_rating(prev_mu, prev_sigma)
                prev_rating = round(prev_rating_base * quality_factor, 1)
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
                "prev_rating": prev_rating,
            }

        sorted_teams = sorted(size_results.items(), key=lambda x: -x[1]["rating"])
        print(f"  {len(sorted_teams)} unique teams rated")
        if sorted_teams:
            top = sorted_teams[0][1]
            print(f"  Top: {top['handler']} / {top['dog']} ({top['country']}) — {top['rating']}")

        all_ratings[size] = size_results

    # Cross-size normalization: map each size to common mean/std
    if NORMALIZE_ACROSS_SIZES:
        for size in sorted(all_ratings.keys()):
            qualified = [
                t for t in all_ratings[size].values()
                if t["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING
            ]
            if len(qualified) < 2:
                continue
            ratings = [t["rating"] for t in qualified]
            size_mean = sum(ratings) / len(ratings)
            size_std = (sum((r - size_mean) ** 2 for r in ratings) / len(ratings)) ** 0.5
            if size_std < 1:
                continue
            for team in all_ratings[size].values():
                z = (team["rating"] - size_mean) / size_std
                team["rating"] = round(NORM_TARGET_MEAN + NORM_TARGET_STD * z, 1)
                if team.get("prev_rating") is not None:
                    z_prev = (team["prev_rating"] - size_mean) / size_std
                    team["prev_rating"] = round(NORM_TARGET_MEAN + NORM_TARGET_STD * z_prev, 1)
            print(f"  {size}: normalized (mean {size_mean:.0f}→{NORM_TARGET_MEAN:.0f}, std {size_std:.0f}→{NORM_TARGET_STD:.0f})")

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

        # Build previous-rank lookup for trend arrows
        prev_ranked = [t for t in sorted_teams if t.get("prev_rating") is not None]
        prev_ranked.sort(key=lambda t: -t["prev_rating"])
        prev_rank_map = {}
        for prev_rank, team in enumerate(prev_ranked, 1):
            prev_rank_map[(team["handler"], team["dog"])] = prev_rank

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

            # Medal badges for top 3
            rank_class = f" rank-{rank}" if rank <= 3 else ""
            if rank == 1:
                rank_cell = "<span class='medal gold'>1</span>"
            elif rank == 2:
                rank_cell = "<span class='medal silver'>2</span>"
            elif rank == 3:
                rank_cell = "<span class='medal bronze'>3</span>"
            else:
                rank_cell = str(rank)

            # Rank change arrow
            key = (team["handler"], team["dog"])
            if team.get("prev_rating") is None:
                trend_html = "<span class='trend trend-new'>NEW</span>"
            elif key in prev_rank_map:
                rank_change = prev_rank_map[key] - rank
                if rank_change > 0:
                    trend_html = f"<span class='trend trend-up'>&#9650;{rank_change}</span>"
                elif rank_change < 0:
                    trend_html = f"<span class='trend trend-down'>&#9660;{abs(rank_change)}</span>"
                else:
                    trend_html = "<span class='trend trend-same'>&mdash;</span>"
            else:
                trend_html = "<span class='trend trend-new'>NEW</span>"

            rows.append(
                f"<tr class='tier-{team.get('skill_tier', 'Competitor').lower()}{rank_class}'>"
                f"<td class='rank-cell'>{rank_cell}</td>"
                f"<td>{trend_html} {handler_cell}</td>"
                f"<td>{dog_cell}</td>"
                f"<td>{base._esc(team['country'])}</td>"
                f"<td class='num rating-cell'>{team['rating']:.0f}</td>"
                f"<td class='num'>{team['num_runs']}</td>"
                f"<td class='num'>{team['finished_pct']:.1f}%</td>"
                f"<td class='num'>{team['top3_pct']:.1f}%</td>"
                f"</tr>"
            )

        tables[size] = "\n".join(rows)

    # Compute static summary stats for header cards
    total_teams = sum(
        sum(1 for t in s.values() if t["num_runs"] >= MIN_RUNS_FOR_LIVE_RANKING)
        for s in all_ratings.values()
    )
    total_comps = len(comp_stats) if comp_stats else 0
    total_rounds = sum(cs["runs_total"] for cs in comp_stats.values()) if comp_stats else 0

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
          <div class="table-card">
            <table class="rating-table" id="table-{size}">
                <thead>
                    <tr>
                        <th onclick="sortTable('table-{size}', 0, 'num')">#</th>
                        <th onclick="sortTable('table-{size}', 1, 'str')">Handler</th>
                        <th onclick="sortTable('table-{size}', 2, 'str')">Dog</th>
                        <th onclick="sortTable('table-{size}', 3, 'str')">Country</th>
                        <th class="num" onclick="sortTable('table-{size}', 4, 'num')">Rating</th>
                        <th class="num" onclick="sortTable('table-{size}', 5, 'num')">Runs</th>
                        <th class="num" onclick="sortTable('table-{size}', 6, 'num')">Finished</th>
                        <th class="num" onclick="sortTable('table-{size}', 7, 'num')">TOP3</th>
                    </tr>
                </thead>
                <tbody>
                    {tables[size]}
                </tbody>
            </table>
          </div>
        </div>""")

    # --- Build competition stats rows ---
    comp_rows_html = ""
    if comp_stats:
        sorted_comps = sorted(comp_stats.values(), key=lambda c: c["date"], reverse=True)
        for cs in sorted_comps:
            tier_badge = ('<span class="tier-badge major">Major</span>'
                          if cs["tier"] == 1
                          else '<span class="tier-badge open">Open</span>')
            comp_team_count = sum(cs["teams_by_size"].values())
            sizes_detail = ", ".join(f"{s}: {n}" for s, n in sorted(cs["teams_by_size"].items()))
            finished_pct = round(cs["finished_runs_total"] / cs["total_entries"] * 100, 1) if cs["total_entries"] else 0
            avg_rating = cs.get("avg_rating", 0)
            comp_rows_html += (
                f"<tr>"
                f"<td>{base._esc(cs['name'])}</td>"
                f"<td>{cs['date']}</td>"
                f"<td>{tier_badge}</td>"
                f"<td class='num' title='{sizes_detail}'>{comp_team_count}</td>"
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
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap');

:root {{
    --bg-primary: #0a0e17;
    --bg-secondary: #0f1629;
    --bg-card: rgba(15, 23, 42, 0.8);
    --bg-card-hover: rgba(30, 41, 59, 0.6);
    --border-subtle: rgba(99, 102, 241, 0.15);
    --border-medium: rgba(99, 102, 241, 0.25);
    --text-primary: #e2e8f0;
    --text-secondary: #94a3b8;
    --text-muted: #64748b;
    --accent: #6366f1;
    --accent-light: #818cf8;
    --accent-glow: rgba(99, 102, 241, 0.3);
    --elite: #f59e0b;
    --champion: #a78bfa;
    --expert: #60a5fa;
    --competitor: #34d399;
    --gold: linear-gradient(135deg, #f59e0b, #d97706);
    --silver: linear-gradient(135deg, #94a3b8, #64748b);
    --bronze: linear-gradient(135deg, #c2854a, #92400e);
    --glass-blur: blur(20px);
    --radius: 12px;
    --radius-sm: 8px;
}}

* {{ margin: 0; padding: 0; box-sizing: border-box; }}

body {{
    font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    background: var(--bg-primary);
    color: var(--text-primary);
    min-height: 100vh;
    background-image:
        radial-gradient(ellipse 80% 50% at 50% -20%, rgba(99, 102, 241, 0.12), transparent),
        radial-gradient(ellipse 60% 40% at 80% 50%, rgba(139, 92, 246, 0.06), transparent);
}}

/* Hero Header */
.hero {{
    padding: 48px 24px 32px;
    text-align: center;
    position: relative;
}}

.hero h1 {{
    font-size: 40px;
    font-weight: 700;
    letter-spacing: -0.5px;
    margin-bottom: 4px;
}}

.hero h1 .gradient-text {{
    background: linear-gradient(135deg, var(--accent-light), #c084fc);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
}}

.hero .subtitle {{
    color: var(--text-secondary);
    font-size: 14px;
    font-weight: 400;
    margin-bottom: 24px;
}}

.stat-cards {{
    display: flex;
    gap: 16px;
    justify-content: center;
    flex-wrap: wrap;
}}

.stat-card {{
    background: var(--bg-card);
    backdrop-filter: var(--glass-blur);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-sm);
    padding: 16px 24px;
    min-width: 140px;
    text-align: center;
}}

.stat-card .stat-value {{
    font-size: 24px;
    font-weight: 700;
    color: var(--accent-light);
    font-variant-numeric: tabular-nums;
}}

.stat-card .stat-label {{
    font-size: 11px;
    text-transform: uppercase;
    letter-spacing: 1px;
    color: var(--text-muted);
    margin-top: 4px;
}}

.accent-bar {{
    height: 2px;
    background: linear-gradient(90deg, transparent, var(--accent), transparent);
    margin: 0 auto;
    max-width: 600px;
}}

/* Sticky Nav */
.main-nav {{
    position: sticky;
    top: 0;
    z-index: 100;
    background: rgba(10, 14, 23, 0.85);
    backdrop-filter: var(--glass-blur);
    border-bottom: 1px solid var(--border-subtle);
    padding: 12px 24px;
    display: flex;
    gap: 8px;
    justify-content: center;
}}

.main-nav .nav-pill {{
    padding: 8px 20px;
    border: 1px solid var(--border-subtle);
    background: transparent;
    color: var(--text-secondary);
    border-radius: 99px;
    cursor: pointer;
    font-size: 14px;
    font-weight: 500;
    font-family: inherit;
    transition: all 0.2s ease;
    display: inline-flex;
    align-items: center;
    gap: 6px;
}}

.main-nav .nav-pill:hover {{
    color: var(--text-primary);
    border-color: var(--border-medium);
    background: var(--bg-card-hover);
}}

.main-nav .nav-pill.active {{
    background: var(--accent);
    color: #fff;
    border-color: var(--accent);
}}

.main-nav .nav-pill svg {{
    width: 16px;
    height: 16px;
    opacity: 0.7;
}}

/* Content Area */
.content-area {{
    max-width: 1280px;
    margin: 0 auto;
    padding: 24px;
}}

.section {{ display: none; opacity: 0; transition: opacity 0.3s ease; }}
.section.active {{ display: block; opacity: 1; }}

/* Toolbar (tabs + search on one line) */
.toolbar {{
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 16px;
    margin-bottom: 20px;
    flex-wrap: wrap;
}}

.search-box {{
    position: relative;
}}

.search-box svg {{
    position: absolute;
    left: 12px;
    top: 50%;
    transform: translateY(-50%);
    width: 16px;
    height: 16px;
    color: var(--text-muted);
}}

.search-box input {{
    padding: 10px 12px 10px 36px;
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-sm);
    font-size: 14px;
    font-family: inherit;
    width: 340px;
    max-width: 100%;
    background: var(--bg-card);
    color: var(--text-primary);
    transition: border-color 0.2s;
}}

.search-box input::placeholder {{ color: var(--text-muted); }}
.search-box input:focus {{ outline: none; border-color: var(--accent); box-shadow: 0 0 0 3px var(--accent-glow); }}

/* Size Tabs (segmented control) */
.size-tabs {{
    display: flex;
    gap: 4px;
    flex-wrap: wrap;
    background: var(--bg-card);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-sm);
    padding: 4px;
    width: fit-content;
}}

.tab-btn {{
    padding: 8px 18px;
    border: none;
    background: transparent;
    border-radius: 6px;
    cursor: pointer;
    font-size: 14px;
    font-weight: 500;
    font-family: inherit;
    color: var(--text-secondary);
    transition: all 0.2s ease;
}}

.tab-btn:hover {{ color: var(--text-primary); background: var(--bg-card-hover); }}

.tab-btn.active {{
    background: var(--accent);
    color: #fff;
}}

.tab-btn .count {{ font-weight: 400; opacity: 0.7; font-size: 12px; }}

/* Table Card (glass container) */
.table-card {{
    background: var(--bg-card);
    backdrop-filter: var(--glass-blur);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius);
    overflow: hidden;
}}

/* Rating Table */
.rating-table {{ width: 100%; border-collapse: collapse; }}

.rating-table th {{
    background: rgba(15, 23, 42, 0.95);
    padding: 12px 14px;
    text-align: left;
    font-size: 11px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.8px;
    color: var(--text-muted);
    cursor: pointer;
    user-select: none;
    border-bottom: 1px solid var(--border-subtle);
    position: relative;
    transition: color 0.2s;
}}

.rating-table th:hover {{ color: var(--text-secondary); }}

.rating-table th.sorted-asc::after {{ content: ' \\25B2'; font-size: 9px; }}
.rating-table th.sorted-desc::after {{ content: ' \\25BC'; font-size: 9px; }}

.rating-table td {{
    padding: 10px 14px;
    border-bottom: 1px solid rgba(30, 41, 59, 0.5);
    font-size: 14px;
    color: var(--text-primary);
}}

.rating-table td.num {{
    text-align: right;
    font-variant-numeric: tabular-nums;
    color: var(--text-secondary);
}}

.rating-table td.rating-cell {{
    font-weight: 700;
    font-size: 15px;
    color: #fff !important;
}}

.rating-table td.rank-cell {{
    width: 48px;
    text-align: center;
    color: var(--text-muted);
    font-weight: 500;
}}

.rating-table tbody tr {{
    transition: background 0.15s ease;
}}

.rating-table tbody tr:hover {{
    background: rgba(99, 102, 241, 0.06);
}}

/* Tier borders */
.tier-elite td:first-child {{ border-left: 3px solid var(--elite); }}
.tier-champion td:first-child {{ border-left: 3px solid var(--champion); }}
.tier-expert td:first-child {{ border-left: 3px solid var(--expert); }}
.tier-competitor td:first-child {{ border-left: 3px solid var(--competitor); }}

/* Top 3 row highlights */
.rank-1 {{ background: rgba(245, 158, 11, 0.06); }}
.rank-2 {{ background: rgba(148, 163, 184, 0.04); }}
.rank-3 {{ background: rgba(194, 133, 74, 0.04); }}

/* Medal badges */
.medal {{
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    border-radius: 50%;
    font-size: 13px;
    font-weight: 700;
    color: #fff;
}}

.medal.gold {{
    background: var(--gold);
    box-shadow: 0 0 12px rgba(245, 158, 11, 0.4);
}}

.medal.silver {{
    background: var(--silver);
    box-shadow: 0 0 12px rgba(148, 163, 184, 0.3);
}}

.medal.bronze {{
    background: var(--bronze);
    box-shadow: 0 0 12px rgba(194, 133, 74, 0.3);
}}

/* Misc */
.reg-name {{ font-size: 11px; color: var(--text-muted); }}

.prov-badge {{
    display: inline-block;
    margin-left: 6px;
    padding: 2px 6px;
    border-radius: 4px;
    background: rgba(99, 102, 241, 0.15);
    color: var(--accent-light);
    font-size: 10px;
    font-weight: 700;
    letter-spacing: 0.3px;
    vertical-align: middle;
}}

/* Trend arrows */
.trend {{ font-size: 12px; font-weight: 600; margin-right: 6px; }}
.trend-up {{ color: var(--competitor); }}
.trend-down {{ color: #ef4444; }}
.trend-same {{ color: var(--text-muted); }}
.trend-new {{ color: var(--accent-light); font-size: 10px; }}

/* Numeric header alignment */
.rating-table th.num {{ text-align: right; }}

.tier-badge {{
    display: inline-block;
    padding: 3px 10px;
    border-radius: 99px;
    font-size: 12px;
    font-weight: 600;
}}

.tier-badge.major {{
    background: transparent;
    color: var(--elite);
    border: 1px solid rgba(245, 158, 11, 0.4);
}}

.tier-badge.open {{
    background: transparent;
    color: var(--accent-light);
    border: 1px solid rgba(99, 102, 241, 0.3);
}}

/* Methodology */
.methodology {{ max-width: 800px; }}

.methodology h2 {{
    margin: 32px 0 12px;
    font-size: 20px;
    color: var(--text-primary);
    font-weight: 600;
}}

.methodology h2:first-child {{ margin-top: 0; }}

.methodology h3 {{
    margin: 24px 0 8px;
    font-size: 16px;
    color: var(--text-secondary);
    font-weight: 600;
}}

.methodology p {{
    margin: 8px 0;
    line-height: 1.7;
    color: var(--text-secondary);
}}

.methodology strong {{ color: var(--text-primary); }}

.methodology code {{
    background: rgba(99, 102, 241, 0.1);
    padding: 2px 6px;
    border-radius: 4px;
    font-size: 13px;
    font-family: 'JetBrains Mono', monospace;
    color: var(--accent-light);
}}

.methodology ul {{
    margin: 8px 0 8px 20px;
    line-height: 1.7;
    color: var(--text-secondary);
}}

.methodology .formula {{
    background: rgba(99, 102, 241, 0.08);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-sm);
    padding: 16px 20px;
    margin: 12px 0;
    font-family: 'JetBrains Mono', monospace;
    font-size: 14px;
    color: var(--text-primary);
}}

.methodology .param-table {{
    width: 100%;
    border-collapse: collapse;
    margin: 12px 0;
}}

.methodology .param-table th {{
    background: rgba(15, 23, 42, 0.95);
    padding: 10px 14px;
    text-align: left;
    font-size: 12px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    color: var(--text-muted);
    border-bottom: 1px solid var(--border-subtle);
}}

.methodology .param-table td {{
    padding: 10px 14px;
    border-bottom: 1px solid rgba(30, 41, 59, 0.5);
    font-size: 14px;
    color: var(--text-secondary);
}}

/* Responsive */
@media (max-width: 768px) {{
    .hero {{ padding: 32px 16px 24px; }}
    .hero h1 {{ font-size: 28px; }}
    .stat-cards {{ display: none; }}
    .content-area {{ padding: 16px; }}
    .table-card {{ overflow-x: auto; }}
    .rating-table {{ min-width: 700px; }}
    .search-box input {{ width: 100%; }}
    .main-nav {{ gap: 4px; padding: 10px 12px; }}
    .main-nav .nav-pill {{ padding: 6px 14px; font-size: 13px; }}
    .size-tabs {{ width: 100%; }}
}}
</style>
</head>
<body>
    <header class="hero">
        <h1>ADW <span class="gradient-text">Live Rating</span></h1>
        <p class="subtitle">Experimental algorithm &middot; data window {cutoff_date} to {latest_date}</p>
        <div class="stat-cards">
            <div class="stat-card">
                <div class="stat-value">{total_teams}</div>
                <div class="stat-label">Teams</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">{total_comps}</div>
                <div class="stat-label">Competitions</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">{total_rounds}</div>
                <div class="stat-label">Runs</div>
            </div>
        </div>
    </header>
    <div class="accent-bar"></div>

    <nav class="main-nav">
        <button class="nav-pill active" onclick="showSection('rankings', this)">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/></svg>
            Rankings
        </button>
        <button class="nav-pill" onclick="showSection('competitions', this)">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M6 2L3 6v14a2 2 0 002 2h14a2 2 0 002-2V6l-3-4zM3 6h18"/></svg>
            Competitions ({len(comp_stats) if comp_stats else 0})
        </button>
        <button class="nav-pill" onclick="showSection('methodology', this)">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><path d="M9.09 9a3 3 0 015.83 1c0 2-3 3-3 3M12 17h.01"/></svg>
            How it works
        </button>
    </nav>

    <main class="content-area">
    <!-- RANKINGS SECTION -->
    <div id="section-rankings" class="section active">
        <div class="toolbar">
            <div class="size-tabs">
                {"".join(tab_buttons)}
            </div>
            <div class="search-box">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/></svg>
                <input id="search" type="text" placeholder="Search handler, dog, country..." oninput="filterRows()">
            </div>
        </div>

        {"".join(tab_contents)}
    </div>

    <!-- COMPETITIONS SECTION -->
    <div id="section-competitions" class="section">
        <div class="table-card">
        <table class="rating-table" id="table-competitions">
            <thead>
                <tr>
                    <th onclick="sortTable('table-competitions', 0, 'str')">Competition</th>
                    <th onclick="sortTable('table-competitions', 1, 'str')">Date</th>
                    <th onclick="sortTable('table-competitions', 2, 'str')">Tier</th>
                    <th class="num" onclick="sortTable('table-competitions', 3, 'num')">Teams</th>
                    <th class="num" onclick="sortTable('table-competitions', 4, 'num')">Rounds</th>
                    <th class="num" onclick="sortTable('table-competitions', 5, 'num')">Avg Rating</th>
                    <th class="num" onclick="sortTable('table-competitions', 6, 'num')">Finished</th>
                </tr>
            </thead>
            <tbody>
                {comp_rows_html}
            </tbody>
        </table>
        </div>
    </div>

    <!-- METHODOLOGY SECTION -->
    <div id="section-methodology" class="section">
        <div class="methodology">
            <h2>How ADW Live Rating works</h2>
            <p>ADW Live Rating ranks agility teams (handler + dog) based on their results at international competitions. Think of it like a chess rating, but for agility &mdash; the more you compete and the better you place, the higher your rating climbs.</p>

            <h2>The big picture</h2>
            <p>The system processes competitions <strong>in chronological order</strong>. For every run (a single round in a competition), it looks at how each team placed relative to everyone else in that run. Teams that beat expectations &mdash; finishing higher than their rating would predict &mdash; gain points. Teams that underperform lose points. Over time, the rating converges on a team's true skill level.</p>
            <p>Under the hood we use <strong>OpenSkill (Plackett-Luce)</strong>, a modern statistical model similar to the Elo system used in chess, but designed for competitions where many participants are ranked at once (not just two players head-to-head).</p>

            <h2>Step by step</h2>

            <h3>1. Collect results</h3>
            <p>We import results from international agility competitions. Each run is a single scored round &mdash; for example, "Individual Jumping Large, run 1" at EO 2024. A run must have at least <strong>{base.MIN_FIELD_SIZE} teams</strong> to count.</p>

            <h3>2. Process runs chronologically</h3>
            <p>Every team starts with the same default rating. Then, competition by competition and run by run, the algorithm updates all teams that participated:</p>
            <ul>
                <li><strong>Beat stronger opponents?</strong> Your rating goes up more.</li>
                <li><strong>Lost to weaker opponents?</strong> Your rating goes down more.</li>
                <li><strong>Eliminated (DIS/DSQ)?</strong> You're placed last in that run &mdash; it hurts your rating, but one bad day won't ruin everything.</li>
            </ul>
            <p>The size of the rating change depends on how surprising the result was. A big upset causes a big swing; a predictable finish barely moves the needle.</p>

            <h3>3. Confidence grows over time</h3>
            <p>New teams start with high <strong>uncertainty</strong> &mdash; the system doesn't know yet how good they really are. With each run, uncertainty shrinks and the rating becomes more stable. Teams with very few runs are marked <span class="prov-badge">FEW RUNS</span> to signal that their rating may still change significantly.</p>

            <h3>4. Podium bonus</h3>
            <p>Teams that consistently finish in the <strong>top 3</strong> receive a quality bonus of up to <strong>+{(PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE - PODIUM_BOOST_BASE) * 100:.0f}%</strong> on their rating. Teams without any podium finishes get a small penalty (<strong>&minus;{(1.0 - PODIUM_BOOST_BASE) * 100:.0f}%</strong>). This rewards not just beating weak fields, but actually winning competitive runs.</p>

            <h3>5. Major event boost</h3>
            <p>Results from <strong>Major competitions</strong> (AWC, EO, JOAWC/SOAWC) carry <strong>{MAJOR_EVENT_WEIGHT}&times;</strong> the weight of regular Open competitions. Winning a run at AWC moves your rating more than winning a run at a regional open.</p>

            <h3>6. Cross-size normalization</h3>
            <p>Each size category (Small, Medium, Intermediate, Large) is rated separately. Since some categories have more teams or tighter competition, we normalize the ratings so they are <strong>comparable across sizes</strong>. An average team in any category lands around <strong>{NORM_TARGET_MEAN:.0f}</strong> points. This means you can compare a Small team's rating to a Large team's rating on the same scale.</p>

            <h2>What's included</h2>
            <ul>
                <li>Only runs from the last <strong>{LIVE_WINDOW_DAYS} days</strong> (~{LIVE_WINDOW_DAYS // 365} years) are counted &mdash; the rating reflects recent form, not lifetime history.</li>
                <li>A team needs at least <strong>{MIN_RUNS_FOR_LIVE_RANKING} runs</strong> in this window to appear in the rankings.</li>
                <li>Both individual and team rounds count (using individual placements within team rounds).</li>
            </ul>

            <h2>Skill tiers</h2>
            <p>Based on where a team's rating falls within its size category:</p>
            <table class="param-table">
                <tr><th>Tier</th><th>What it means</th><th></th></tr>
                <tr><td><strong>Elite</strong></td><td>Top {base.ELITE_TOP_PERCENT*100:.0f}% &mdash; the absolute best</td><td style="border-left:3px solid var(--elite); padding-left:12px;">gold</td></tr>
                <tr><td><strong>Champion</strong></td><td>Top {base.CHAMPION_TOP_PERCENT*100:.0f}% &mdash; consistently excellent</td><td style="border-left:3px solid var(--champion); padding-left:12px;">purple</td></tr>
                <tr><td><strong>Expert</strong></td><td>Top {base.EXPERT_TOP_PERCENT*100:.0f}% &mdash; above average</td><td style="border-left:3px solid var(--expert); padding-left:12px;">blue</td></tr>
                <tr><td><strong>Competitor</strong></td><td>Everyone else &mdash; still competing at the international level!</td><td style="border-left:3px solid var(--competitor); padding-left:12px;">green</td></tr>
            </table>

            <h2>Reading the numbers</h2>
            <ul>
                <li><strong>Rating ~{NORM_TARGET_MEAN:.0f}</strong> = average among ranked teams in that size category.</li>
                <li><strong>Rating ~{NORM_TARGET_MEAN + NORM_TARGET_STD:.0f}</strong> = clearly above average (top ~16%).</li>
                <li><strong>Rating ~{NORM_TARGET_MEAN + 2*NORM_TARGET_STD:.0f}+</strong> = exceptional, among the very best in the world.</li>
                <li><strong>Trend arrows</strong> (&#9650;&#9660;) show how a team's rank changed since the last competition was added.</li>
            </ul>

            <h2>Technical details</h2>
            <p>For those interested in the math:</p>
            <div class="formula">
                rating = ({DISPLAY_BASE} + {DISPLAY_SCALE} &times; (&mu; &minus; {RATING_SIGMA_MULTIPLIER} &times; &sigma;)) &times; quality_factor
            </div>
            <p>Where <strong>&mu;</strong> (mu) is the estimated skill, <strong>&sigma;</strong> (sigma) is the uncertainty, and <strong>quality_factor</strong> is the podium bonus ({PODIUM_BOOST_BASE:.2f}&ndash;{PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE:.2f} based on top-3 finish rate).</p>
            <table class="param-table">
                <tr><th>Parameter</th><th>Value</th><th>Description</th></tr>
                <tr><td>Live window</td><td>{LIVE_WINDOW_DAYS} days</td><td>Only recent results count</td></tr>
                <tr><td>Min. runs</td><td>{MIN_RUNS_FOR_LIVE_RANKING}</td><td>Minimum runs to appear in rankings</td></tr>
                <tr><td>Min. field size</td><td>{base.MIN_FIELD_SIZE}</td><td>Minimum teams in a run for it to count</td></tr>
                <tr><td>Major event weight</td><td>{MAJOR_EVENT_WEIGHT}&times;</td><td>AWC, EO, JOAWC/SOAWC boost</td></tr>
                <tr><td>Podium bonus range</td><td>{PODIUM_BOOST_BASE:.0%}&ndash;{PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE:.0%}</td><td>Based on top-3 finish percentage</td></tr>
                <tr><td>Normalization target</td><td>{NORM_TARGET_MEAN:.0f} &plusmn; {NORM_TARGET_STD:.0f}</td><td>Cross-size rating scale</td></tr>
            </table>
        </div>
    </div>
    </main>

<script>
let currentTab = '{sizes[0] if sizes else ""}';

function showSection(sectionId, buttonEl) {{
    document.querySelectorAll('.section').forEach(s => {{
        s.classList.remove('active');
    }});
    const target = document.getElementById('section-' + sectionId);
    // Trigger reflow for opacity transition
    target.offsetHeight;
    target.classList.add('active');
    document.querySelectorAll('.nav-pill').forEach(btn => btn.classList.remove('active'));
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
    const headers = table.querySelectorAll('th');
    const th = headers[colIdx];
    const asc = th.dataset.sort !== 'asc';

    // Clear sort indicators from all headers in this table
    headers.forEach(h => {{
        h.classList.remove('sorted-asc', 'sorted-desc');
        if (h !== th) h.dataset.sort = '';
    }});

    th.dataset.sort = asc ? 'asc' : 'desc';
    th.classList.add(asc ? 'sorted-asc' : 'sorted-desc');

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

// Init
</script>
</body>
</html>"""

    with open(outpath, "w", encoding="utf-8") as html_file:
        html_file.write(html)

    # Also write to repo root as index.html
    root_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "index.html")
    with open(root_path, "w", encoding="utf-8") as root_file:
        root_file.write(html)

    print(f"HTML written to {outpath}")
    print(f"HTML written to {root_path}")


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
