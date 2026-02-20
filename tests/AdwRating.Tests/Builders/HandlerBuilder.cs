using AdwRating.Domain.Entities;
using AdwRating.Domain.Helpers;

namespace AdwRating.Tests.Builders;

public class HandlerBuilder
{
    private int _id = 1;
    private string _name = "John Smith";
    private string _normalizedName = "john smith";
    private string _country = "GBR";
    private string _slug = "john-smith";

    public HandlerBuilder WithId(int id) { _id = id; return this; }

    public HandlerBuilder WithName(string name)
    {
        _name = name;
        _normalizedName = NameNormalizer.Normalize(name);
        _slug = SlugHelper.GenerateSlug(name);
        return this;
    }

    public HandlerBuilder WithNormalizedName(string normalizedName) { _normalizedName = normalizedName; return this; }
    public HandlerBuilder WithCountry(string country) { _country = country; return this; }
    public HandlerBuilder WithSlug(string slug) { _slug = slug; return this; }

    public Handler Build() => new()
    {
        Id = _id,
        Name = _name,
        NormalizedName = _normalizedName,
        Country = _country,
        Slug = _slug,
    };
}
