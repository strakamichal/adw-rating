using System.CommandLine;
using AdwRating.Cli.Commands;

var connectionOption = new Option<string?>("--connection")
{
    Description = "SQL Server connection string (or set ADW_RATING_CONNECTION env var)",
    Recursive = true,
    HelpName = "connection-string"
};

var verboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "Show detailed EF Core / SQL logging",
    Recursive = true
};

var rootCommand = new RootCommand("ADW Rating CLI");
rootCommand.Add(connectionOption);
rootCommand.Add(verboseOption);

rootCommand.Add(ImportCommand.Create(connectionOption, verboseOption));
rootCommand.Add(SeedConfigCommand.Create(connectionOption, verboseOption));
rootCommand.Add(ListCommands.Create(connectionOption, verboseOption));
rootCommand.Add(ShowCommands.Create(connectionOption, verboseOption));
rootCommand.Add(MergeCommands.Create(connectionOption, verboseOption));
rootCommand.Add(DeleteCommands.Create(connectionOption, verboseOption));
rootCommand.Add(UpdateCommands.Create(connectionOption, verboseOption));
rootCommand.Add(AliasCommands.Create(connectionOption, verboseOption));
rootCommand.Add(RecalculateCommand.Create(connectionOption, verboseOption));

return await rootCommand.Parse(args).InvokeAsync();
