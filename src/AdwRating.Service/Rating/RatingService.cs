using AdwRating.Domain.Entities;
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

        // 3. Compute cutoff date and load runs in window
        var runs = await _runRepo.GetAllInWindowAsync(
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-config.LiveWindowDays));

        // Sort runs chronologically (by date, then by id for stable ordering)
        var sortedRuns = runs.OrderBy(r => r.Date).ThenBy(r => r.Id).ToList();

        if (sortedRuns.Count == 0)
        {
            _logger.LogInformation("No runs found in window. Saving reset teams.");
            ApplyStateToTeams(allTeams, teamState, config);
            await _teamRepo.UpdateBatchAsync(allTeams);
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

            processedRuns++;
        }

        _logger.LogInformation("Processed {Processed} runs, skipped {Skipped} (below MinFieldSize or empty)",
            processedRuns, skippedRuns);

        // 6. Apply final state to team entities and persist
        ApplyStateToTeams(allTeams, teamState, config);
        await _teamRepo.UpdateBatchAsync(allTeams);

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
        }
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
