using System.CommandLine;
using System.CommandLine.Parsing;

namespace AdwRating.Cli;

public static class ConnectionHelper
{
    public static string Resolve(ParseResult parseResult, Option<string?> connectionOption)
    {
        var value = parseResult.GetValue(connectionOption);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        var envValue = Environment.GetEnvironmentVariable("ADW_RATING_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envValue))
            return envValue;

        throw new InvalidOperationException(
            "Connection string is required. Use --connection or set ADW_RATING_CONNECTION environment variable.");
    }
}
