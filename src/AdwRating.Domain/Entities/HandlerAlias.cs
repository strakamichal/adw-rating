using AdwRating.Domain.Enums;

namespace AdwRating.Domain.Entities;

public class HandlerAlias
{
    public int Id { get; set; }
    public string AliasName { get; set; } = string.Empty;
    public int CanonicalHandlerId { get; set; }
    public AliasSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
}
