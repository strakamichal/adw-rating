using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AdwRating.Domain.Helpers;

public static class NameNormalizer
{
    // Characters that don't decompose via Unicode normalization
    private static readonly Dictionary<char, string> SpecialMappings = new()
    {
        ['Ł'] = "L", ['ł'] = "l",
        ['Đ'] = "D", ['đ'] = "d",
        ['Ø'] = "O", ['ø'] = "o",
        ['Ħ'] = "H", ['ħ'] = "h",
        ['Ŧ'] = "T", ['ŧ'] = "t",
        ['ß'] = "ss",
    };

    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var normalized = StripDiacritics(name);
        normalized = normalized.ToLowerInvariant().Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return normalized;
    }

    private static string StripDiacritics(string text)
    {
        var sb = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (SpecialMappings.TryGetValue(c, out var replacement))
            {
                sb.Append(replacement);
            }
            else
            {
                sb.Append(c);
            }
        }

        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        sb.Clear();

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
