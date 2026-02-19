using AdwRating.Service.Import;

namespace AdwRating.Tests.Import;

[TestFixture]
public class CsvResultParserTests
{
    private const string Header =
        "round_key,date,size_category,discipline,is_team_round,handler_name,handler_country,dog_call_name,rank,eliminated";

    private static Stream CreateCsvStream(string csvContent)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csvContent));
    }

    [Test]
    public void ValidCsv_ParsesAllFieldsCorrectly()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,1,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));

        var row = rows[0];
        Assert.That(row.RoundKey, Is.EqualTo("R1"));
        Assert.That(row.Date, Is.EqualTo("2025-01-15"));
        Assert.That(row.SizeCategory, Is.EqualTo("S"));
        Assert.That(row.Discipline, Is.EqualTo("Agility"));
        Assert.That(row.IsTeamRound, Is.EqualTo("false"));
        Assert.That(row.HandlerName, Is.EqualTo("John Smith"));
        Assert.That(row.HandlerCountry, Is.EqualTo("CZ"));
        Assert.That(row.DogCallName, Is.EqualTo("Rex"));
        Assert.That(row.Rank, Is.EqualTo("1"));
        Assert.That(row.Eliminated, Is.EqualTo("false"));
    }

    [Test]
    public void BomHandling_ParsesCorrectly()
    {
        var bom = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetPreamble());
        var csv = $"""
            {bom}{Header}
            R1,2025-01-15,M,Jumping,false,Jane Doe,SK,Buddy,2,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].RoundKey, Is.EqualTo("R1"));
    }

    [Test]
    public void EmptyRows_AreIgnored()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,1,false


            R2,2025-01-15,M,Jumping,false,Jane Doe,SK,Buddy,2,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(2));
    }

    [Test]
    public void QuotedFieldsWithCommas_ParseCorrectly()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,"Smith, John",CZ,Rex,1,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].HandlerName, Is.EqualTo("Smith, John"));
    }

    [Test]
    public void MissingRequiredField_HandlerName_ReturnsError()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,,CZ,Rex,1,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(errors.Any(e => e.Contains("handler_name")), Is.True);
    }

    [Test]
    public void InvalidRank_WhenNotEliminated_ReturnsError()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,abc,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(errors.Any(e => e.Contains("rank")), Is.True);
    }

    [Test]
    public void InvalidDiscipline_ReturnsError()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Obedience,false,John Smith,CZ,Rex,1,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(errors.Any(e => e.Contains("discipline") && e.Contains("Obedience")), Is.True);
    }

    [Test]
    public void DuplicateHandlerDogInSameRound_ReturnsError()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,1,false
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,2,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(errors.Any(e => e.Contains("duplicate")), Is.True);
    }

    [Test]
    public void CaseInsensitiveHeaders_ParseCorrectly()
    {
        var csv = """
            Round_Key,Date,Size_Category,Discipline,Is_Team_Round,Handler_Name,Handler_Country,Dog_Call_Name,Rank,Eliminated
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,1,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].HandlerName, Is.EqualTo("John Smith"));
    }

    [Test]
    public void MultipleValidationErrors_CollectedAtOnce()
    {
        var csv = $"""
            {Header}
            ,2025-01-15,,InvalidDisc,false,,CZ,,abc,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        // Should have errors for: round_key, handler_name, dog_call_name, size_category, discipline (invalid), rank (invalid)
        Assert.That(errors, Has.Count.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void EliminatedTrue_WithNoRank_IsValid()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,,true
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void EliminatedFalse_WithRank_IsValid()
    {
        var csv = $"""
            {Header}
            R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,3,false
            """;

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Rank, Is.EqualTo("3"));
    }

    [Test]
    public void AllOptionalFieldsPopulated_ParsesCorrectly()
    {
        var csv =
            "round_key,date,size_category,discipline,is_team_round,handler_name,handler_country,dog_call_name,rank,eliminated,dog_registered_name,dog_breed,faults,refusals,time_faults,total_faults,time,speed,judge,sct,mct,course_length,start_no,run_number\n" +
            "R1,2025-01-15,S,Agility,false,John Smith,CZ,Rex,1,false,Rex von Castle,Border Collie,0,0,0.5,0.5,35.21,4.5,J. Judge,40,60,160,5,1";

        var (rows, errors) = CsvResultParser.Parse(CreateCsvStream(csv));

        Assert.That(errors, Is.Empty);
        Assert.That(rows, Has.Count.EqualTo(1));

        var row = rows[0];
        Assert.That(row.DogRegisteredName, Is.EqualTo("Rex von Castle"));
        Assert.That(row.DogBreed, Is.EqualTo("Border Collie"));
        Assert.That(row.Faults, Is.EqualTo("0"));
        Assert.That(row.Refusals, Is.EqualTo("0"));
        Assert.That(row.TimeFaults, Is.EqualTo("0.5"));
        Assert.That(row.TotalFaults, Is.EqualTo("0.5"));
        Assert.That(row.Time, Is.EqualTo("35.21"));
        Assert.That(row.Speed, Is.EqualTo("4.5"));
        Assert.That(row.Judge, Is.EqualTo("J. Judge"));
        Assert.That(row.Sct, Is.EqualTo("40"));
        Assert.That(row.Mct, Is.EqualTo("60"));
        Assert.That(row.CourseLength, Is.EqualTo("160"));
        Assert.That(row.StartNo, Is.EqualTo("5"));
        Assert.That(row.RunNumber, Is.EqualTo("1"));
    }
}
