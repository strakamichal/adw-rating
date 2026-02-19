using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Entities;

public class DogAlias
{
    public int Id { get; set; }
    public string AliasName { get; set; } = string.Empty;
    public int CanonicalDogId { get; set; }
    public DogAliasType AliasType { get; set; }
    public AliasSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
}
