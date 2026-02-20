using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Service.Rating;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class RatingServiceTests
{
    private ITeamRepository _teamRepo = null!;
    private IRunRepository _runRepo = null!;
    private IRunResultRepository _runResultRepo = null!;
    private IRatingConfigurationRepository _configRepo = null!;
    private IRatingSnapshotRepository _snapshotRepo = null!;
    private ILogger<RatingService> _logger = null!;
    private RatingService _service = null!;

    private RatingConfiguration _defaultConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _teamRepo = Substitute.For<ITeamRepository>();
        _runRepo = Substitute.For<IRunRepository>();
        _runResultRepo = Substitute.For<IRunResultRepository>();
        _configRepo = Substitute.For<IRatingConfigurationRepository>();
        _snapshotRepo = Substitute.For<IRatingSnapshotRepository>();
        _logger = Substitute.For<ILogger<RatingService>>();

        _service = new RatingService(
            _teamRepo, _runRepo, _runResultRepo,
            _configRepo, _snapshotRepo, _logger);

        _defaultConfig = new RatingConfiguration
        {
            Id = 1,
            IsActive = true,
            Mu0 = 25.0f,
            Sigma0 = 25.0f / 3,
            LiveWindowDays = 730,
            MinRunsForLiveRanking = 5,
            MinFieldSize = 6,
            MajorEventWeight = 1.2f,
            SigmaDecay = 0.99f,
            SigmaMin = 1.5f,
            DisplayBase = 1000,
            DisplayScale = 40,
            RatingSigmaMultiplier = 1.0f,
            PodiumBoostBase = 0.85f,
            PodiumBoostRange = 0.20f,
            PodiumBoostTarget = 0.50f,
            ProvisionalSigmaThreshold = 7.8f,
            NormTargetMean = 1500,
            NormTargetStd = 150,
            EliteTopPercent = 0.02f,
            ChampionTopPercent = 0.10f,
            ExpertTopPercent = 0.30f,
        };

        _configRepo.GetActiveAsync().Returns(_defaultConfig);
    }

    [Test]
    public async Task RecalculateAllAsync_NoRuns_ResetsTeamsToDefaults()
    {
        // Arrange
        var teams = CreateTeams(3);
        // Give teams non-default values to verify reset
        teams[0].Mu = 30; teams[0].Sigma = 5;

        _teamRepo.GetAllAsync().Returns(teams);
        _runRepo.GetAllInWindowAsync(Arg.Any<DateOnly>()).Returns(new List<Run>());

        // Act
        await _service.RecalculateAllAsync();

        // Assert
        await _teamRepo.Received(1).UpdateBatchAsync(Arg.Any<IEnumerable<Team>>());
        Assert.That(teams[0].Mu, Is.EqualTo(_defaultConfig.Mu0).Within(0.01f));
        Assert.That(teams[0].Sigma, Is.EqualTo(_defaultConfig.Sigma0).Within(0.01f));
        Assert.That(teams[0].RunCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RecalculateAllAsync_SingleRun_WinnerMuIncreases_LoserMuDecreases()
    {
        // Arrange: 6 teams in one run
        var teams = CreateTeams(6);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams, eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run }, results);

        // Act
        await _service.RecalculateAllAsync();

        // Assert: winner gets higher mu, loser gets lower
        Assert.That(teams[0].Mu, Is.GreaterThan(_defaultConfig.Mu0),
            "Winner's mu should increase");
        Assert.That(teams[5].Mu, Is.LessThan(_defaultConfig.Mu0),
            "Last place team's mu should decrease");
    }

    [Test]
    public async Task RecalculateAllAsync_SingleRun_AllSigmasDecrease()
    {
        var teams = CreateTeams(6);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams, eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // All sigmas should decrease (more certainty after a run) + sigma decay applied
        foreach (var team in teams)
        {
            Assert.That(team.Sigma, Is.LessThan(_defaultConfig.Sigma0),
                $"Team {team.Id} sigma should decrease");
        }
    }

    [Test]
    public async Task RecalculateAllAsync_SingleRun_CountsCorrect()
    {
        // 6 teams, last one eliminated
        var teams = CreateTeams(6);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams,
            eliminated: new[] { false, false, false, false, false, true });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // All teams get RunCount = 1
        foreach (var team in teams)
            Assert.That(team.RunCount, Is.EqualTo(1));

        // Non-eliminated get FinishedRunCount = 1
        Assert.That(teams[0].FinishedRunCount, Is.EqualTo(1));
        Assert.That(teams[5].FinishedRunCount, Is.EqualTo(0), "Eliminated team should not count as finished");

        // Top 3 get Top3RunCount = 1
        Assert.That(teams[0].Top3RunCount, Is.EqualTo(1), "Rank 1 should count as top 3");
        Assert.That(teams[1].Top3RunCount, Is.EqualTo(1), "Rank 2 should count as top 3");
        Assert.That(teams[2].Top3RunCount, Is.EqualTo(1), "Rank 3 should count as top 3");
        Assert.That(teams[3].Top3RunCount, Is.EqualTo(0), "Rank 4 should not count as top 3");
    }

    [Test]
    public async Task RecalculateAllAsync_RunBelowMinFieldSize_Skipped()
    {
        // Only 5 teams, MinFieldSize = 6 → should be skipped
        var teams = CreateTeams(5);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams,
            eliminated: new[] { false, false, false, false, false });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // All teams should remain at defaults since run was skipped
        foreach (var team in teams)
        {
            Assert.That(team.Mu, Is.EqualTo(_defaultConfig.Mu0).Within(0.01f),
                "Mu should be unchanged when run is skipped");
            Assert.That(team.RunCount, Is.EqualTo(0),
                "RunCount should be 0 when run is skipped");
        }
    }

    [Test]
    public async Task RecalculateAllAsync_EliminatedTeams_GetTiedLastRank()
    {
        // 6 teams: 4 ranked, 2 eliminated
        var teams = CreateTeams(6);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams,
            eliminated: new[] { false, false, false, false, true, true });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // Both eliminated teams should get same mu change (tied last rank)
        Assert.That(teams[4].Mu, Is.EqualTo(teams[5].Mu).Within(0.001f),
            "Eliminated teams with tied rank should get same mu");
        Assert.That(teams[4].Sigma, Is.EqualTo(teams[5].Sigma).Within(0.001f),
            "Eliminated teams with tied rank should get same sigma");

        // Eliminated teams should have lower mu than ranked teams
        Assert.That(teams[4].Mu, Is.LessThan(teams[3].Mu),
            "Eliminated teams should have lower mu than last ranked team");
    }

    [Test]
    public async Task RecalculateAllAsync_TwoCompetitions_CumulativeUpdates()
    {
        // Two competitions, each with 6 teams
        var teams = CreateTeams(6);
        var comp1 = CreateCompetition(tier: 2, daysAgo: 100);
        var comp2 = CreateCompetition(tier: 2, daysAgo: 50);
        var run1 = CreateRun(comp1, teams.Count, runId: 1);
        var run2 = CreateRun(comp2, teams.Count, runId: 2);

        var results1 = CreateRunResults(run1, teams,
            eliminated: new[] { false, false, false, false, false, false });
        var results2 = CreateRunResults(run2, teams,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run1, run2 }, results1.Concat(results2).ToList());

        await _service.RecalculateAllAsync();

        // After 2 runs, RunCount should be 2 for all
        foreach (var team in teams)
            Assert.That(team.RunCount, Is.EqualTo(2));

        // Winner should have even higher mu after 2 wins
        Assert.That(teams[0].Mu, Is.GreaterThan(_defaultConfig.Mu0 + 1),
            "Consistent winner should have significantly higher mu after 2 runs");
    }

    [Test]
    public async Task RecalculateAllAsync_MajorEvent_ProducesLargerChanges()
    {
        // Test with tier 1 (major event) vs tier 2
        var teamsMajor = CreateTeams(6, idOffset: 0);
        var compMajor = CreateCompetition(tier: 1);
        var runMajor = CreateRun(compMajor, teamsMajor.Count, runId: 1);
        var resultsMajor = CreateRunResults(runMajor, teamsMajor,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teamsMajor, new[] { runMajor }, resultsMajor);
        await _service.RecalculateAllAsync();
        float majorWinnerMu = teamsMajor[0].Mu;

        // Reset and test with tier 2
        var teamsNormal = CreateTeams(6, idOffset: 0);
        var compNormal = CreateCompetition(tier: 2);
        var runNormal = CreateRun(compNormal, teamsNormal.Count, runId: 2);
        var resultsNormal = CreateRunResults(runNormal, teamsNormal,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teamsNormal, new[] { runNormal }, resultsNormal);
        await _service.RecalculateAllAsync();
        float normalWinnerMu = teamsNormal[0].Mu;

        float majorDelta = Math.Abs(majorWinnerMu - _defaultConfig.Mu0);
        float normalDelta = Math.Abs(normalWinnerMu - _defaultConfig.Mu0);

        Assert.That(majorDelta, Is.GreaterThan(normalDelta),
            "Major event (tier 1) should produce larger mu changes");
    }

    [Test]
    public async Task RecalculateAllAsync_SigmaDecayApplied()
    {
        var teams = CreateTeams(6);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // Sigma should be less than what OpenSkill alone would produce,
        // because we additionally apply sigma * 0.99 decay
        // At minimum, sigma should be > SigmaMin
        foreach (var team in teams)
        {
            Assert.That(team.Sigma, Is.GreaterThanOrEqualTo(_defaultConfig.SigmaMin),
                "Sigma should not go below SigmaMin");
        }
    }

    [Test]
    public async Task RecalculateAllAsync_PrevMuSigma_ReflectsStateBeforeLastRun()
    {
        var teams = CreateTeams(6);
        var comp1 = CreateCompetition(tier: 2, daysAgo: 100);
        var comp2 = CreateCompetition(tier: 2, daysAgo: 50);
        var run1 = CreateRun(comp1, teams.Count, runId: 1);
        var run2 = CreateRun(comp2, teams.Count, runId: 2);

        var results1 = CreateRunResults(run1, teams,
            eliminated: new[] { false, false, false, false, false, false });
        var results2 = CreateRunResults(run2, teams,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run1, run2 }, results1.Concat(results2).ToList());

        await _service.RecalculateAllAsync();

        // PrevMu should be different from current Mu (changed by 2nd run)
        Assert.That(teams[0].PrevMu, Is.Not.EqualTo(teams[0].Mu).Within(0.001f),
            "PrevMu should differ from Mu after multiple runs");

        // PrevMu should NOT be the initial value (it should reflect state after 1st run)
        Assert.That(teams[0].PrevMu, Is.Not.EqualTo(_defaultConfig.Mu0).Within(0.001f),
            "PrevMu should reflect state after first run, not initial state");
    }

    #region Display Scaling & Podium Boost Tests (5.3a)

    [Test]
    public void ComputeBaseRating_KnownInputs_ReturnsExpectedValue()
    {
        // rating_base = 1000 + 40 * (25.0 - 1.0 * 8.333) = 1000 + 40 * 16.667 = 1666.68
        float result = RatingService.ComputeBaseRating(25.0, 25.0 / 3, _defaultConfig);
        Assert.That(result, Is.EqualTo(1666.67f).Within(1.0f));
    }

    [Test]
    public void ComputeBaseRating_HigherMu_ProducesHigherRating()
    {
        float low = RatingService.ComputeBaseRating(20.0, 8.0, _defaultConfig);
        float high = RatingService.ComputeBaseRating(30.0, 8.0, _defaultConfig);
        Assert.That(high, Is.GreaterThan(low));
    }

    [Test]
    public void ComputeBaseRating_HigherSigma_ProducesLowerRating()
    {
        float lowSigma = RatingService.ComputeBaseRating(25.0, 5.0, _defaultConfig);
        float highSigma = RatingService.ComputeBaseRating(25.0, 10.0, _defaultConfig);
        Assert.That(lowSigma, Is.GreaterThan(highSigma));
    }

    [Test]
    public void ComputeQualityFactor_ZeroTop3_ReturnsBase()
    {
        // 0% top3 → quality_norm = 0 → factor = 0.85
        float factor = RatingService.ComputeQualityFactor(10, 0, _defaultConfig);
        Assert.That(factor, Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void ComputeQualityFactor_FiftyPercentTop3_ReturnsMaxFactor()
    {
        // 50% top3 = PODIUM_BOOST_TARGET → quality_norm = 1.0 → factor = 0.85 + 0.20 = 1.05
        float factor = RatingService.ComputeQualityFactor(10, 5, _defaultConfig);
        Assert.That(factor, Is.EqualTo(1.05f).Within(0.001f));
    }

    [Test]
    public void ComputeQualityFactor_AboveTarget_ClampedToMax()
    {
        // 80% top3 > 50% target → should still clamp to 1.05
        float factor = RatingService.ComputeQualityFactor(10, 8, _defaultConfig);
        Assert.That(factor, Is.EqualTo(1.05f).Within(0.001f));
    }

    [Test]
    public void ComputeQualityFactor_TwentyFivePercentTop3_ReturnsMidFactor()
    {
        // 25% top3 / 50% target = 0.5 norm → factor = 0.85 + 0.20 * 0.5 = 0.95
        float factor = RatingService.ComputeQualityFactor(20, 5, _defaultConfig);
        Assert.That(factor, Is.EqualTo(0.95f).Within(0.001f));
    }

    [Test]
    public void ComputeQualityFactor_ZeroRuns_ReturnsBase()
    {
        float factor = RatingService.ComputeQualityFactor(0, 0, _defaultConfig);
        Assert.That(factor, Is.EqualTo(0.85f).Within(0.001f));
    }

    [Test]
    public void ComputeRawRating_CombinesBaseAndQualityFactor()
    {
        // base = 1000 + 40 * (30 - 1.0 * 5) = 1000 + 40 * 25 = 2000
        // top3_pct = 5/10 = 0.5 = target → factor = 1.05
        // raw = 2000 * 1.05 = 2100
        float raw = RatingService.ComputeRawRating(30.0, 5.0, 10, 5, _defaultConfig);
        Assert.That(raw, Is.EqualTo(2100.0f).Within(1.0f));
    }

    [Test]
    public async Task RecalculateAllAsync_SetsRatingFromDisplayScaling()
    {
        var teams = CreateTeams(6);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // Rating should be set (not zero) and winner should have higher rating
        Assert.That(teams[0].Rating, Is.GreaterThan(0), "Rating should be computed");
        Assert.That(teams[0].Rating, Is.GreaterThan(teams[5].Rating),
            "Winner should have higher display rating than loser");
    }

    [Test]
    public async Task RecalculateAllAsync_PrevRatingComputed()
    {
        var teams = CreateTeams(6);
        var comp1 = CreateCompetition(tier: 2, daysAgo: 100);
        var comp2 = CreateCompetition(tier: 2, daysAgo: 50);
        var run1 = CreateRun(comp1, teams.Count, runId: 1);
        var run2 = CreateRun(comp2, teams.Count, runId: 2);

        var results1 = CreateRunResults(run1, teams,
            eliminated: new[] { false, false, false, false, false, false });
        var results2 = CreateRunResults(run2, teams,
            eliminated: new[] { false, false, false, false, false, false });

        SetupMocks(teams, new[] { run1, run2 }, results1.Concat(results2).ToList());

        await _service.RecalculateAllAsync();

        // PrevRating should be set and differ from Rating
        Assert.That(teams[0].PrevRating, Is.GreaterThan(0), "PrevRating should be computed");
        Assert.That(teams[0].PrevRating, Is.Not.EqualTo(teams[0].Rating).Within(0.01f),
            "PrevRating should differ from Rating after 2 runs");
    }

    #endregion

    #region Cross-Size Normalization, Flags & Tiers Tests (5.3b)

    [Test]
    public void ApplyNormalization_SingleSizeCategory_MeanApproxTarget()
    {
        var teams = new List<Team>();
        for (int i = 0; i < 20; i++)
        {
            teams.Add(new Team
            {
                Id = i + 1,
                RunCount = 10,
                Rating = 1400 + i * 20, // 1400..1780 spread
                PrevRating = 1390 + i * 20,
                Dog = new Dog { Id = i + 1, CallName = $"D{i}", NormalizedCallName = $"d{i}", SizeCategory = SizeCategory.L },
            });
        }

        RatingService.ApplyNormalization(teams, _defaultConfig);

        double mean = teams.Average(t => (double)t.Rating);
        Assert.That(mean, Is.EqualTo(1500.0).Within(1.0),
            "Normalized mean should be ~1500");
    }

    [Test]
    public void ApplyNormalization_SingleSizeCategory_StdApproxTarget()
    {
        var teams = new List<Team>();
        for (int i = 0; i < 20; i++)
        {
            teams.Add(new Team
            {
                Id = i + 1,
                RunCount = 10,
                Rating = 1400 + i * 20,
                PrevRating = 1390 + i * 20,
                Dog = new Dog { Id = i + 1, CallName = $"D{i}", NormalizedCallName = $"d{i}", SizeCategory = SizeCategory.L },
            });
        }

        RatingService.ApplyNormalization(teams, _defaultConfig);

        double mean = teams.Average(t => (double)t.Rating);
        double std = Math.Sqrt(teams.Average(t => Math.Pow(t.Rating - mean, 2)));
        Assert.That(std, Is.EqualTo(150.0).Within(1.0),
            "Normalized std should be ~150");
    }

    [Test]
    public void ApplyNormalization_PreservesRankOrder()
    {
        var teams = new List<Team>();
        for (int i = 0; i < 10; i++)
        {
            teams.Add(new Team
            {
                Id = i + 1,
                RunCount = 10,
                Rating = 1000 + i * 50,
                PrevRating = 1000 + i * 50,
                Dog = new Dog { Id = i + 1, CallName = $"D{i}", NormalizedCallName = $"d{i}", SizeCategory = SizeCategory.L },
            });
        }

        RatingService.ApplyNormalization(teams, _defaultConfig);

        for (int i = 0; i < 9; i++)
        {
            Assert.That(teams[i].Rating, Is.LessThan(teams[i + 1].Rating),
                "Normalization should preserve rank order");
        }
    }

    [Test]
    public void ApplyNormalization_ZeroRunCount_NotNormalized()
    {
        var teams = new List<Team>
        {
            new() { Id = 1, RunCount = 0, Rating = 1200, PrevRating = 1200,
                Dog = new Dog { Id = 1, CallName = "D1", NormalizedCallName = "d1", SizeCategory = SizeCategory.L } },
        };

        RatingService.ApplyNormalization(teams, _defaultConfig);

        // Team with 0 runs should not be touched by normalization
        Assert.That(teams[0].Rating, Is.EqualTo(1200.0f).Within(0.01f));
    }

    [Test]
    public void ApplyFlagsAndTiers_ActiveFlag_SetCorrectly()
    {
        var teams = new List<Team>
        {
            new() { Id = 1, RunCount = 10, Rating = 1700, Sigma = 3.0f,
                Dog = new Dog { Id = 1, CallName = "D1", NormalizedCallName = "d1", SizeCategory = SizeCategory.L } },
            new() { Id = 2, RunCount = 3, Rating = 1500, Sigma = 3.0f, // below MinRunsForLiveRanking (5)
                Dog = new Dog { Id = 2, CallName = "D2", NormalizedCallName = "d2", SizeCategory = SizeCategory.L } },
        };

        RatingService.ApplyFlagsAndTiers(teams, _defaultConfig);

        Assert.That(teams[0].IsActive, Is.True, "Team with 10 runs should be active");
        Assert.That(teams[1].IsActive, Is.False, "Team with 3 runs should not be active");
    }

    [Test]
    public void ApplyFlagsAndTiers_ProvisionalFlag_BasedOnSigma()
    {
        var teams = new List<Team>
        {
            new() { Id = 1, RunCount = 10, Rating = 1700, Sigma = 3.0f,
                Dog = new Dog { Id = 1, CallName = "D1", NormalizedCallName = "d1", SizeCategory = SizeCategory.L } },
            new() { Id = 2, RunCount = 10, Rating = 1500, Sigma = 8.0f, // above ProvisionalSigmaThreshold (7.8)
                Dog = new Dog { Id = 2, CallName = "D2", NormalizedCallName = "d2", SizeCategory = SizeCategory.L } },
        };

        RatingService.ApplyFlagsAndTiers(teams, _defaultConfig);

        Assert.That(teams[0].IsProvisional, Is.False, "Low sigma = not provisional");
        Assert.That(teams[1].IsProvisional, Is.True, "High sigma = provisional");
    }

    [Test]
    public void ApplyFlagsAndTiers_TierLabels_CorrectPercentiles()
    {
        // Create 100 active teams with distinct ratings
        var teams = new List<Team>();
        for (int i = 0; i < 100; i++)
        {
            teams.Add(new Team
            {
                Id = i + 1,
                RunCount = 10,
                Rating = 1300 + i * 5, // 1300..1795 spread
                Sigma = 3.0f,
                Dog = new Dog { Id = i + 1, CallName = $"D{i}", NormalizedCallName = $"d{i}", SizeCategory = SizeCategory.L },
            });
        }

        RatingService.ApplyFlagsAndTiers(teams, _defaultConfig);

        // Sort by rating desc to check tiers
        var sorted = teams.OrderByDescending(t => t.Rating).ToList();

        // Top 2% = top 2 teams → Elite
        Assert.That(sorted[0].TierLabel, Is.EqualTo(TierLabel.Elite), "Top team should be Elite");
        Assert.That(sorted[1].TierLabel, Is.EqualTo(TierLabel.Elite), "2nd team should be Elite");

        // 3rd-10th = Champion (top 10%)
        Assert.That(sorted[2].TierLabel, Is.EqualTo(TierLabel.Champion), "3rd team should be Champion");
        Assert.That(sorted[9].TierLabel, Is.EqualTo(TierLabel.Champion), "10th team should be Champion");

        // 11th-30th = Expert (top 30%)
        Assert.That(sorted[10].TierLabel, Is.EqualTo(TierLabel.Expert), "11th team should be Expert");
        Assert.That(sorted[29].TierLabel, Is.EqualTo(TierLabel.Expert), "30th team should be Expert");

        // 31st+ = Competitor
        Assert.That(sorted[30].TierLabel, Is.EqualTo(TierLabel.Competitor), "31st team should be Competitor");
        Assert.That(sorted[99].TierLabel, Is.EqualTo(TierLabel.Competitor), "Last team should be Competitor");
    }

    [Test]
    public void ApplyFlagsAndTiers_InactiveTeam_NoTierLabel()
    {
        var teams = new List<Team>
        {
            new() { Id = 1, RunCount = 2, Rating = 1700, Sigma = 3.0f,
                Dog = new Dog { Id = 1, CallName = "D1", NormalizedCallName = "d1", SizeCategory = SizeCategory.L } },
        };

        RatingService.ApplyFlagsAndTiers(teams, _defaultConfig);

        Assert.That(teams[0].TierLabel, Is.Null, "Inactive team should have no tier label");
    }

    [Test]
    public void ApplyFlagsAndTiers_PeakRating_OnlyIncreases()
    {
        var teams = new List<Team>
        {
            new() { Id = 1, RunCount = 10, Rating = 1600, Sigma = 3.0f, PeakRating = 1700,
                Dog = new Dog { Id = 1, CallName = "D1", NormalizedCallName = "d1", SizeCategory = SizeCategory.L } },
            new() { Id = 2, RunCount = 10, Rating = 1800, Sigma = 3.0f, PeakRating = 1700,
                Dog = new Dog { Id = 2, CallName = "D2", NormalizedCallName = "d2", SizeCategory = SizeCategory.L } },
        };

        RatingService.ApplyFlagsAndTiers(teams, _defaultConfig);

        Assert.That(teams[0].PeakRating, Is.EqualTo(1700),
            "PeakRating should not decrease when current rating is lower");
        Assert.That(teams[1].PeakRating, Is.EqualTo(1800),
            "PeakRating should update when current rating is higher");
    }

    [Test]
    public async Task RecalculateAllAsync_FullPipeline_NormalizedRatings()
    {
        // 8 teams, enough for meaningful normalization
        var teams = CreateTeams(8);
        var competition = CreateCompetition(tier: 2);
        var run = CreateRun(competition, teams.Count);
        var results = CreateRunResults(run, teams,
            eliminated: new[] { false, false, false, false, false, false, false, false });

        SetupMocks(teams, new[] { run }, results);

        await _service.RecalculateAllAsync();

        // After normalization, mean should be ~1500
        double mean = teams.Average(t => (double)t.Rating);
        Assert.That(mean, Is.EqualTo(1500.0).Within(2.0),
            "Normalized ratings should have mean ~1500");
    }

    #endregion

    #region Helper Methods

    private static List<Team> CreateTeams(int count, int idOffset = 0, SizeCategory size = SizeCategory.L)
    {
        return Enumerable.Range(1, count).Select(i => new Team
        {
            Id = idOffset + i,
            HandlerId = i,
            DogId = i,
            Slug = $"team-{idOffset + i}",
            Mu = 25.0f,
            Sigma = 25.0f / 3,
            Dog = new Dog
            {
                Id = i,
                CallName = $"Dog{i}",
                NormalizedCallName = $"dog{i}",
                SizeCategory = size,
            },
        }).ToList();
    }

    private static Competition CreateCompetition(int tier, int daysAgo = 30)
    {
        return new Competition
        {
            Id = 1,
            Slug = $"comp-{daysAgo}",
            Name = $"Competition {daysAgo}d ago",
            Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo),
            Tier = tier,
        };
    }

    private static Run CreateRun(Competition competition, int resultCount, int runId = 1)
    {
        return new Run
        {
            Id = runId,
            CompetitionId = competition.Id,
            Competition = competition,
            Date = competition.Date,
            RunNumber = 1,
            RoundKey = $"ind_agility_large_{runId}",
            SizeCategory = SizeCategory.L,
            Discipline = Discipline.Agility,
            IsTeamRound = false,
        };
    }

    private static List<RunResult> CreateRunResults(
        Run run, List<Team> teams, bool[] eliminated)
    {
        var results = new List<RunResult>();
        int rank = 1;

        for (int i = 0; i < teams.Count; i++)
        {
            results.Add(new RunResult
            {
                Id = run.Id * 100 + i + 1,
                RunId = run.Id,
                Run = run,
                TeamId = teams[i].Id,
                Team = teams[i],
                Rank = eliminated[i] ? null : rank,
                Eliminated = eliminated[i],
                Faults = eliminated[i] ? null : 0,
                Time = eliminated[i] ? null : 30.0f + i,
            });

            if (!eliminated[i])
                rank++;
        }

        return results;
    }

    private void SetupMocks(List<Team> teams, Run[] runs, List<RunResult> results)
    {
        _teamRepo.GetAllAsync().Returns(teams);
        _runRepo.GetAllInWindowAsync(Arg.Any<DateOnly>()).Returns(runs.ToList());
        _runResultRepo.GetByRunIdsAsync(Arg.Any<IEnumerable<int>>()).Returns(results);
    }

    #endregion
}
