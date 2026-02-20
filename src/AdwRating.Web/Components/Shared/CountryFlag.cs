namespace AdwRating.Web.Components.Shared;

/// <summary>
/// Converts ISO alpha-3 country codes to flag emoji characters.
/// </summary>
public static class CountryFlag
{
    private static readonly Dictionary<string, string> Alpha3ToAlpha2 = new()
    {
        ["CZE"] = "CZ", ["GBR"] = "GB", ["DEU"] = "DE", ["FRA"] = "FR",
        ["AUT"] = "AT", ["POL"] = "PL", ["SWE"] = "SE", ["NOR"] = "NO",
        ["HUN"] = "HU", ["ESP"] = "ES", ["ITA"] = "IT", ["NLD"] = "NL",
        ["BEL"] = "BE", ["SVK"] = "SK", ["HRV"] = "HR", ["USA"] = "US",
        ["CAN"] = "CA", ["AUS"] = "AU", ["JPN"] = "JP", ["FIN"] = "FI",
        ["DNK"] = "DK", ["CHE"] = "CH", ["LUX"] = "LU", ["IRL"] = "IE",
        ["PRT"] = "PT", ["BRA"] = "BR", ["ROU"] = "RO", ["BGR"] = "BG",
        ["SRB"] = "RS", ["SVN"] = "SI", ["LTU"] = "LT", ["LVA"] = "LV",
        ["EST"] = "EE"
    };

    public static string ToEmoji(string? alpha3)
    {
        if (string.IsNullOrEmpty(alpha3)) return "";
        if (!Alpha3ToAlpha2.TryGetValue(alpha3.ToUpperInvariant(), out var a2)) return alpha3;
        return string.Concat(a2.Select(c => char.ConvertFromUtf32(0x1F1E6 + (c - 'A'))));
    }
}
