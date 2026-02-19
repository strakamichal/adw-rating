using System.Text.RegularExpressions;

namespace AdwRating.Domain.Helpers;

public static class SlugHelper
{
    public static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var slug = input.ToLowerInvariant();
        slug = slug.Replace(' ', '-');
        slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        slug = slug.Trim('-');

        return slug;
    }

    public static string AppendSuffix(string slug, int suffix)
    {
        return $"{slug}-{suffix}";
    }
}
