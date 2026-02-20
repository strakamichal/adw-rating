using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AdwRating.Service.Rating;

public class RatingService : IRatingService
{
    private readonly ITeamRepository _teamRepo;
    private readonly IRunRepository _runRepo;
    private readonly IRunResultRepository _runResultRepo;
    private readonly IRatingConfigurationRepository _configRepo;
    private readonly IRatingSnapshotRepository _snapshotRepo;
    private readonly ILogger<RatingService> _logger;

    public RatingService(
        ITeamRepository teamRepo,
        IRunRepository runRepo,
        IRunResultRepository runResultRepo,
        IRatingConfigurationRepository configRepo,
        IRatingSnapshotRepository snapshotRepo,
        ILogger<RatingService> logger)
    {
        _teamRepo = teamRepo;
        _runRepo = runRepo;
        _runResultRepo = runResultRepo;
        _configRepo = configRepo;
        _snapshotRepo = snapshotRepo;
        _logger = logger;
    }

    public async Task RecalculateAllAsync()
    {
        // 1. Load configuration
        var config = await _configRepo.GetActiveAsync();
        var engine = new RatingEngine(config.Mu0, config.Sigma0);

        _logger.LogInformation("Starting rating recalculation with config: Mu0={Mu0}, Sigma0={Sigma0}, Window={Window}d",
            config.Mu0, config.Sigma0, config.LiveWindowDays);

        // 2. Load all teams and reset to initial state
        var allTeams = await _teamRepo.GetAllAsync();
        var teamState = new Dictionary<int, TeamRatingState>();

        foreach (var team in allTeams)
        {
            teamState[team.Id] = new TeamRatingState
            {
                Mu = config.Mu0,
                Sigma = config.Sigma0,
                PrevMu = config.Mu0,
                PrevSigma = config.Sigma0,
                RunCount = 0,
                FinishedRunCount = 0,
                Top3RunCount = 0,
            };
        }

        // 3. Compute cutoff date from latest competition date (not current time)
        var latestDate = await _runRepo.GetLatestDateAsync();
        if (latestDate is null)
        {
            _logger.LogInformation("No runs found in database. Saving reset teams.");
            ApplyStateToTeams(allTeams, teamState, config);
            ApplyNormalization(allTeams, config, null);
            ApplyFlagsAndTiers(allTeams, config);
            await _teamRepo.UpdateBatchAsync(allTeams);
            await _snapshotRepo.ReplaceAllAsync(new List<RatingSnapshot>());
            return;
        }

        var cutoffDate = latestDate.Value.AddDays(-config.LiveWindowDays);
        _logger.LogInformation("Cutoff date: {CutoffDate} (latest run: {LatestDate}, window: {Window}d)",
            cutoffDate, latestDate.Value, config.LiveWindowDays);

        var runs = await _runRepo.GetAllInWindowAsync(cutoffDate);

        // Sort runs chronologically (by date, then by id for stable ordering)
        var sortedRuns = runs.OrderBy(r => r.Date).ThenBy(r => r.Id).ToList();

        if (sortedRuns.Count == 0)
        {
            _logger.LogInformation("No runs found in window. Saving reset teams.");
            ApplyStateToTeams(allTeams, teamState, config);
            ApplyNormalization(allTeams, config, null);
            ApplyFlagsAndTiers(allTeams, config);
            await _teamRepo.UpdateBatchAsync(allTeams);
            await _snapshotRepo.ReplaceAllAsync(new List<RatingSnapshot>());
            return;
        }

        // 4. Batch-load all run results
        var runIds = sortedRuns.Select(r => r.Id).ToList();
        var allResults = await _runResultRepo.GetByRunIdsAsync(runIds);
        var resultsByRun = allResults.GroupBy(r => r.RunId)
            .ToDictionary(g => g.Key, g => g.ToList());

        _logger.LogInformation("Processing {RunCount} runs with {ResultCount} total results",
            sortedRuns.Count, allResults.Count);

        int processedRuns = 0;
        int skippedRuns = 0;
        var snapshots = new List<RatingSnapshot>();

        // 5. Process each run
        foreach (var run in sortedRuns)
        {
            if (!resultsByRun.TryGetValue(run.Id, out var runResults) || runResults.Count == 0)
            {
                skippedRuns++;
                continue;
            }

            // Deduplicate: keep first result per team within the run
            var dedupedResults = runResults
                .GroupBy(r => r.TeamId)
                .Select(g => g.First())
                .ToList();

            // Skip runs below minimum field size
            if (dedupedResults.Count < config.MinFieldSize)
            {
                skippedRuns++;
                continue;
            }

            // Determine weight from competition tier
            double weight = run.Competition?.Tier == 1 ? config.MajorEventWeight : 1.0;

            // Build rank list
            var (teamIds, ratings, ranks) = BuildRankList(dedupedResults, teamState, config);

            if (teamIds.Count < 2)
            {
                skippedRuns++;
                continue;
            }

            // Save previous state (before this update) for all participating teams
            foreach (var teamId in teamIds)
            {
                var state = teamState[teamId];
                state.PrevMu = state.Mu;
                state.PrevSigma = state.Sigma;
            }

            // Process through rating engine
            var updatedRatings = engine.ProcessRun(ratings, ranks, weight);

            // Apply updates + sigma decay + count tracking
            for (int i = 0; i < teamIds.Count; i++)
            {
                var teamId = teamIds[i];
                var state = teamState[teamId];
                var result = dedupedResults.First(r => r.TeamId == teamId);

                state.Mu = updatedRatings[i].Mu;
                state.Sigma = updatedRatings[i].Sigma;

                // Sigma decay: sigma = max(SIGMA_MIN, sigma * SIGMA_DECAY)
                state.Sigma = Math.Max(config.SigmaMin, state.Sigma * config.SigmaDecay);

                // Track counts
                state.RunCount++;
                if (!result.Eliminated)
                {
                    state.FinishedRunCount++;
                    if (result.Rank.HasValue && result.Rank.Value <= 3)
                        state.Top3RunCount++;
                }
            }

            // Collect snapshots for each participating team
            foreach (var result in dedupedResults)
            {
                if (!teamState.ContainsKey(result.TeamId))
                    continue;

                var state = teamState[result.TeamId];
                snapshots.Add(new RatingSnapshot
                {
                    TeamId = result.TeamId,
                    RunResultId = result.Id,
                    CompetitionId = run.CompetitionId,
                    Date = run.Date,
                    Mu = (float)state.Mu,
                    Sigma = (float)state.Sigma,
                    // Rating will be computed after normalization
                });
            }

            processedRuns++;
        }

        _logger.LogInformation("Processed {Processed} runs, skipped {Skipped} (below MinFieldSize or empty)",
            processedRuns, skippedRuns);

        // 6. Apply raw ratings (display scaling + podium boost)
        ApplyStateToTeams(allTeams, teamState, config);

        // 7. Compute raw ratings for snapshots (using each team's final counts for podium boost)
        var teamLookup = allTeams.ToDictionary(t => t.Id);
        foreach (var snapshot in snapshots)
        {
            if (teamLookup.TryGetValue(snapshot.TeamId, out var team))
            {
                snapshot.Rating = ComputeRawRating(
                    snapshot.Mu, snapshot.Sigma,
                    team.RunCount, team.Top3RunCount, config);
            }
        }

        // 8. Cross-size normalization (teams + snapshots)
        ApplyNormalization(allTeams, config, snapshots);

        // 9. Flags, tiers, PeakRating
        ApplyFlagsAndTiers(allTeams, config);

        // 10. Persist teams and snapshots
        await _teamRepo.UpdateBatchAsync(allTeams);
        await _snapshotRepo.ReplaceAllAsync(snapshots);

        _logger.LogInformation("Rating recalculation complete. Updated {TeamCount} teams.", allTeams.Count);
    }

    private static (List<int> TeamIds, List<(double Mu, double Sigma)> Ratings, List<int> Ranks) BuildRankList(
        List<RunResult> results,
        Dictionary<int, TeamRatingState> teamState,
        RatingConfiguration config)
    {
        var teamIds = new List<int>();
        var ratings = new List<(double Mu, double Sigma)>();
        var ranks = new List<int>();

        // Separate non-eliminated (with valid rank) and eliminated
        var ranked = results
            .Where(r => !r.Eliminated && r.Rank.HasValue)
            .OrderBy(r => r.Rank!.Value)
            .ToList();

        var eliminated = results
            .Where(r => r.Eliminated)
            .ToList();

        // Tied last rank for eliminated teams
        int lastRank = ranked.Count + 1;

        foreach (var result in ranked)
        {
            if (!teamState.ContainsKey(result.TeamId))
                continue;

            var state = teamState[result.TeamId];
            teamIds.Add(result.TeamId);
            ratings.Add((state.Mu, state.Sigma));
            ranks.Add(result.Rank!.Value);
        }

        foreach (var result in eliminated)
        {
            if (!teamState.ContainsKey(result.TeamId))
                continue;

            var state = teamState[result.TeamId];
            teamIds.Add(result.TeamId);
            ratings.Add((state.Mu, state.Sigma));
            ranks.Add(lastRank);
        }

        return (teamIds, ratings, ranks);
    }

    /// <summary>
    /// Computes the base display rating from mu/sigma.
    /// rating_base = DISPLAY_BASE + DISPLAY_SCALE * (mu - RATING_SIGMA_MULTIPLIER * sigma)
    /// </summary>
    internal static float ComputeBaseRating(double mu, double sigma, RatingConfiguration config)
    {
        return (float)(config.DisplayBase + config.DisplayScale * (mu - config.RatingSigmaMultiplier * sigma));
    }

    /// <summary>
    /// Computes the podium boost quality factor from top-3 placement percentage.
    /// quality_norm = clamp(top3_pct / PODIUM_BOOST_TARGET, 0, 1)
    /// quality_factor = PODIUM_BOOST_BASE + PODIUM_BOOST_RANGE * quality_norm
    /// </summary>
    internal static float ComputeQualityFactor(int runCount, int top3RunCount, RatingConfiguration config)
    {
        if (runCount == 0)
            return config.PodiumBoostBase; // No runs → minimum factor

        double top3Pct = (double)top3RunCount / runCount;
        double qualityNorm = Math.Clamp(top3Pct / config.PodiumBoostTarget, 0.0, 1.0);
        return (float)(config.PodiumBoostBase + config.PodiumBoostRange * qualityNorm);
    }

    /// <summary>
    /// Computes the raw display rating (before cross-size normalization).
    /// rating_raw = rating_base * quality_factor
    /// </summary>
    internal static float ComputeRawRating(double mu, double sigma, int runCount, int top3RunCount, RatingConfiguration config)
    {
        float baseRating = ComputeBaseRating(mu, sigma, config);
        float qualityFactor = ComputeQualityFactor(runCount, top3RunCount, config);
        return baseRating * qualityFactor;
    }

    private static void ApplyStateToTeams(
        IReadOnlyList<Team> teams,
        Dictionary<int, TeamRatingState> teamState,
        RatingConfiguration config)
    {
        foreach (var team in teams)
        {
            if (!teamState.TryGetValue(team.Id, out var state))
                continue;

            team.Mu = (float)state.Mu;
            team.Sigma = (float)state.Sigma;
            team.PrevMu = (float)state.PrevMu;
            team.PrevSigma = (float)state.PrevSigma;
            team.RunCount = state.RunCount;
            team.FinishedRunCount = state.FinishedRunCount;
            team.Top3RunCount = state.Top3RunCount;

            // Display scaling + podium boost → raw rating (before normalization)
            team.Rating = ComputeRawRating(state.Mu, state.Sigma, state.RunCount, state.Top3RunCount, config);
            team.PrevRating = ComputeRawRating(state.PrevMu, state.PrevSigma, state.RunCount, state.Top3RunCount, config);
        }
    }

    /// <summary>
    /// Applies z-score normalization per size category to produce final display ratings.
    /// Rating = NORM_TARGET_MEAN + NORM_TARGET_STD * (rating_raw - size_mean) / size_std
    /// Also normalizes PrevRating for consistent trend display.
    /// </summary>
    internal static void ApplyNormalization(
        IReadOnlyList<Team> teams,
        RatingConfiguration config,
        IList<RatingSnapshot>? snapshots = null)
    {
        // Build snapshot lookup by team for applying same normalization params
        var snapshotsByTeam = snapshots?.GroupBy(s => s.TeamId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group all teams with runs by size category
        var sizeGroups = teams
            .Where(t => t.RunCount > 0)
            .GroupBy(t => GetEffectiveSizeCategory(t));

        // Compute normalization params per size category, then apply
        foreach (var group in sizeGroups)
        {
            var groupTeams = group.ToList();

            // Compute mean/std only from qualified teams (matching Python behavior)
            var qualified = groupTeams
                .Where(t => t.RunCount >= config.MinRunsForLiveRanking)
                .ToList();

            if (qualified.Count < 2)
            {
                // With 0-1 qualified teams, can't compute std; just set to target mean
                foreach (var team in groupTeams)
                {
                    team.Rating = config.NormTargetMean;
                    team.PrevRating = config.NormTargetMean;
                    NormalizeTeamSnapshots(snapshotsByTeam, team.Id, config.NormTargetMean, 0, 1);
                }
                continue;
            }

            // Compute mean and std from qualified teams only
            double mean = qualified.Average(t => (double)t.Rating);
            double variance = qualified.Average(t => Math.Pow(t.Rating - mean, 2));
            double std = Math.Sqrt(variance);

            if (std < 1e-6)
            {
                // All same rating → just set to target mean
                foreach (var team in groupTeams)
                {
                    team.Rating = config.NormTargetMean;
                    team.PrevRating = config.NormTargetMean;
                    NormalizeTeamSnapshots(snapshotsByTeam, team.Id, config.NormTargetMean, 0, 1);
                }
                continue;
            }

            // Apply normalization to ALL teams in the group (using qualified mean/std)
            foreach (var team in groupTeams)
            {
                team.Rating = (float)(config.NormTargetMean + config.NormTargetStd * (team.Rating - mean) / std);
                team.PrevRating = (float)(config.NormTargetMean + config.NormTargetStd * (team.PrevRating - mean) / std);

                // Apply same normalization params to this team's snapshots
                if (snapshotsByTeam != null && snapshotsByTeam.TryGetValue(team.Id, out var teamSnapshots))
                {
                    foreach (var snapshot in teamSnapshots)
                    {
                        snapshot.Rating = (float)(config.NormTargetMean + config.NormTargetStd * (snapshot.Rating - mean) / std);
                    }
                }
            }
        }
    }

    private static void NormalizeTeamSnapshots(
        Dictionary<int, List<RatingSnapshot>>? snapshotsByTeam,
        int teamId, float targetMean, double mean, double std)
    {
        if (snapshotsByTeam == null || !snapshotsByTeam.TryGetValue(teamId, out var teamSnapshots))
            return;

        foreach (var snapshot in teamSnapshots)
            snapshot.Rating = targetMean;
    }

    /// <summary>
    /// Sets IsActive, IsProvisional flags, tier labels, and updates PeakRating.
    /// </summary>
    internal static void ApplyFlagsAndTiers(IReadOnlyList<Team> teams, RatingConfiguration config)
    {
        foreach (var team in teams)
        {
            // IsActive: has minimum runs for ranking
            team.IsActive = team.RunCount >= config.MinRunsForLiveRanking;

            // IsProvisional: high sigma (uncertain rating)
            team.IsProvisional = team.Sigma >= config.ProvisionalSigmaThreshold;

            // PeakRating: only increases (preserved across recalculations)
            if (team.IsActive && team.Rating > team.PeakRating)
                team.PeakRating = team.Rating;

            // Reset tier for inactive teams
            if (!team.IsActive)
            {
                team.TierLabel = null;
                continue;
            }
        }

        // Assign tier labels per size category (only among active teams)
        var activeBySizeCategory = teams
            .Where(t => t.IsActive)
            .GroupBy(t => GetEffectiveSizeCategory(t));

        foreach (var group in activeBySizeCategory)
        {
            var sorted = group.OrderByDescending(t => t.Rating).ToList();
            int count = sorted.Count;

            for (int i = 0; i < count; i++)
            {
                float percentile = (float)(i + 1) / count; // top percentile (1/N for best)
                sorted[i].TierLabel = percentile <= config.EliteTopPercent ? TierLabel.Elite
                    : percentile <= config.ChampionTopPercent ? TierLabel.Champion
                    : percentile <= config.ExpertTopPercent ? TierLabel.Expert
                    : TierLabel.Competitor;
            }
        }
    }

    /// <summary>
    /// Gets the effective FCI size category for a team's dog,
    /// using the override if set.
    /// </summary>
    internal static SizeCategory GetEffectiveSizeCategory(Team team)
    {
        return team.Dog?.SizeCategoryOverride ?? team.Dog?.SizeCategory ?? SizeCategory.L;
    }

    /// <summary>
    /// Internal mutable state for tracking a team's rating during recalculation.
    /// </summary>
    private class TeamRatingState
    {
        public double Mu { get; set; }
        public double Sigma { get; set; }
        public double PrevMu { get; set; }
        public double PrevSigma { get; set; }
        public int RunCount { get; set; }
        public int FinishedRunCount { get; set; }
        public int Top3RunCount { get; set; }
    }
}
