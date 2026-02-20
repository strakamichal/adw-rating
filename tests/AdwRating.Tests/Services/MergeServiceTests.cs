using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Service;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class MergeServiceTests
{
    private IHandlerRepository _handlerRepo = null!;
    private IDogRepository _dogRepo = null!;
    private IHandlerAliasRepository _handlerAliasRepo = null!;
    private IDogAliasRepository _dogAliasRepo = null!;
    private ILogger<MergeService> _logger = null!;
    private MergeService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _handlerRepo = Substitute.For<IHandlerRepository>();
        _dogRepo = Substitute.For<IDogRepository>();
        _handlerAliasRepo = Substitute.For<IHandlerAliasRepository>();
        _dogAliasRepo = Substitute.For<IDogAliasRepository>();
        _logger = Substitute.For<ILogger<MergeService>>();

        _sut = new MergeService(
            _handlerRepo,
            _dogRepo,
            _handlerAliasRepo,
            _dogAliasRepo,
            _logger);
    }

    [Test]
    public async Task MergeHandlersAsync_BothExist_MergesAndCreatesAlias()
    {
        var source = new Handler { Id = 1, Name = "John Smith", NormalizedName = "john smith", Country = "CZE", Slug = "john-smith" };
        var target = new Handler { Id = 2, Name = "J. Smith", NormalizedName = "j. smith", Country = "CZE", Slug = "j-smith" };

        _handlerRepo.GetByIdAsync(1).Returns(source);
        _handlerRepo.GetByIdAsync(2).Returns(target);

        await _sut.MergeHandlersAsync(1, 2);

        await _handlerRepo.Received(1).MergeAsync(1, 2);
        await _handlerAliasRepo.Received(1).CreateAsync(Arg.Is<HandlerAlias>(a =>
            a.AliasName == "John Smith" &&
            a.CanonicalHandlerId == 2 &&
            a.Source == AliasSource.Manual));
    }

    [Test]
    public void MergeHandlersAsync_SourceNotFound_Throws()
    {
        _handlerRepo.GetByIdAsync(1).Returns((Handler?)null);
        _handlerRepo.GetByIdAsync(2).Returns(new Handler { Id = 2, Name = "Target" });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.MergeHandlersAsync(1, 2));

        Assert.That(ex!.Message, Does.Contain("Source handler 1 not found"));
    }

    [Test]
    public void MergeHandlersAsync_TargetNotFound_Throws()
    {
        _handlerRepo.GetByIdAsync(1).Returns(new Handler { Id = 1, Name = "Source" });
        _handlerRepo.GetByIdAsync(2).Returns((Handler?)null);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.MergeHandlersAsync(1, 2));

        Assert.That(ex!.Message, Does.Contain("Target handler 2 not found"));
    }

    [Test]
    public async Task MergeDogsAsync_SameSize_MergesAndCreatesAlias()
    {
        var source = new Dog { Id = 1, CallName = "Rex", NormalizedCallName = "rex", SizeCategory = SizeCategory.L };
        var target = new Dog { Id = 2, CallName = "Rexy", NormalizedCallName = "rexy", SizeCategory = SizeCategory.L };

        _dogRepo.GetByIdAsync(1).Returns(source);
        _dogRepo.GetByIdAsync(2).Returns(target);

        await _sut.MergeDogsAsync(1, 2);

        await _dogRepo.Received(1).MergeAsync(1, 2);
        await _dogAliasRepo.Received(1).CreateAsync(Arg.Is<DogAlias>(a =>
            a.AliasName == "Rex" &&
            a.CanonicalDogId == 2 &&
            a.AliasType == DogAliasType.CallName &&
            a.Source == AliasSource.Manual));
    }

    [Test]
    public void MergeDogsAsync_DifferentSize_ThrowsInvalidOperation()
    {
        var source = new Dog { Id = 1, CallName = "Rex", NormalizedCallName = "rex", SizeCategory = SizeCategory.L };
        var target = new Dog { Id = 2, CallName = "Rexy", NormalizedCallName = "rexy", SizeCategory = SizeCategory.S };

        _dogRepo.GetByIdAsync(1).Returns(source);
        _dogRepo.GetByIdAsync(2).Returns(target);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.MergeDogsAsync(1, 2));

        Assert.That(ex!.Message, Does.Contain("different size categories"));
    }

    [Test]
    public void MergeDogsAsync_SourceNotFound_Throws()
    {
        _dogRepo.GetByIdAsync(1).Returns((Dog?)null);
        _dogRepo.GetByIdAsync(2).Returns(new Dog { Id = 2, CallName = "Target", SizeCategory = SizeCategory.L });

        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.MergeDogsAsync(1, 2));

        Assert.That(ex!.Message, Does.Contain("Source dog 1 not found"));
    }
}
