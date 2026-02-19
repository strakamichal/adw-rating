using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Builders;

public class DogBuilder
{
    private int _id = 1;
    private string _callName = "Rex";
    private string _normalizedCallName = "rex";
    private string? _registeredName;
    private string? _breed = "Border Collie";
    private SizeCategory _sizeCategory = SizeCategory.L;
    private SizeCategory? _sizeCategoryOverride;

    public DogBuilder WithId(int id) { _id = id; return this; }

    public DogBuilder WithCallName(string callName)
    {
        _callName = callName;
        _normalizedCallName = NameNormalizer.Normalize(callName);
        return this;
    }

    public DogBuilder WithNormalizedCallName(string normalizedCallName) { _normalizedCallName = normalizedCallName; return this; }
    public DogBuilder WithRegisteredName(string? registeredName) { _registeredName = registeredName; return this; }
    public DogBuilder WithBreed(string? breed) { _breed = breed; return this; }
    public DogBuilder WithSizeCategory(SizeCategory sizeCategory) { _sizeCategory = sizeCategory; return this; }
    public DogBuilder WithSizeCategoryOverride(SizeCategory? sizeCategoryOverride) { _sizeCategoryOverride = sizeCategoryOverride; return this; }

    public Dog Build() => new()
    {
        Id = _id,
        CallName = _callName,
        NormalizedCallName = _normalizedCallName,
        RegisteredName = _registeredName,
        Breed = _breed,
        SizeCategory = _sizeCategory,
        SizeCategoryOverride = _sizeCategoryOverride,
    };
}
