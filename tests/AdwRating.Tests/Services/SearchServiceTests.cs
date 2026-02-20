using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using AdwRating.Service;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class SearchServiceTests
{
    private IHandlerRepository _handlerRepo = null!;
    private IDogRepository _dogRepo = null!;
    private ICompetitionRepository _competitionRepo = null!;
    private ITeamRepository _teamRepo = null!;
    private SearchService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _handlerRepo = Substitute.For<IHandlerRepository>();
        _dogRepo = Substitute.For<IDogRepository>();
        _competitionRepo = Substitute.For<ICompetitionRepository>();
        _teamRepo = Substitute.For<ITeamRepository>();
        _sut = new SearchService(_handlerRepo, _dogRepo, _competitionRepo, _teamRepo);
    }

    [Test]
    public async Task SearchAsync_CombinesResults_FromAllSources()
    {
        _handlerRepo.SearchAsync("john", 10).Returns(new List<Handler>
        {
            new() { Name = "John Smith", Slug = "john-smith", Country = "GBR" }
        });

        _dogRepo.SearchAsync("john", 10).Returns(new List<Dog>());

        _competitionRepo.GetListAsync(Arg.Any<CompetitionFilter>())
            .Returns(new PagedResult<Competition>(
                new List<Competition>
                {
                    new() { Slug = "johnstown-2024", Name = "Johnstown Open 2024", Date = new DateOnly(2024, 5, 1) }
                }, 1, 1, 10));

        var results = await _sut.SearchAsync("john");

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Type, Is.EqualTo("handler"));
        Assert.That(results[0].DisplayName, Is.EqualTo("John Smith"));
        Assert.That(results[1].Type, Is.EqualTo("competition"));
    }

    [Test]
    public async Task SearchAsync_DogResults_MappedToTeams()
    {
        _handlerRepo.SearchAsync("rex", 10).Returns(new List<Handler>());

        var dog = new Dog { Id = 1, CallName = "Rex", SizeCategory = SizeCategory.L };
        _dogRepo.SearchAsync("rex", 10).Returns(new List<Dog> { dog });

        var team = new Team
        {
            Id = 1, Slug = "john-rex", Rating = 1500,
            Handler = new Handler { Name = "John Smith", Slug = "john-smith", Country = "GBR" },
            Dog = dog
        };
        _teamRepo.GetByDogIdAsync(1).Returns(new List<Team> { team });

        _competitionRepo.GetListAsync(Arg.Any<CompetitionFilter>())
            .Returns(new PagedResult<Competition>([], 0, 1, 10));

        var results = await _sut.SearchAsync("rex");

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Type, Is.EqualTo("team"));
        Assert.That(results[0].DisplayName, Is.EqualTo("John Smith & Rex"));
        Assert.That(results[0].Subtitle, Is.EqualTo("1500"));
    }

    [Test]
    public async Task SearchAsync_RespectsLimit()
    {
        var handlers = Enumerable.Range(1, 5).Select(i => new Handler
        {
            Name = $"Handler {i}", Slug = $"handler-{i}", Country = "GBR"
        }).ToList();

        _handlerRepo.SearchAsync("handler", 3).Returns(handlers);
        _dogRepo.SearchAsync("handler", 3).Returns(new List<Dog>());
        _competitionRepo.GetListAsync(Arg.Any<CompetitionFilter>())
            .Returns(new PagedResult<Competition>([], 0, 1, 3));

        var results = await _sut.SearchAsync("handler", limit: 3);

        Assert.That(results, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task SearchAsync_NoResults_ReturnsEmpty()
    {
        _handlerRepo.SearchAsync("zzz", 10).Returns(new List<Handler>());
        _dogRepo.SearchAsync("zzz", 10).Returns(new List<Dog>());
        _competitionRepo.GetListAsync(Arg.Any<CompetitionFilter>())
            .Returns(new PagedResult<Competition>([], 0, 1, 10));

        var results = await _sut.SearchAsync("zzz");

        Assert.That(results, Is.Empty);
    }
}
