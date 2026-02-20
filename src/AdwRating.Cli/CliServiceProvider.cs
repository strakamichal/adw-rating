using System.CommandLine;
using System.CommandLine.Parsing;
using AdwRating.Data.Mssql;
using AdwRating.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli;

public static class CliServiceProvider
{
    public static ServiceProvider Build(
        ParseResult parseResult,
        Option<string?> connectionOption,
        Option<bool> verboseOption,
        bool addServices = false)
    {
        var connectionString = ConnectionHelper.Resolve(parseResult, connectionOption);
        var verbose = parseResult.GetValue(verboseOption);

        var services = new ServiceCollection();
        services.AddDataMssql(connectionString);

        if (addServices)
            services.AddServices();

        var minLevel = verbose ? LogLevel.Information : LogLevel.Warning;
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(minLevel));

        return services.BuildServiceProvider();
    }
}
