namespace AdwRating.Tests.Builders;

using AdwRating.Domain.Entities;

public class CompetitionBuilder
{
    private int _id = 1;
    private string _slug = "test-comp-2024";
    private string _name = "Test Competition 2024";
    private DateOnly _date = new(2024, 10, 3);
    private DateOnly? _endDate;
    private string? _country;
    private string? _location;
    private int _tier = 2;
    private string? _organization;

    public CompetitionBuilder WithId(int id) { _id = id; return this; }
    public CompetitionBuilder WithSlug(string slug) { _slug = slug; return this; }
    public CompetitionBuilder WithName(string name) { _name = name; return this; }
    public CompetitionBuilder WithDate(DateOnly date) { _date = date; return this; }
    public CompetitionBuilder WithEndDate(DateOnly? endDate) { _endDate = endDate; return this; }
    public CompetitionBuilder WithCountry(string? country) { _country = country; return this; }
    public CompetitionBuilder WithLocation(string? location) { _location = location; return this; }
    public CompetitionBuilder WithTier(int tier) { _tier = tier; return this; }
    public CompetitionBuilder WithOrganization(string? organization) { _organization = organization; return this; }

    public Competition Build() => new()
    {
        Id = _id,
        Slug = _slug,
        Name = _name,
        Date = _date,
        EndDate = _endDate,
        Country = _country,
        Location = _location,
        Tier = _tier,
        Organization = _organization,
    };
}
