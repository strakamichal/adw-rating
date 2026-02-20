using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Service;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class TeamProfileServiceTests
{
    private ITeamRepository _teamRepo = null!;
    private IRunResultRepository _runResultRepo = null!;
    private TeamProfileService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _teamRepo = Substitute.For<ITeamRepository>();
        _runResultRepo = Substitute.For<IRunResultRepository>();
        _sut = new TeamProfileService(_teamRepo, _runResultRepo);
    }

    [Test]
    public async Task GetBySlugAsync_TeamExists_ReturnsDto()
    {
        var team = new Team
        {
            Id = 1, Slug = "john-rex", Rating = 1550, Sigma = 50, PrevRating = 1500, PeakRating = 1600,
            RunCount = 10, FinishedRunCount = 8, Top3RunCount = 3, IsActive = true, IsProvisional = false,
            TierLabel = TierLabel.Expert, Mu = 25, PrevMu = 24, PrevSigma = 5,
            Handler = new Handler { Name = "John", Slug = "john", Country = "GBR" },
            Dog = new Dog { CallName = "Rex", RegisteredName = "Rex of Something", Breed = "Border Collie", SizeCategory = SizeCategory.L }
        };

        _teamRepo.GetBySlugAsync("john-rex").Returns(team);

        var results = new List<RunResult>
        {
            new() { Rank = 1, Eliminated = false, Run = new Run { Date = new DateOnly(2024, 1, 1), Competition = new Competition() } },
            new() { Rank = 3, Eliminated = false, Run = new Run { Date = new DateOnly(2024, 2, 1), Competition = new Competition() } },
            new() { Rank = null, Eliminated = true, Run = new Run { Date = new DateOnly(2024, 3, 1), Competition = new Competition() } }
        };
        _runResultRepo.GetByTeamIdAsync(1, null).Returns(results);

        var dto = await _sut.GetBySlugAsync("john-rex");

        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.Slug, Is.EqualTo("john-rex"));
        Assert.That(dto.Rating, Is.EqualTo(1550));
        Assert.That(dto.FinishedPct, Is.EqualTo(0.8f));
        Assert.That(dto.Top3Pct, Is.EqualTo(0.3f));
        Assert.That(dto.AvgRank, Is.EqualTo(2f));
        Assert.That(dto.HandlerName, Is.EqualTo("John"));
        Assert.That(dto.DogCallName, Is.EqualTo("Rex"));
    }

    [Test]
    public async Task GetBySlugAsync_TeamNotFound_ReturnsNull()
    {
        _teamRepo.GetBySlugAsync("nonexistent").Returns((Team?)null);

        var result = await _sut.GetBySlugAsync("nonexistent");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetResultsAsync_ReturnsPaginatedResults()
    {
        var team = new Team { Id = 1, Slug = "john-rex", Handler = new Handler(), Dog = new Dog() };
        _teamRepo.GetBySlugAsync("john-rex").Returns(team);

        var competition = new Competition { Slug = "awc2024", Name = "AWC 2024" };
        var run = new Run
        {
            Date = new DateOnly(2024, 10, 3), SizeCategory = SizeCategory.L,
            Discipline = Discipline.Agility, IsTeamRound = false, Competition = competition
        };

        var results = new List<RunResult>
        {
            new() { Rank = 1, Faults = 0, TimeFaults = 0, Time = 32.5f, Speed = 5.1f, Eliminated = false, Run = run },
            new() { Rank = 2, Faults = 5, TimeFaults = 0, Time = 33.1f, Speed = 5.0f, Eliminated = false, Run = run },
            new() { Rank = null, Eliminated = true, Run = run }
        };
        _runResultRepo.GetByTeamIdAsync(1, null).Returns(results);

        var page = await _sut.GetResultsAsync("john-rex", page: 1, pageSize: 2);

        Assert.That(page.TotalCount, Is.EqualTo(3));
        Assert.That(page.Items, Has.Count.EqualTo(2));
        Assert.That(page.Items[0].CompetitionSlug, Is.EqualTo("awc2024"));
        Assert.That(page.Items[0].Rank, Is.EqualTo(1));
    }

    [Test]
    public async Task GetResultsAsync_TeamNotFound_ReturnsEmpty()
    {
        _teamRepo.GetBySlugAsync("nonexistent").Returns((Team?)null);

        var page = await _sut.GetResultsAsync("nonexistent");

        Assert.That(page.TotalCount, Is.EqualTo(0));
        Assert.That(page.Items, Is.Empty);
    }
}
