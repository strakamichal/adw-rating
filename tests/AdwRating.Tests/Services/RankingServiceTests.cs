using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using AdwRating.Service;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class RankingServiceTests
{
    private ITeamRepository _teamRepo = null!;
    private ICompetitionRepository _competitionRepo = null!;
    private IRunRepository _runRepo = null!;
    private RankingService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _teamRepo = Substitute.For<ITeamRepository>();
        _competitionRepo = Substitute.For<ICompetitionRepository>();
        _runRepo = Substitute.For<IRunRepository>();
        _sut = new RankingService(_teamRepo, _competitionRepo, _runRepo);
    }

    [Test]
    public async Task GetRankingsAsync_DelegatesToRepository()
    {
        var filter = new RankingFilter(SizeCategory.L, null, null);
        var expected = new PagedResult<Team>(
            new List<Team>
            {
                new() { Id = 1, Rating = 1600, IsActive = true, Handler = new Handler { Name = "John", Country = "GBR", Slug = "john" }, Dog = new Dog { CallName = "Rex", SizeCategory = SizeCategory.L } },
                new() { Id = 2, Rating = 1500, IsActive = true, Handler = new Handler { Name = "Jane", Country = "USA", Slug = "jane" }, Dog = new Dog { CallName = "Luna", SizeCategory = SizeCategory.L } }
            },
            2, 1, 50);

        _teamRepo.GetRankedTeamsAsync(filter).Returns(expected);

        var result = await _sut.GetRankingsAsync(filter);

        Assert.That(result.TotalCount, Is.EqualTo(2));
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].Rating, Is.GreaterThan(result.Items[1].Rating));
    }

    [Test]
    public async Task GetSummaryAsync_ReturnsCounts()
    {
        _teamRepo.GetAllAsync().Returns(new List<Team>
        {
            new() { Id = 1, IsActive = true },
            new() { Id = 2, IsActive = true },
            new() { Id = 3, IsActive = false }
        });

        _competitionRepo.GetListAsync(Arg.Any<CompetitionFilter>())
            .Returns(new PagedResult<Competition>([], 5, 1, 1));

        _runRepo.GetAllInWindowAsync(Arg.Any<DateOnly>())
            .Returns(new List<Run>
            {
                new() { Id = 1 },
                new() { Id = 2 },
                new() { Id = 3 },
                new() { Id = 4 }
            });

        var summary = await _sut.GetSummaryAsync();

        Assert.That(summary.QualifiedTeams, Is.EqualTo(2));
        Assert.That(summary.Competitions, Is.EqualTo(5));
        Assert.That(summary.Runs, Is.EqualTo(4));
    }
}
