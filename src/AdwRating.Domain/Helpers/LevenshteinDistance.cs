namespace AdwRating.Domain.Helpers;

public static class LevenshteinDistance
{
    public static int Compute(string a, string b)
    {
        a ??= string.Empty;
        b ??= string.Empty;

        var m = a.Length;
        var n = b.Length;

        var d = new int[m + 1, n + 1];

        for (var i = 0; i <= m; i++)
            d[i, 0] = i;

        for (var j = 0; j <= n; j++)
            d[0, j] = j;

        for (var i = 1; i <= m; i++)
        {
            for (var j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[m, n];
    }
}
