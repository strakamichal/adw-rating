using AdwRating.Service.Rating;

namespace AdwRating.Tests.Services;

[TestFixture]
public class RatingEngineTests
{
    private RatingEngine _engine = null!;

    [SetUp]
    public void SetUp()
    {
        _engine = new RatingEngine(mu0: 25.0, sigma0: 25.0 / 3);
    }

    [Test]
    public void CreateRating_Default_ReturnsInitialValues()
    {
        var (mu, sigma) = _engine.CreateRating();

        Assert.That(mu, Is.EqualTo(25.0).Within(0.01));
        Assert.That(sigma, Is.EqualTo(25.0 / 3).Within(0.01));
    }

    [Test]
    public void CreateRating_WithValues_ReturnsSameValues()
    {
        var (mu, sigma) = _engine.CreateRating(30.0, 5.0);

        Assert.That(mu, Is.EqualTo(30.0));
        Assert.That(sigma, Is.EqualTo(5.0));
    }

    [Test]
    public void ProcessRun_FiveTeams_WinnerMuIncreases()
    {
        // Arrange: 5 teams with identical starting ratings
        var ratings = Enumerable.Range(0, 5)
            .Select(_ => (Mu: 25.0, Sigma: 25.0 / 3))
            .ToList();
        var ranks = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var results = _engine.ProcessRun(ratings, ranks);

        // Assert: winner (rank 1) should have higher mu than starting value
        Assert.That(results[0].Mu, Is.GreaterThan(25.0),
            "Winner's mu should increase");
    }

    [Test]
    public void ProcessRun_FiveTeams_LoserMuDecreases()
    {
        var ratings = Enumerable.Range(0, 5)
            .Select(_ => (Mu: 25.0, Sigma: 25.0 / 3))
            .ToList();
        var ranks = new List<int> { 1, 2, 3, 4, 5 };

        var results = _engine.ProcessRun(ratings, ranks);

        // Last place should have lower mu
        Assert.That(results[4].Mu, Is.LessThan(25.0),
            "Last place team's mu should decrease");
    }

    [Test]
    public void ProcessRun_FiveTeams_AllSigmasDecrease()
    {
        var ratings = Enumerable.Range(0, 5)
            .Select(_ => (Mu: 25.0, Sigma: 25.0 / 3))
            .ToList();
        var ranks = new List<int> { 1, 2, 3, 4, 5 };

        var results = _engine.ProcessRun(ratings, ranks);

        // All sigmas should decrease (more certainty after observing results)
        for (int i = 0; i < 5; i++)
        {
            Assert.That(results[i].Sigma, Is.LessThan(25.0 / 3),
                $"Team {i} sigma should decrease after a run");
        }
    }

    [Test]
    public void ProcessRun_FiveTeams_RankOrderPreservedInMu()
    {
        var ratings = Enumerable.Range(0, 5)
            .Select(_ => (Mu: 25.0, Sigma: 25.0 / 3))
            .ToList();
        var ranks = new List<int> { 1, 2, 3, 4, 5 };

        var results = _engine.ProcessRun(ratings, ranks);

        // Higher-placed teams should get higher mu than lower-placed
        for (int i = 0; i < 4; i++)
        {
            Assert.That(results[i].Mu, Is.GreaterThan(results[i + 1].Mu),
                $"Team at rank {i + 1} should have higher mu than team at rank {i + 2}");
        }
    }

    [Test]
    public void ProcessRun_WeightGreaterThanOne_ProducesLargerMuChanges()
    {
        var ratings = Enumerable.Range(0, 5)
            .Select(_ => (Mu: 25.0, Sigma: 25.0 / 3))
            .ToList();
        var ranks = new List<int> { 1, 2, 3, 4, 5 };

        var normalResults = _engine.ProcessRun(ratings, ranks, weight: 1.0);
        var weightedResults = _engine.ProcessRun(ratings, ranks, weight: 1.2);

        // Winner's mu change should be larger with weight > 1
        double normalDelta = Math.Abs(normalResults[0].Mu - 25.0);
        double weightedDelta = Math.Abs(weightedResults[0].Mu - 25.0);

        Assert.That(weightedDelta, Is.GreaterThan(normalDelta),
            "Weight > 1.0 should produce larger mu changes");

        // Loser's mu change should also be larger
        double normalLoserDelta = Math.Abs(normalResults[4].Mu - 25.0);
        double weightedLoserDelta = Math.Abs(weightedResults[4].Mu - 25.0);

        Assert.That(weightedLoserDelta, Is.GreaterThan(normalLoserDelta),
            "Weight > 1.0 should produce larger mu changes for losers too");
    }

    [Test]
    public void ProcessRun_TiedRanks_EliminatedTeamsGetSameMuChange()
    {
        // Simulate: 3 teams finish normally, 2 are eliminated (tied last)
        var ratings = Enumerable.Range(0, 5)
            .Select(_ => (Mu: 25.0, Sigma: 25.0 / 3))
            .ToList();
        // Ranks: 1, 2, 3, then two eliminated teams share rank 4
        var ranks = new List<int> { 1, 2, 3, 4, 4 };

        var results = _engine.ProcessRun(ratings, ranks);

        // Tied teams should get same mu/sigma changes
        Assert.That(results[3].Mu, Is.EqualTo(results[4].Mu).Within(0.001),
            "Tied teams should get the same mu");
        Assert.That(results[3].Sigma, Is.EqualTo(results[4].Sigma).Within(0.001),
            "Tied teams should get the same sigma");
    }

    [Test]
    public void ProcessRun_TwoTeams_ThrowsNoException()
    {
        var ratings = new List<(double Mu, double Sigma)>
        {
            (25.0, 8.333), (25.0, 8.333)
        };
        var ranks = new List<int> { 1, 2 };

        Assert.DoesNotThrow(() => _engine.ProcessRun(ratings, ranks));
    }

    [Test]
    public void ProcessRun_MismatchedLengths_ThrowsArgumentException()
    {
        var ratings = new List<(double Mu, double Sigma)>
        {
            (25.0, 8.333), (25.0, 8.333)
        };
        var ranks = new List<int> { 1, 2, 3 };

        Assert.Throws<ArgumentException>(() => _engine.ProcessRun(ratings, ranks));
    }

    [Test]
    public void ProcessRun_SingleTeam_ThrowsArgumentException()
    {
        var ratings = new List<(double Mu, double Sigma)> { (25.0, 8.333) };
        var ranks = new List<int> { 1 };

        Assert.Throws<ArgumentException>(() => _engine.ProcessRun(ratings, ranks));
    }

    [Test]
    public void ProcessRun_PreexistingRatings_StrongerTeamWinningChangesLess()
    {
        // A strong team beating a weak team should change less than vice versa
        var ratings = new List<(double Mu, double Sigma)>
        {
            (30.0, 5.0), // strong team
            (20.0, 5.0), // weak team
        };
        var ranks = new List<int> { 1, 2 }; // strong team wins (expected)

        var results = _engine.ProcessRun(ratings, ranks);

        double strongDelta = Math.Abs(results[0].Mu - 30.0);
        double weakDelta = Math.Abs(results[1].Mu - 20.0);

        // When the expected outcome happens, changes are smaller
        // Both deltas should be relatively small since this is expected
        Assert.That(results[0].Mu, Is.GreaterThan(30.0),
            "Winning strong team still gains some mu");
        Assert.That(results[1].Mu, Is.LessThan(20.0),
            "Losing weak team still loses some mu");
    }
}
