using AdwRating.Domain.Enums;

namespace AdwRating.Service.Import;

public static class SizeCategoryMapper
{
    public static (SizeCategory? Mapped, bool Excluded) Map(string? organization, string sourceCategory)
    {
        var cat = sourceCategory.Trim();
        var org = organization?.Trim().ToUpperInvariant();

        return org switch
        {
            null or "" or "FCI" => MapFci(cat),
            "AKC" => MapAkc(cat),
            "USDAA" => MapUsdaa(cat),
            "WAO" => MapWao(cat),
            "UKI" => MapUki(cat),
            "IFCS" => MapIfcs(cat),
            _ => (null, false),
        };
    }

    private static (SizeCategory? Mapped, bool Excluded) MapFci(string cat)
    {
        return cat.ToUpperInvariant() switch
        {
            "S" or "SMALL" or "XS" => (SizeCategory.S, false),
            "M" or "MEDIUM" => (SizeCategory.M, false),
            "I" or "INTERMEDIATE" => (SizeCategory.I, false),
            "L" or "LARGE" => (SizeCategory.L, false),
            _ => (null, false),
        };
    }

    private static (SizeCategory? Mapped, bool Excluded) MapAkc(string cat)
    {
        if (cat.Contains("Preferred", StringComparison.OrdinalIgnoreCase))
            return (null, true);

        return cat.Trim('"') switch
        {
            "8\"" or "8" => (SizeCategory.S, false),
            "12\"" or "12" => (SizeCategory.S, false),
            "16\"" or "16" => (SizeCategory.M, false),
            "20\"" or "20" => (SizeCategory.I, false),
            "24\"" or "24" => (SizeCategory.L, false),
            _ => (null, false),
        };
    }

    private static (SizeCategory? Mapped, bool Excluded) MapUsdaa(string cat)
    {
        return cat.Trim('"') switch
        {
            "12\"" or "12" => (SizeCategory.S, false),
            "16\"" or "16" => (SizeCategory.M, false),
            "22\"" or "22" => (SizeCategory.L, false),
            "26\"" or "26" => (SizeCategory.L, false),
            _ => (null, false),
        };
    }

    private static (SizeCategory? Mapped, bool Excluded) MapWao(string cat)
    {
        return cat switch
        {
            "250" => (SizeCategory.S, false),
            "300" => (SizeCategory.M, false),
            "400" => (SizeCategory.I, false),
            "500" => (SizeCategory.L, false),
            "600" => (SizeCategory.L, false),
            _ => (null, false),
        };
    }

    private static (SizeCategory? Mapped, bool Excluded) MapUki(string cat)
    {
        return cat.ToUpperInvariant() switch
        {
            "S" => (SizeCategory.S, false),
            "M" => (SizeCategory.M, false),
            "I" => (SizeCategory.I, false),
            "L" => (SizeCategory.L, false),
            _ => (null, false),
        };
    }

    private static (SizeCategory? Mapped, bool Excluded) MapIfcs(string cat)
    {
        return cat.ToUpperInvariant() switch
        {
            "S" => (SizeCategory.S, false),
            "M" => (SizeCategory.M, false),
            "L" => (SizeCategory.L, false),
            _ => (null, false),
        };
    }
}
