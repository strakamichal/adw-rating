using System.CommandLine;
using System.CommandLine.Parsing;
using AdwRating.Cli.Commands;
using NUnit.Framework;

namespace AdwRating.Tests.Cli;

[TestFixture]
public class CliCommandTests
{
    private Option<string?> _connectionOption = null!;
    private RootCommand _rootCommand = null!;

    [SetUp]
    public void SetUp()
    {
        _connectionOption = new Option<string?>("--connection")
        {
            Description = "SQL Server connection string",
            Required = true,
            Recursive = true
        };

        _rootCommand = new RootCommand("ADW Rating CLI");
        _rootCommand.Add(_connectionOption);
        _rootCommand.Add(ImportCommand.Create(_connectionOption));
        _rootCommand.Add(SeedConfigCommand.Create(_connectionOption));
        _rootCommand.Add(ListCommands.Create(_connectionOption));
        _rootCommand.Add(ShowCommands.Create(_connectionOption));
        _rootCommand.Add(MergeCommands.Create(_connectionOption));
        _rootCommand.Add(DeleteCommands.Create(_connectionOption));
        _rootCommand.Add(UpdateCommands.Create(_connectionOption));
        _rootCommand.Add(AliasCommands.Create(_connectionOption));
    }

    private ParseResult Parse(string commandLine)
    {
        return _rootCommand.Parse(commandLine);
    }

    [Test]
    public void CommandTree_CreatesWithoutErrors()
    {
        Assert.That(_rootCommand.Subcommands, Has.Count.GreaterThanOrEqualTo(8));
    }

    // LIST commands

    [Test]
    public void ListCompetitions_ParsesCorrectly()
    {
        var result = Parse("--connection test list competitions");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListHandlers_WithoutSearch_ParsesCorrectly()
    {
        var result = Parse("--connection test list handlers");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListHandlers_WithSearch_ParsesCorrectly()
    {
        var result = Parse("--connection test list handlers --search Smith");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListDogs_WithoutSearch_ParsesCorrectly()
    {
        var result = Parse("--connection test list dogs");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListDogs_WithSearch_ParsesCorrectly()
    {
        var result = Parse("--connection test list dogs --search Rex");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListImports_ParsesCorrectly()
    {
        var result = Parse("--connection test list imports");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListAliasesHandler_RequiresId()
    {
        var result = Parse("--connection test list aliases handler");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void ListAliasesHandler_WithId_ParsesCorrectly()
    {
        var result = Parse("--connection test list aliases handler 42");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ListAliasesDog_WithId_ParsesCorrectly()
    {
        var result = Parse("--connection test list aliases dog 42");
        Assert.That(result.Errors, Is.Empty);
    }

    // SHOW commands

    [Test]
    public void ShowHandler_RequiresId()
    {
        var result = Parse("--connection test show handler");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void ShowHandler_WithId_ParsesCorrectly()
    {
        var result = Parse("--connection test show handler 1");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void ShowDog_WithId_ParsesCorrectly()
    {
        var result = Parse("--connection test show dog 5");
        Assert.That(result.Errors, Is.Empty);
    }

    // MERGE commands

    [Test]
    public void MergeHandler_RequiresTwoIds()
    {
        var result = Parse("--connection test merge handler 1");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void MergeHandler_WithIds_ParsesCorrectly()
    {
        var result = Parse("--connection test merge handler 1 2");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void MergeHandler_WithDryRun_ParsesCorrectly()
    {
        var result = Parse("--connection test merge handler 1 2 --dry-run");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void MergeDog_WithIds_ParsesCorrectly()
    {
        var result = Parse("--connection test merge dog 10 20");
        Assert.That(result.Errors, Is.Empty);
    }

    // DELETE commands

    [Test]
    public void DeleteCompetition_RequiresId()
    {
        var result = Parse("--connection test delete competition");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void DeleteCompetition_WithId_ParsesCorrectly()
    {
        var result = Parse("--connection test delete competition 1");
        Assert.That(result.Errors, Is.Empty);
    }

    // UPDATE commands

    [Test]
    public void UpdateHandler_RequiresCountry()
    {
        var result = Parse("--connection test update handler 1");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void UpdateHandler_WithCountry_ParsesCorrectly()
    {
        var result = Parse("--connection test update handler 1 --country CZE");
        Assert.That(result.Errors, Is.Empty);
    }

    // ADD ALIAS commands

    [Test]
    public void AddAliasHandler_RequiresIdAndName()
    {
        var result = Parse("--connection test add alias handler");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void AddAliasHandler_WithArgs_ParsesCorrectly()
    {
        var result = Parse("--connection test add alias handler 1 JohnSmith");
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void AddAliasDog_RequiresType()
    {
        var result = Parse("--connection test add alias dog 1 Buddy");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void AddAliasDog_WithAllArgs_ParsesCorrectly()
    {
        var result = Parse("--connection test add alias dog 1 Buddy --type CallName");
        Assert.That(result.Errors, Is.Empty);
    }

    // IMPORT command

    [Test]
    public void Import_RequiresFileAndOptions()
    {
        var result = Parse("--connection test import");
        Assert.That(result.Errors, Is.Not.Empty);
    }

    [Test]
    public void Import_WithAllRequiredArgs_ParsesCorrectly()
    {
        var result = Parse("--connection test import test.csv --competition my-comp --name \"My Competition\" --date 2026-01-15 --tier 1");
        Assert.That(result.Errors, Is.Empty);
    }

    // SEED-CONFIG command

    [Test]
    public void SeedConfig_ParsesCorrectly()
    {
        var result = Parse("--connection test seed-config");
        Assert.That(result.Errors, Is.Empty);
    }

    // Global option

    [Test]
    public void Connection_IsRequired()
    {
        var result = Parse("list competitions");
        Assert.That(result.Errors, Is.Not.Empty);
    }
}
