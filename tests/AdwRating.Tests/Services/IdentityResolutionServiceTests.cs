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
    public async Task ResolveHandlerAsync_NoMatch_CreatesNewHandler()
    {
        var created = new Handler
        {
            Id = 99, Name = "New Person", NormalizedName = "new person",
            Country = "CZE", Slug = "new-person"
        };

        _handlerAliasRepo.FindByAliasNameAsync("new person")
            .Returns((HandlerAlias?)null);
        _handlerAliasRepo.FindByAliasNameAsync("person new")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("new person", "CZE")
            .Returns((Handler?)null);
        _handlerRepo.FindByNormalizedNameAsync("new person")
            .Returns(new List<Handler>());
        _handlerRepo.FindByNormalizedNameContainingAsync("new person", "CZE")
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
        await _handlerAliasRepo.Received(1).CreateAsync(
            Arg.Is<HandlerAlias>(a =>
                a.AliasName == "person new" &&
                a.CanonicalHandlerId == 99 &&
                a.Source == AliasSource.Import));
    }

    [Test]
    public async Task ResolveHandlerAsync_CountryMismatch_SingleNameOnlyMatch_ReturnsFallback()
    {
        var handler = new Handler
        {
            Id = 1, Name = "Ádám-Bökényi Andrea", NormalizedName = "adam bokenyi andrea",
            Country = "HUN", Slug = "adam-bokenyi-andrea"
        };

        _handlerAliasRepo.FindByAliasNameAsync("adam bokenyi andrea")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("adam bokenyi andrea", "AUS")
            .Returns((Handler?)null);
        _handlerRepo.FindByNormalizedNameAsync("adam bokenyi andrea")
            .Returns(new List<Handler> { handler });

        var result = await _sut.ResolveHandlerAsync("Ádám-Bökényi Andrea", "AUS");

        Assert.That(result, Is.SameAs(handler));
        await _handlerRepo.DidNotReceive().CreateAsync(Arg.Any<Handler>());
    }

    [Test]
    public async Task ResolveHandlerAsync_SingleTokenName_SkipsCountryFallback()
    {
        _handlerAliasRepo.FindByAliasNameAsync("martin")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("martin", "CZE")
            .Returns((Handler?)null);

        var created = new Handler
        {
            Id = 99, Name = "Martin", NormalizedName = "martin",
            Country = "CZE", Slug = "martin"
        };
        _handlerRepo.CreateAsync(Arg.Any<Handler>())
            .Returns(created);

        var result = await _sut.ResolveHandlerAsync("Martin", "CZE");

        Assert.That(result, Is.SameAs(created));
        // Should NOT have called FindByNormalizedNameAsync (single token)
        await _handlerRepo.DidNotReceive().FindByNormalizedNameAsync(Arg.Any<string>());
    }

    [Test]
    public async Task ResolveHandlerAsync_CountryFallback_MultipleMatches_CreatesNew()
    {
        _handlerAliasRepo.FindByAliasNameAsync("john smith")
            .Returns((HandlerAlias?)null);
        _handlerAliasRepo.FindByAliasNameAsync("smith john")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("john smith", "AUS")
            .Returns((Handler?)null);
        // Two handlers with same name in different countries — ambiguous
        _handlerRepo.FindByNormalizedNameAsync("john smith")
            .Returns(new List<Handler>
            {
                new() { Id = 1, Name = "John Smith", NormalizedName = "john smith", Country = "GBR", Slug = "john-smith" },
                new() { Id = 2, Name = "John Smith", NormalizedName = "john smith", Country = "USA", Slug = "john-smith-2" }
            });
        // Containment returns both exact matches (which get filtered as self-matches) — no unique match
        _handlerRepo.FindByNormalizedNameContainingAsync("john smith", "AUS")
            .Returns(new List<Handler>());

        var created = new Handler
        {
            Id = 99, Name = "John Smith", NormalizedName = "john smith",
            Country = "AUS", Slug = "john-smith-3"
        };
        _handlerRepo.CreateAsync(Arg.Any<Handler>())
            .Returns(created);

        var result = await _sut.ResolveHandlerAsync("John Smith", "AUS");

        Assert.That(result, Is.SameAs(created));
        await _handlerRepo.Received(1).CreateAsync(Arg.Any<Handler>());
    }

    [Test]
    public async Task ResolveHandlerAsync_ContainmentMatch_ShorterToLonger()
    {
        var existing = new Handler
        {
            Id = 1, Name = "Adrian Bajo Alonso", NormalizedName = "adrian bajo alonso",
            Country = "ESP", Slug = "adrian-bajo-alonso"
        };

        _handlerAliasRepo.FindByAliasNameAsync("adrian bajo")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("adrian bajo", "ESP")
            .Returns((Handler?)null);
        _handlerRepo.FindByNormalizedNameAsync("adrian bajo")
            .Returns(new List<Handler>());
        _handlerRepo.FindByNormalizedNameContainingAsync("adrian bajo", "ESP")
            .Returns(new List<Handler> { existing });

        var result = await _sut.ResolveHandlerAsync("Adrian Bajo", "ESP");

        Assert.That(result, Is.SameAs(existing));
        await _handlerRepo.DidNotReceive().CreateAsync(Arg.Any<Handler>());
        // Should create a FuzzyMatch alias
        await _handlerAliasRepo.Received(1).CreateAsync(
            Arg.Is<HandlerAlias>(a =>
                a.AliasName == "adrian bajo" &&
                a.CanonicalHandlerId == 1 &&
                a.Source == AliasSource.FuzzyMatch));
    }

    [Test]
    public async Task ResolveHandlerAsync_ContainmentMatch_LongerToShorter()
    {
        var existing = new Handler
        {
            Id = 1, Name = "Adrian Bajo", NormalizedName = "adrian bajo",
            Country = "ESP", Slug = "adrian-bajo"
        };

        _handlerAliasRepo.FindByAliasNameAsync("adrian bajo alonso")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("adrian bajo alonso", "ESP")
            .Returns((Handler?)null);
        _handlerRepo.FindByNormalizedNameAsync("adrian bajo alonso")
            .Returns(new List<Handler>());
        _handlerRepo.FindByNormalizedNameContainingAsync("adrian bajo alonso", "ESP")
            .Returns(new List<Handler> { existing });

        var result = await _sut.ResolveHandlerAsync("Adrian Bajo Alonso", "ESP");

        Assert.That(result, Is.SameAs(existing));
        await _handlerRepo.DidNotReceive().CreateAsync(Arg.Any<Handler>());
    }

    [Test]
    public async Task ResolveHandlerAsync_ContainmentMatch_ShortName_SkipsFuzzy()
    {
        // "Li Wei" is only 6 chars — below the 10-char minimum for containment match
        _handlerAliasRepo.FindByAliasNameAsync("li wei")
            .Returns((HandlerAlias?)null);
        _handlerRepo.FindByNormalizedNameAndCountryAsync("li wei", "CHN")
            .Returns((Handler?)null);
        _handlerRepo.FindByNormalizedNameAsync("li wei")
            .Returns(new List<Handler>());

        var created = new Handler
        {
            Id = 99, Name = "Li Wei", NormalizedName = "li wei",
            Country = "CHN", Slug = "li-wei"
        };
        _handlerAliasRepo.FindByAliasNameAsync("wei li")
            .Returns((HandlerAlias?)null);
        _handlerRepo.CreateAsync(Arg.Any<Handler>())
            .Returns(created);

        var result = await _sut.ResolveHandlerAsync("Li Wei", "CHN");

        Assert.That(result, Is.SameAs(created));
        // Should NOT have called containment search (name too short)
        await _handlerRepo.DidNotReceive().FindByNormalizedNameContainingAsync(Arg.Any<string>(), Arg.Any<string>());
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
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("rex", SizeCategory.L)
            .Returns(new List<Dog> { dog });

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
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("rex", SizeCategory.L)
            .Returns(new List<Dog> { dog });

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
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("buddy", SizeCategory.M)
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

    [Test]
    public async Task ResolveDogAsync_MultipleDogsWithSameName_FindsHandlersDog()
    {
        // Two different dogs named "Lucky" in size L, belonging to different handlers
        var otherHandlersDog = new Dog
        {
            Id = 1, CallName = "Lucky", NormalizedCallName = "lucky",
            Breed = "Border Collie", SizeCategory = SizeCategory.L
        };
        var thisHandlersDog = new Dog
        {
            Id = 2, CallName = "Lucky", NormalizedCallName = "lucky",
            Breed = "Sheltie", SizeCategory = SizeCategory.L
        };

        // Handler 20 owns dog 2 via a team
        _teamRepo.GetByHandlerIdAsync(20)
            .Returns(new List<Team> { new() { Id = 50, HandlerId = 20, DogId = 2 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("lucky", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        // Returns BOTH dogs — the old code would only return the first one (wrong handler's dog)
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("lucky", SizeCategory.L)
            .Returns(new List<Dog> { otherHandlersDog, thisHandlersDog });

        var result = await _sut.ResolveDogAsync("Lucky", "Sheltie", SizeCategory.L, 20);

        Assert.That(result, Is.SameAs(thisHandlersDog));
        await _dogRepo.DidNotReceive().CreateAsync(Arg.Any<Dog>());
    }

    [Test]
    public async Task ResolveDogAsync_FuzzyContainment_CallNameMatchesRegisteredName()
    {
        // Handler has "Berta z Kojca Coli" (call name "Berta"), new row has just "Berta"
        var existingDog = new Dog
        {
            Id = 5, CallName = "Berta", NormalizedCallName = "berta",
            RegisteredName = "Berta z Kojca Coli", SizeCategory = SizeCategory.M
        };

        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team> { new() { Id = 30, HandlerId = 10, DogId = 5 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("berta", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("berta", SizeCategory.M)
            .Returns(new List<Dog>()); // No exact match (different name format)
        _dogRepo.GetByIdAsync(5).Returns(existingDog);

        var result = await _sut.ResolveDogAsync("Berta", null, SizeCategory.M, 10);

        Assert.That(result, Is.SameAs(existingDog));
        await _dogRepo.DidNotReceive().CreateAsync(Arg.Any<Dog>());
    }

    [Test]
    public async Task ResolveDogAsync_FuzzyContainment_LongNameMatchesCallName()
    {
        // Handler has "Cinnamon" (call name), new row has "Cinnamon Flycatcher of Noble County"
        var existingDog = new Dog
        {
            Id = 6, CallName = "Cinnamon", NormalizedCallName = "cinnamon",
            RegisteredName = null, SizeCategory = SizeCategory.S
        };

        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team> { new() { Id = 31, HandlerId = 10, DogId = 6 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("cinnamon flycatcher of noble county", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogAliasRepo.FindByAliasNameAndTypeAsync("cinnamon", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("cinnamon flycatcher of noble county", SizeCategory.S)
            .Returns(new List<Dog>());
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("cinnamon", SizeCategory.S)
            .Returns(new List<Dog>());
        _dogRepo.GetByIdAsync(6).Returns(existingDog);

        var result = await _sut.ResolveDogAsync("Cinnamon Flycatcher of Noble County", null, SizeCategory.S, 10);

        Assert.That(result, Is.SameAs(existingDog));
        await _dogRepo.DidNotReceive().CreateAsync(Arg.Any<Dog>());
    }

    [Test]
    public async Task ResolveDogAsync_FuzzyContainment_AdjacentSizeMatches()
    {
        // Handler has "Gia" in S, new row has "Gia" in M (adjacent sizes)
        var existingDog = new Dog
        {
            Id = 7, CallName = "Gia", NormalizedCallName = "gia",
            RegisteredName = null, SizeCategory = SizeCategory.S
        };

        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team> { new() { Id = 32, HandlerId = 10, DogId = 7 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("gia", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("gia", SizeCategory.M)
            .Returns(new List<Dog>()); // Exact match fails (different size)
        _dogRepo.GetByIdAsync(7).Returns(existingDog);

        var result = await _sut.ResolveDogAsync("Gia", null, SizeCategory.M, 10);

        Assert.That(result, Is.SameAs(existingDog));
        await _dogRepo.DidNotReceive().CreateAsync(Arg.Any<Dog>());
    }

    [Test]
    public async Task ResolveDogAsync_FuzzyContainment_NonWordBoundary_DoesNotMatch()
    {
        // Handler has "Borealis", new row has "Lis" — NOT a word-boundary match
        var existingDog = new Dog
        {
            Id = 8, CallName = "Borealis", NormalizedCallName = "borealis",
            RegisteredName = null, SizeCategory = SizeCategory.L
        };

        var created = new Dog
        {
            Id = 99, CallName = "Lis", NormalizedCallName = "lis",
            SizeCategory = SizeCategory.L
        };

        _teamRepo.GetByHandlerIdAsync(10)
            .Returns(new List<Team> { new() { Id = 33, HandlerId = 10, DogId = 8 } });
        _dogAliasRepo.FindByAliasNameAndTypeAsync("lis", DogAliasType.CallName)
            .Returns((DogAlias?)null);
        _dogRepo.FindAllByNormalizedNameAndSizeAsync("lis", SizeCategory.L)
            .Returns(new List<Dog>());
        _dogRepo.GetByIdAsync(8).Returns(existingDog);
        _dogRepo.CreateAsync(Arg.Any<Dog>()).Returns(created);

        var result = await _sut.ResolveDogAsync("Lis", null, SizeCategory.L, 10);

        // Should NOT match "Borealis" — should create a new dog
        Assert.That(result, Is.SameAs(created));
        await _dogRepo.Received(1).CreateAsync(Arg.Any<Dog>());
    }

    #endregion

    #region IsWordBoundaryContainment

    [TestCase("berta", "berta z kojca coli", true)]
    [TestCase("cinnamon", "cinnamon flycatcher of noble county", true)]
    [TestCase("lis", "borealis", false)]  // Not at word boundary
    [TestCase("berta", "berta", false)]   // Exact match — not containment
    [TestCase("ab", "ab cde", false)]     // Too short (< 3 chars)
    [TestCase("day", "daylight neverending", false)] // Not at word boundary on right
    [TestCase("day", "birth day party", true)] // Word boundary match
    public void IsWordBoundaryContainment_ReturnsExpected(string a, string b, bool expected)
    {
        Assert.That(IdentityResolutionService.IsWordBoundaryContainment(a, b), Is.EqualTo(expected));
    }

    #endregion

    #region IsAdjacentOrSameSize

    [TestCase(SizeCategory.S, SizeCategory.S, true)]
    [TestCase(SizeCategory.S, SizeCategory.M, true)]
    [TestCase(SizeCategory.M, SizeCategory.S, true)]
    [TestCase(SizeCategory.I, SizeCategory.L, true)]
    [TestCase(SizeCategory.L, SizeCategory.I, true)]
    [TestCase(SizeCategory.S, SizeCategory.I, false)]
    [TestCase(SizeCategory.S, SizeCategory.L, false)]
    [TestCase(SizeCategory.M, SizeCategory.I, false)]
    [TestCase(SizeCategory.M, SizeCategory.L, false)]
    public void IsAdjacentOrSameSize_ReturnsExpected(SizeCategory a, SizeCategory b, bool expected)
    {
        Assert.That(IdentityResolutionService.IsAdjacentOrSameSize(a, b), Is.EqualTo(expected));
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

    #region BuildNameRotations

    [Test]
    public void BuildNameRotations_SingleToken_ReturnsEmpty()
    {
        var result = IdentityResolutionService.BuildNameRotations("smith");
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void BuildNameRotations_TwoTokens_ReturnsSwapped()
    {
        var result = IdentityResolutionService.BuildNameRotations("john smith");
        Assert.That(result, Is.EqualTo(new[] { "smith john" }));
    }

    [Test]
    public void BuildNameRotations_ThreeTokens_ReturnsBothRotations()
    {
        // "de groote andy" → ["andy de groote", "groote andy de"]
        var result = IdentityResolutionService.BuildNameRotations("de groote andy");
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Does.Contain("andy de groote"));
        Assert.That(result, Does.Contain("groote andy de"));
    }

    [Test]
    public void BuildNameRotations_FourTokens_ReturnsBothRotations()
    {
        // "van der stock thora" → ["thora van der stock", "der stock thora van"]
        var result = IdentityResolutionService.BuildNameRotations("van der stock thora");
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result, Does.Contain("thora van der stock"));
        Assert.That(result, Does.Contain("der stock thora van"));
    }

    #endregion
}
