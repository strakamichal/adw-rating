using System.CommandLine;
using AdwRating.Cli;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using AdwRating.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class ImportCommand
{
    public static Command Create(Option<string?> connectionOption)
    {
        var fileArgument = new Argument<FileInfo>("file") { Description = "Path to CSV file" };
        var competitionOption = new Option<string>("--competition") { Description = "Competition slug", Required = true };
        var nameOption = new Option<string>("--name") { Description = "Competition name", Required = true };
        var dateOption = new Option<DateOnly>("--date") { Description = "Competition start date (YYYY-MM-DD)", Required = true };
        var tierOption = new Option<int>("--tier") { Description = "Competition tier (1 or 2)", Required = true };
        var countryOption = new Option<string?>("--country") { Description = "Host country (ISO 3166-1 alpha-3)" };
        var locationOption = new Option<string?>("--location") { Description = "City or venue" };
        var endDateOption = new Option<DateOnly?>("--end-date") { Description = "End date (YYYY-MM-DD)" };
        var organizationOption = new Option<string?>("--organization") { Description = "Governing body (FCI, AKC, USDAA, etc.)" };

        var command = new Command("import", "Import competition results from CSV");
        command.Add(fileArgument);
        command.Add(competitionOption);
        command.Add(nameOption);
        command.Add(dateOption);
        command.Add(tierOption);
        command.Add(countryOption);
        command.Add(locationOption);
        command.Add(endDateOption);
        command.Add(organizationOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetValue(fileArgument);
            var connectionString = ConnectionHelper.Resolve(parseResult, connectionOption);
            var slug = parseResult.GetValue(competitionOption)!;
            var name = parseResult.GetValue(nameOption)!;
            var date = parseResult.GetValue(dateOption);
            var tier = parseResult.GetValue(tierOption);
            var country = parseResult.GetValue(countryOption);
            var location = parseResult.GetValue(locationOption);
            var endDate = parseResult.GetValue(endDateOption);
            var organization = parseResult.GetValue(organizationOption);

            if (file is null || !file.Exists)
            {
                Console.Error.WriteLine($"File not found: {file?.FullName ?? "(null)"}");
                return 1;
            }

            var services = new ServiceCollection();
            services.AddDataMssql(connectionString);
            services.AddServices();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            await using var provider = services.BuildServiceProvider();
            var importService = provider.GetRequiredService<IImportService>();

            var metadata = new CompetitionMetadata(name, date, endDate, country, location, tier, organization);
            var result = await importService.ImportCompetitionAsync(file.FullName, slug, metadata);

            if (result.Success)
            {
                Console.WriteLine("Import successful!");
                Console.WriteLine($"  Rows processed: {result.RowCount}");
                Console.WriteLine($"  Handlers resolved: {result.NewHandlers}");
                Console.WriteLine($"  Dogs resolved: {result.NewDogs}");
                Console.WriteLine($"  Teams resolved: {result.NewTeams}");
                if (result.Warnings.Count > 0)
                {
                    Console.WriteLine("  Warnings:");
                    foreach (var w in result.Warnings)
                        Console.WriteLine($"    - {w}");
                }
                return 0;
            }
            else
            {
                Console.Error.WriteLine("Import failed:");
                foreach (var error in result.Errors)
                    Console.Error.WriteLine($"  - {error}");
                return 1;
            }
        });

        return command;
    }
}
