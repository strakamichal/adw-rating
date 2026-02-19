using System.Text;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using AdwRating.Domain.Models;
using AdwRating.Service;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AdwRating.Tests.Services;

[TestFixture]
public class ImportServiceTests
{
    private ICompetitionRepository _competitionRepo = null!;
    private IRunRepository _runRepo = null!;
    private IRunResultRepository _runResultRepo = null!;
    private IImportLogRepository _importLogRepo = null!;
    private IIdentityResolutionService _identityService = null!;
    private ILogger<ImportService> _logger = null!;
    private ImportService _sut = null!;

    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _competitionRepo = Substitute.For<ICompetitionRepository>();
        _runRepo = Substitute.For<IRunRepository>();
        _runResultRepo = Substitute.For<IRunResultRepository>();
        _importLogRepo = Substitute.For<IImportLogRepository>();
        _identityService = Substitute.For<IIdentityResolutionService>();
        _logger = Substitute.For<ILogger<ImportService>>();

        _sut = new ImportService(
            _competitionRepo,
            _runRepo,
            _runResultRepo,
            _importLogRepo,
            _identityService,
            _logger);

        _tempDir = Path.Combine(Path.GetTempPath(), $"import-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WriteCsv(string content)
    {
        var path = Path.Combine(_tempDir, "test.csv");
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static CompetitionMetadata DefaultMetadata => new(
        "Test Competition",
        new DateOnly(2025, 6, 15),
        null,
        "CZE",
        "Prague",
        1,
        "FCI");

    private static string ValidCsvHeader =>
        "round_key,date,size_category,discipline,is_team_round,handler_name,handler_country,dog_call_name,rank,eliminated,dog_breed,faults,refusals,time_faults,total_faults,time,speed,judge,sct,mct,course_length,start_no,run_number";

    private static string ValidCsvRow(
        string roundKey = "R1-L-Agility",
        string handlerName = "John Smith",
        string country = "CZE",
        string dogName = "Rex",
        string rank = "1",
        string eliminated = "false",
        string size = "L",
        string discipline = "Agility") =>
        $"{roundKey},2025-06-15,{size},{discipline},false,{handlerName},{country},{dogName},{rank},{eliminated},Border Collie,0,0,0,0,35.5,4.2,Judge A,45,60,150,1,1";

    [Test]
    public async Task ImportCompetitionAsync_ValidCsv_ReturnsSuccess()
    {
        var csv = $"{ValidCsvHeader}\n{ValidCsvRow()}\n{ValidCsvRow(handlerName: "Jane Doe", dogName: "Buddy", rank: "2")}";
        var filePath = WriteCsv(csv);

        _competitionRepo.GetBySlugAsync("test-comp").Returns((Competition?)null);
        _competitionRepo.CreateAsync(Arg.Any<Competition>())
            .Returns(ci => { ci.Arg<Competition>().Id = 1; return ci.Arg<Competition>(); });

        var handler1 = new Handler { Id = 10, Name = "John Smith", NormalizedName = "john smith", Country = "CZE", Slug = "john-smith" };
        var handler2 = new Handler { Id = 11, Name = "Jane Doe", NormalizedName = "jane doe", Country = "CZE", Slug = "jane-doe" };
        var dog1 = new Dog { Id = 20, CallName = "Rex", NormalizedCallName = "rex", SizeCategory = SizeCategory.L };
        var dog2 = new Dog { Id = 21, CallName = "Buddy", NormalizedCallName = "buddy", SizeCategory = SizeCategory.L };
        var team1 = new Team { Id = 30, HandlerId = 10, DogId = 20 };
        var team2 = new Team { Id = 31, HandlerId = 11, DogId = 21 };

        _identityService.ResolveHandlerAsync("John Smith", "CZE").Returns(handler1);
        _identityService.ResolveHandlerAsync("Jane Doe", "CZE").Returns(handler2);
        _identityService.ResolveDogAsync("Rex", "Border Collie", SizeCategory.L).Returns(dog1);
        _identityService.ResolveDogAsync("Buddy", "Border Collie", SizeCategory.L).Returns(dog2);
        _identityService.ResolveTeamAsync(10, 20).Returns(team1);
        _identityService.ResolveTeamAsync(11, 21).Returns(team2);

        var result = await _sut.ImportCompetitionAsync(filePath, "test-comp", DefaultMetadata);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.RowCount, Is.EqualTo(2));
            Assert.That(result.NewHandlers, Is.EqualTo(2));
            Assert.That(result.NewDogs, Is.EqualTo(2));
            Assert.That(result.NewTeams, Is.EqualTo(2));
            Assert.That(result.Errors, Is.Empty);
        });

        await _competitionRepo.Received(1).CreateAsync(Arg.Any<Competition>());
        await _runRepo.Received(1).CreateBatchAsync(Arg.Any<IEnumerable<Run>>());
        await _runResultRepo.Received(1).CreateBatchAsync(Arg.Any<IEnumerable<RunResult>>());
        await _importLogRepo.Received(1).CreateAsync(Arg.Is<ImportLog>(l =>
            l.Status == ImportStatus.Success && l.RowCount == 2));
    }

    [Test]
    public async Task ImportCompetitionAsync_CsvValidationErrors_ReturnsFailure()
    {
        // Row with round_key present but other required fields missing triggers validation errors
        var csv = $"{ValidCsvHeader}\nR1-L-Agility,,,,,,,,,,,,,,,,,,,,,,";
        var filePath = WriteCsv(csv);

        var result = await _sut.ImportCompetitionAsync(filePath, "test-comp", DefaultMetadata);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Is.Not.Empty);
        });

        await _importLogRepo.Received(1).CreateAsync(Arg.Is<ImportLog>(l =>
            l.Status == ImportStatus.Rejected));
        await _competitionRepo.DidNotReceive().CreateAsync(Arg.Any<Competition>());
    }

    [Test]
    public async Task ImportCompetitionAsync_DuplicateSlug_ReturnsFailure()
    {
        var csv = $"{ValidCsvHeader}\n{ValidCsvRow()}";
        var filePath = WriteCsv(csv);

        _competitionRepo.GetBySlugAsync("existing-comp")
            .Returns(new Competition { Id = 99, Slug = "existing-comp", Name = "Existing" });

        var result = await _sut.ImportCompetitionAsync(filePath, "existing-comp", DefaultMetadata);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0], Does.Contain("already exists"));
        });

        await _competitionRepo.DidNotReceive().CreateAsync(Arg.Any<Competition>());
    }

    [Test]
    public async Task ImportCompetitionAsync_ExcludedSize_SkipsWithWarning()
    {
        // AKC Preferred size should be excluded
        var metadata = DefaultMetadata with { Organization = "AKC" };
        // Use quoted CSV field to handle the embedded quote in 8" Preferred
        var csv = ValidCsvHeader + "\n" +
                  "R1-L-Agility,2025-06-15,\"8\"\" Preferred\",Agility,false,John Smith,CZE,Rex,1,false,Border Collie,0,0,0,0,35.5,4.2,Judge A,45,60,150,1,1";
        var filePath = WriteCsv(csv);

        _competitionRepo.GetBySlugAsync("test-comp").Returns((Competition?)null);
        _competitionRepo.CreateAsync(Arg.Any<Competition>())
            .Returns(ci => { ci.Arg<Competition>().Id = 1; return ci.Arg<Competition>(); });

        var result = await _sut.ImportCompetitionAsync(filePath, "test-comp", metadata);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings, Is.Not.Empty);
            Assert.That(result.Warnings[0], Does.Contain("excluded"));
        });

        // No identity resolution should have been called since all rows were skipped
        await _identityService.DidNotReceive().ResolveHandlerAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task ImportCompetitionAsync_SameHandlerMultipleRows_ResolvesOnce()
    {
        var csv = $"{ValidCsvHeader}\n" +
                  $"{ValidCsvRow(roundKey: "R1-L-Agility", handlerName: "John Smith", dogName: "Rex", rank: "1")}\n" +
                  $"{ValidCsvRow(roundKey: "R1-L-Agility", handlerName: "John Smith", dogName: "Buddy", rank: "2")}";
        var filePath = WriteCsv(csv);

        _competitionRepo.GetBySlugAsync("test-comp").Returns((Competition?)null);
        _competitionRepo.CreateAsync(Arg.Any<Competition>())
            .Returns(ci => { ci.Arg<Competition>().Id = 1; return ci.Arg<Competition>(); });

        var handler = new Handler { Id = 10, Name = "John Smith", NormalizedName = "john smith", Country = "CZE", Slug = "john-smith" };
        var dog1 = new Dog { Id = 20, CallName = "Rex", NormalizedCallName = "rex", SizeCategory = SizeCategory.L };
        var dog2 = new Dog { Id = 21, CallName = "Buddy", NormalizedCallName = "buddy", SizeCategory = SizeCategory.L };
        var team1 = new Team { Id = 30, HandlerId = 10, DogId = 20 };
        var team2 = new Team { Id = 31, HandlerId = 10, DogId = 21 };

        _identityService.ResolveHandlerAsync("John Smith", "CZE").Returns(handler);
        _identityService.ResolveDogAsync("Rex", "Border Collie", SizeCategory.L).Returns(dog1);
        _identityService.ResolveDogAsync("Buddy", "Border Collie", SizeCategory.L).Returns(dog2);
        _identityService.ResolveTeamAsync(10, 20).Returns(team1);
        _identityService.ResolveTeamAsync(10, 21).Returns(team2);

        var result = await _sut.ImportCompetitionAsync(filePath, "test-comp", DefaultMetadata);

        Assert.That(result.Success, Is.True);

        // Handler should only be resolved once due to caching
        await _identityService.Received(1).ResolveHandlerAsync("John Smith", "CZE");
        // But dogs should be resolved separately
        await _identityService.Received(1).ResolveDogAsync("Rex", Arg.Any<string?>(), SizeCategory.L);
        await _identityService.Received(1).ResolveDogAsync("Buddy", Arg.Any<string?>(), SizeCategory.L);
    }

    [Test]
    public async Task ImportCompetitionAsync_EliminatedRow_SetsFieldsCorrectly()
    {
        var csv = $"{ValidCsvHeader}\n{ValidCsvRow(rank: "", eliminated: "true")}";
        var filePath = WriteCsv(csv);

        _competitionRepo.GetBySlugAsync("test-comp").Returns((Competition?)null);
        _competitionRepo.CreateAsync(Arg.Any<Competition>())
            .Returns(ci => { ci.Arg<Competition>().Id = 1; return ci.Arg<Competition>(); });

        var handler = new Handler { Id = 10, Name = "John Smith", NormalizedName = "john smith", Country = "CZE", Slug = "john-smith" };
        var dog = new Dog { Id = 20, CallName = "Rex", NormalizedCallName = "rex", SizeCategory = SizeCategory.L };
        var team = new Team { Id = 30, HandlerId = 10, DogId = 20 };

        _identityService.ResolveHandlerAsync("John Smith", "CZE").Returns(handler);
        _identityService.ResolveDogAsync("Rex", "Border Collie", SizeCategory.L).Returns(dog);
        _identityService.ResolveTeamAsync(10, 20).Returns(team);

        var result = await _sut.ImportCompetitionAsync(filePath, "test-comp", DefaultMetadata);

        Assert.That(result.Success, Is.True);

        await _runResultRepo.Received(1).CreateBatchAsync(Arg.Is<IEnumerable<RunResult>>(results =>
            results.Any(r => r.Eliminated && r.Rank == null)));
    }
}
