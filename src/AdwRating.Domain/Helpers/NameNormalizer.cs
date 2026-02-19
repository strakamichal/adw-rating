using System.Text.RegularExpressions;

namespace AdwRating.Domain.Helpers;

public static class NameNormalizer
{
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = name.ToLowerInvariant().Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized;
    }
}
