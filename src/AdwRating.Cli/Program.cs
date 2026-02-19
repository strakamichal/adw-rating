using System.CommandLine;
using AdwRating.Cli.Commands;

var connectionOption = new Option<string?>("--connection")
{
    Description = "SQL Server connection string (or set ADW_RATING_CONNECTION env var)",
    Recursive = true,
    HelpName = "connection-string"
};

var rootCommand = new RootCommand("ADW Rating CLI");
rootCommand.Add(connectionOption);

rootCommand.Add(ImportCommand.Create(connectionOption));
rootCommand.Add(SeedConfigCommand.Create(connectionOption));
rootCommand.Add(ListCommands.Create(connectionOption));
rootCommand.Add(ShowCommands.Create(connectionOption));
rootCommand.Add(MergeCommands.Create(connectionOption));
rootCommand.Add(DeleteCommands.Create(connectionOption));
rootCommand.Add(UpdateCommands.Create(connectionOption));
rootCommand.Add(AliasCommands.Create(connectionOption));

return await rootCommand.Parse(args).InvokeAsync();
