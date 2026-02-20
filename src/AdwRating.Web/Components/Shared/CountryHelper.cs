namespace AdwRating.Web.Components.Shared;

/// <summary>
/// Maps ISO 3166-1 alpha-3 country codes to flag emojis using regional indicator symbols.
/// Each letter A-Z maps to U+1F1E6 to U+1F1FF.
/// </summary>
public static class CountryHelper
{
    private static readonly Dictionary<string, string> Alpha3ToAlpha2 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CZE"] = "CZ",
        ["GBR"] = "GB",
        ["DEU"] = "DE",
        ["FRA"] = "FR",
        ["AUT"] = "AT",
        ["POL"] = "PL",
        ["SWE"] = "SE",
        ["NOR"] = "NO",
        ["HUN"] = "HU",
        ["ESP"] = "ES",
        ["ITA"] = "IT",
        ["NLD"] = "NL",
        ["BEL"] = "BE",
        ["SVK"] = "SK",
        ["HRV"] = "HR",
        ["USA"] = "US",
        ["CAN"] = "CA",
        ["AUS"] = "AU",
        ["JPN"] = "JP",
        ["FIN"] = "FI",
        ["DNK"] = "DK",
        ["CHE"] = "CH",
        ["LUX"] = "LU",
        ["SVN"] = "SI",
        ["ROU"] = "RO",
        ["PRT"] = "PT",
        ["IRL"] = "IE",
        ["BRA"] = "BR",
        ["MEX"] = "MX",
        ["ISR"] = "IL",
        ["RUS"] = "RU",
        ["UKR"] = "UA",
        ["BGR"] = "BG",
        ["SRB"] = "RS",
        ["LTU"] = "LT",
        ["LVA"] = "LV",
        ["EST"] = "EE",
    };

    /// <summary>
    /// Returns a flag emoji for the given ISO 3166-1 alpha-3 country code.
    /// Returns the code itself if no mapping is found.
    /// </summary>
    public static string GetFlag(string? countryCode)
    {
        if (string.IsNullOrEmpty(countryCode))
            return "";

        string alpha2;
        if (countryCode.Length == 3)
        {
            if (!Alpha3ToAlpha2.TryGetValue(countryCode, out var mapped))
                return countryCode;
            alpha2 = mapped;
        }
        else if (countryCode.Length == 2)
        {
            alpha2 = countryCode.ToUpperInvariant();
        }
        else
        {
            return countryCode;
        }

        // Regional indicator symbol letters: U+1F1E6 (A) to U+1F1FF (Z)
        return string.Concat(
            alpha2.ToUpperInvariant().Select(c => char.ConvertFromUtf32(0x1F1E6 + (c - 'A')))
        );
    }
}
