using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Entities;

public class Dog
{
    public int Id { get; set; }
    public string CallName { get; set; } = string.Empty;
    public string NormalizedCallName { get; set; } = string.Empty;
    public string? RegisteredName { get; set; }
    public string? NormalizedRegisteredName { get; set; }
    public string? Breed { get; set; }
    public SizeCategory SizeCategory { get; set; }
    public SizeCategory? SizeCategoryOverride { get; set; }

    // Navigation properties
    public ICollection<Team> Teams { get; set; } = new List<Team>();
}
