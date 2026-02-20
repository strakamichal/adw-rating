using System.CommandLine;
using AdwRating.Cli;
using AdwRating.Data.Mssql;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdwRating.Cli.Commands;

public static class SeedConfigCommand
{
    public static Command Create(Option<string?> connectionOption)
    {
        var command = new Command("seed-config", "Create default rating configuration if none exists");

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var connectionString = ConnectionHelper.Resolve(parseResult, connectionOption);

            var services = new ServiceCollection();
            services.AddDataMssql(connectionString);
            services.AddLogging(builder => builder.AddConsole());

            await using var provider = services.BuildServiceProvider();

            // Ensure database and schema exist
            var dbInit = provider.GetRequiredService<IDatabaseInitializer>();
            await dbInit.EnsureCreatedAsync();

            var configRepo = provider.GetRequiredService<IRatingConfigurationRepository>();

            try
            {
                await configRepo.GetActiveAsync();
                Console.WriteLine("Active rating configuration already exists. Skipping seed.");
                return 0;
            }
            catch
            {
                // No active config found — proceed to create
            }

            await configRepo.CreateAsync(new RatingConfiguration
            {
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Mu0 = 25.0f,
                Sigma0 = 8.333f,
                LiveWindowDays = 730,
                MinRunsForLiveRanking = 5,
                MinFieldSize = 6,
                MajorEventWeight = 1.2f,
                SigmaDecay = 0.99f,
                SigmaMin = 1.5f,
                DisplayBase = 1000f,
                DisplayScale = 40f,
                RatingSigmaMultiplier = 1.0f,
                PodiumBoostBase = 0.85f,
                PodiumBoostRange = 0.20f,
                PodiumBoostTarget = 0.50f,
                ProvisionalSigmaThreshold = 7.8f,
                NormTargetMean = 1500f,
                NormTargetStd = 150f,
                EliteTopPercent = 0.02f,
                ChampionTopPercent = 0.10f,
                ExpertTopPercent = 0.30f,
                CountryTopN = 10,
                MinTeamsForCountryRanking = 3
            });

            Console.WriteLine("Default rating configuration created successfully.");
            return 0;
        });

        return command;
    }
}
