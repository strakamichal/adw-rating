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

        var normalized = NormalizeTypographicQuotes(name);
        normalized = normalized.Replace('-', ' ');
        normalized = normalized.Replace('`', '\'');
        normalized = ReorderLastFirst(normalized);
        normalized = StripDiacritics(normalized);
        normalized = normalized.ToLowerInvariant().Trim();
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = DeduplicateConsecutiveWords(normalized);

        return normalized;
    }

    private static string NormalizeTypographicQuotes(string text)
    {
        return text
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'');
    }

    /// <summary>
    /// Cleans a display name: reorders "Last, First" → "First Last" and collapses whitespace.
    /// Preserves original casing and diacritics (unlike Normalize which lowercases and strips).
    /// </summary>
    public static string CleanDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var cleaned = ReorderLastFirst(name.Trim());
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    private static string ReorderLastFirst(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 2)
            return text;

        var last = parts[0].Trim();
        var first = parts[1].Trim();
        return $"{first} {last}";
    }

    /// <summary>
    /// Extracts a call name from a dog name that may contain a registered name
    /// with the call name in parentheses or double quotes.
    /// Examples:
    ///   "Daylight Neverending Force (Day)" → callName="Day", registeredName="Daylight Neverending Force"
    ///   "Shadow of Aire Under Pressure ""Ninja""" → callName="Ninja", registeredName="Shadow of Aire Under Pressure"
    ///   "Day" → callName=null (no extraction possible)
    ///   "Let's Rock (FCI)" → callName=null (FCI is not a call name)
    /// </summary>
    public static (string? CallName, string? RegisteredName) ExtractCallName(string rawDogName)
    {
        if (string.IsNullOrWhiteSpace(rawDogName))
            return (null, null);

        var name = rawDogName.Trim();

        // Ignored parenthetical tokens (not call names)
        var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FCI", "FCI registration", "AKC", "KC", "UKC", "CKC",
        "cp",   // Italian: conduttore proprietario (handler is the owner)
        "None"  // No call name provided
        };

        // Try double-quoted call name: Registered Name ""CallName""
        var quoteMatch = Regex.Match(name, @"""([^""]+)""\s*$");
        if (quoteMatch.Success)
        {
            var candidate = quoteMatch.Groups[1].Value.Trim();
            if (!ignored.Contains(candidate) && candidate.Length >= 2)
            {
                var registered = name[..quoteMatch.Index].Trim().TrimEnd('"').Trim();
                return (candidate, registered.Length > 0 ? registered : null);
            }
        }

        // Try parenthesized call name: Registered Name (CallName)
        // Match the last well-formed parenthetical group
        var parenMatch = Regex.Match(name, @"\(([^()]+)\)\s*$");
        if (parenMatch.Success)
        {
            var candidate = parenMatch.Groups[1].Value.Trim();
            // Skip if it looks like a metadata tag rather than a name
            if (!ignored.Contains(candidate) && candidate.Length >= 2
                && !candidate.StartsWith("FCI", StringComparison.OrdinalIgnoreCase))
            {
                var registered = name[..parenMatch.Index].Trim();
                return (candidate, registered.Length > 0 ? registered : null);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Collapses consecutive duplicate words: "jessi jessi" → "jessi".
    /// Only removes exact consecutive repeats, not non-adjacent duplicates.
    /// </summary>
    private static string DeduplicateConsecutiveWords(string text)
    {
        var words = text.Split(' ');
        var result = new List<string>(words.Length) { words[0] };

        for (int i = 1; i < words.Length; i++)
        {
            if (!string.Equals(words[i], words[i - 1], StringComparison.Ordinal))
                result.Add(words[i]);
        }

        return string.Join(' ', result);
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
