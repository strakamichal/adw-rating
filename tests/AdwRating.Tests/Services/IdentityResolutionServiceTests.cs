using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Service;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class IdentityResolutionServiceTests
{
    private IHandlerRepository _handlerRepo = null!;
    private IHandlerAliasRepository _handlerAliasRepo = null!;
    private IDogRepository _dogRepo = null!;
    private IDogAliasRepository _dogAliasRepo = null!;
    private ITeamRepository _teamRepo = null!;
    private IRatingConfigurationRepository _configRepo = null!;
    private ILogger<IdentityResolutionService> _logger = null!;
    private IdentityResolutionService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _handlerRepo = Substitute.For<IHandlerRepository>();
        _handlerAliasRepo = Substitute.For<IHandlerAliasRepository>();
        _dogRepo = Substitute.For<IDogRepository>();
        _dogAliasRepo = Substitute.For<IDogAliasRepository>();
        _teamRepo = Substitute.For<ITeamRepository>();
        _configRepo = Substitute.For<IRatingConfigurationRepository>();
        _logger = Substitute.For<ILogger<IdentityResolutionService>>();

        _sut = new IdentityResolutionService(
            _handlerRepo,
            _handlerAliasRepo,
            _dogRepo,
            _dogAliasRepo,
            _teamRepo,
            _configRepo,
            _logger);
    }

    #region ResolveHandlerAsync

    [Test]
    public async Task ResolveHandlerAsync_ExactMatch_ReturnsExistingHandler()
    {
        var handler = new Handler
        {
            Id = 1, Name = "John Smith", NormalizedName = "john smith",
            Country = "GBR", Slug = "john-smith"
        };

        _handlerAliasRepo.FindByAliasNameAsync("john smith")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("john smith", "GBR")
            .Returns(handler);

        var result = await _sut.ResolveHandlerAsync("John Smith", "GBR");

        Assert.That(result, Is.SameAs(handler));
        await _handlerRepo.DidNotReceive().CreateAsync(Arg.Any<Handler>());
    }

    [Test]
    public async Task ResolveHandlerAsync_AliasMatch_ReturnsCanonicalHandler()
    {
        var alias = new HandlerAlias
        {
            Id = 10, AliasName = "jon smith", CanonicalHandlerId = 1,
            Source = AliasSource.FuzzyMatch
        };
        var canonical = new Handler
        {
            Id = 1, Name = "John Smith", NormalizedName = "john smith",
            Country = "GBR", Slug = "john-smith"
        };

        _handlerAliasRepo.FindByAliasNameAsync("jon smith")
            .Returns(alias);
        _handlerRepo.GetByIdAsync(1)
            .Returns(canonical);

        var result = await _sut.ResolveHandlerAsync("Jon Smith", "GBR");

        Assert.That(result, Is.SameAs(canonical));
        await _handlerRepo.DidNotReceive().FindByNormalizedNameAndCountryAsync(
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task ResolveHandlerAsync_FuzzyMatch_CreatesAliasAndReturnsHandler()
    {
        var existing = new Handler
        {
            Id = 1, Name = "John Smith", NormalizedName = "john smith",
            Country = "GBR", Slug = "john-smith"
        };

        _handlerAliasRepo.FindByAliasNameAsync("john smth")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("john smth", "GBR")
            .Returns((Handler?)null);
        _handlerRepo.SearchAsync("john smth", 50)
            .Returns(new List<Handler> { existing });

        var result = await _sut.ResolveHandlerAsync("John Smth", "GBR");

        Assert.That(result, Is.SameAs(existing));
        await _handlerAliasRepo.Received(1).CreateAsync(
            Arg.Is<HandlerAlias>(a =>
                a.AliasName == "john smth" &&
                a.CanonicalHandlerId == 1 &&
                a.Source == AliasSource.FuzzyMatch));
    }

    [Test]
    public async Task ResolveHandlerAsync_NoMatch_CreatesNewHandler()
    {
        var created = new Handler
        {
            Id = 99, Name = "New Person", NormalizedName = "new person",
            Country = "CZE", Slug = "new-person"
        };

        _handlerAliasRepo.FindByAliasNameAsync("new person")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("new person", "CZE")
            .Returns((Handler?)null);
        _handlerRepo.SearchAsync("new person", 50)
            .Returns(new List<Handler>());
        _handlerRepo.CreateAsync(Arg.Any<Handler>())
            .Returns(created);

        var result = await _sut.ResolveHandlerAsync("New Person", "CZE");

        Assert.That(result, Is.SameAs(created));
        await _handlerRepo.Received(1).CreateAsync(
            Arg.Is<Handler>(h =>
                h.Name == "New Person" &&
                h.NormalizedName == "new person" &&
                h.Country == "CZE" &&
                h.Slug == "new-person"));
    }

    [Test]
    public async Task ResolveHandlerAsync_FuzzyDifferentCountry_CreatesNewHandler()
    {
        var differentCountryHandler = new Handler
        {
            Id = 1, Name = "John Smith", NormalizedName = "john smith",
            Country = "USA", Slug = "john-smith"
        };
        var created = new Handler
        {
            Id = 99, Name = "John Smth", NormalizedName = "john smth",
            Country = "GBR", Slug = "john-smth"
        };

        _handlerAliasRepo.FindByAliasNameAsync("john smth")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("john smth", "GBR")
            .Returns((Handler?)null);
        _handlerRepo.SearchAsync("john smth", 50)
            .Returns(new List<Handler> { differentCountryHandler });
        _handlerRepo.CreateAsync(Arg.Any<Handler>())
            .Returns(created);

        var result = await _sut.ResolveHandlerAsync("John Smth", "GBR");

        Assert.That(result, Is.SameAs(created));
        await _handlerAliasRepo.DidNotReceive().CreateAsync(Arg.Any<HandlerAlias>());
    }

    #endregion

    #region ResolveDogAsync

    [Test]
    public async Task ResolveDogAsync_ExactMatch_ReturnsExistingDog()
    {
        var dog = new Dog
        {
            Id = 1, CallName = "Rex", NormalizedCallName = "rex",
            Breed = "Border Collie", SizeCategory = SizeCategory.L
        };

        // Handler 10 owns dog 1 via a team
        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team> { new() { Id = 30, HandlerId = 10, DogId = 1 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("rex", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindByNormalizedNameAndSizeAsync("rex", SizeCategory.L)
            .Returns(dog);

        var result = await _sut.ResolveDogAsync("Rex", "Border Collie", SizeCategory.L, 10);

        Assert.That(result, Is.SameAs(dog));
        await _dogRepo.DidNotReceive().CreateAsync(Arg.Any<Dog>());
    }

    [Test]
    public async Task ResolveDogAsync_BreedUpdate_UpdatesBreedWhenNull()
    {
        var dog = new Dog
        {
            Id = 1, CallName = "Rex", NormalizedCallName = "rex",
            Breed = null, SizeCategory = SizeCategory.L
        };

        // Handler 10 owns dog 1 via a team
        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team> { new() { Id = 30, HandlerId = 10, DogId = 1 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("rex", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindByNormalizedNameAndSizeAsync("rex", SizeCategory.L)
            .Returns(dog);

        var result = await _sut.ResolveDogAsync("Rex", "Border Collie", SizeCategory.L, 10);

        Assert.That(result.Breed, Is.EqualTo("Border Collie"));
        await _dogRepo.Received(1).UpdateAsync(
            Arg.Is<Dog>(d => d.Breed == "Border Collie"));
    }

    [Test]
    public async Task ResolveDogAsync_NoMatch_CreatesNewDog()
    {
        var created = new Dog
        {
            Id = 99, CallName = "Buddy", NormalizedCallName = "buddy",
            Breed = "Sheltie", SizeCategory = SizeCategory.M
        };

        // Handler 10 has no existing dogs
        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team>());
        _dogAliasRepo.FindByAliasNameAndTypeAsync("buddy", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindByNormalizedNameAndSizeAsync("buddy", SizeCategory.M)
            .Returns((Dog?)null);
        _dogRepo.SearchAsync("buddy", 50)
            .Returns(new List<Dog>());
        _dogRepo.CreateAsync(Arg.Any<Dog>())
            .Returns(created);

        var result = await _sut.ResolveDogAsync("Buddy", "Sheltie", SizeCategory.M, 10);

        Assert.That(result, Is.SameAs(created));
        await _dogRepo.Received(1).CreateAsync(
            Arg.Is<Dog>(d =>
                d.CallName == "Buddy" &&
                d.NormalizedCallName == "buddy" &&
                d.Breed == "Sheltie" &&
                d.SizeCategory == SizeCategory.M));
    }

    #endregion

    #region ResolveTeamAsync

    [Test]
    public async Task ResolveTeamAsync_ExistingTeam_ReturnsIt()
    {
        var team = new Team
        {
            Id = 1, HandlerId = 10, DogId = 20,
            Mu = 25.0f, Sigma = 8.333f
        };

        _teamRepo.GetByHandlerAndDogAsync(10, 20)
            .Returns(team);

        var result = await _sut.ResolveTeamAsync(10, 20);

        Assert.That(result, Is.SameAs(team));
        await _teamRepo.DidNotReceive().CreateAsync(Arg.Any<Team>());
    }

    [Test]
    public async Task ResolveTeamAsync_NewTeam_CreatesWithConfigDefaults()
    {
        var config = new RatingConfiguration
        {
            Id = 1, IsActive = true, Mu0 = 25.0f, Sigma0 = 8.333f
        };
        var handler = new Handler
        {
            Id = 10, Name = "John Smith", NormalizedName = "john smith",
            Country = "GBR", Slug = "john-smith"
        };
        var dog = new Dog
        {
            Id = 20, CallName = "Rex", NormalizedCallName = "rex",
            SizeCategory = SizeCategory.L
        };

        _teamRepo.GetByHandlerAndDogAsync(10, 20)
            .Returns((Team?)null);
        _configRepo.GetActiveAsync()
            .Returns(config);
        _handlerRepo.GetByIdAsync(10)
            .Returns(handler);
        _dogRepo.GetByIdAsync(20)
            .Returns(dog);
        _teamRepo.CreateAsync(Arg.Any<Team>())
            .Returns(callInfo => callInfo.Arg<Team>());

        var result = await _sut.ResolveTeamAsync(10, 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.HandlerId, Is.EqualTo(10));
            Assert.That(result.DogId, Is.EqualTo(20));
            Assert.That(result.Mu, Is.EqualTo(25.0f));
            Assert.That(result.Sigma, Is.EqualTo(8.333f));
            Assert.That(result.Slug, Is.EqualTo("john-smith-rex"));
            Assert.That(result.IsProvisional, Is.True);
            Assert.That(result.IsActive, Is.False);
            Assert.That(result.RunCount, Is.EqualTo(0));
        });
    }

    #endregion
}
