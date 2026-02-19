using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.Tests.Builders;

public class RunBuilder
{
    private int _id = 1;
    private int _competitionId = 1;
    private DateOnly _date = new(2024, 10, 3);
    private int _runNumber = 1;
    private string _roundKey = "ind_agility_large_1";
    private SizeCategory _sizeCategory = SizeCategory.L;
    private Discipline _discipline = Discipline.Agility;
    private bool _isTeamRound;
    private string? _judge;
    private float? _sct;
    private float? _mct;
    private float? _courseLength;
    private string? _originalSizeCategory;

    public RunBuilder WithId(int id) { _id = id; return this; }
    public RunBuilder WithCompetitionId(int competitionId) { _competitionId = competitionId; return this; }
    public RunBuilder WithDate(DateOnly date) { _date = date; return this; }
    public RunBuilder WithRunNumber(int runNumber) { _runNumber = runNumber; return this; }
    public RunBuilder WithRoundKey(string roundKey) { _roundKey = roundKey; return this; }
    public RunBuilder WithSizeCategory(SizeCategory sizeCategory) { _sizeCategory = sizeCategory; return this; }
    public RunBuilder WithDiscipline(Discipline discipline) { _discipline = discipline; return this; }
    public RunBuilder WithIsTeamRound(bool isTeamRound) { _isTeamRound = isTeamRound; return this; }
    public RunBuilder WithJudge(string? judge) { _judge = judge; return this; }
    public RunBuilder WithSct(float? sct) { _sct = sct; return this; }
    public RunBuilder WithMct(float? mct) { _mct = mct; return this; }
    public RunBuilder WithCourseLength(float? courseLength) { _courseLength = courseLength; return this; }
    public RunBuilder WithOriginalSizeCategory(string? originalSizeCategory) { _originalSizeCategory = originalSizeCategory; return this; }

    public Run Build() => new()
    {
        Id = _id,
        CompetitionId = _competitionId,
        Date = _date,
        RunNumber = _runNumber,
        RoundKey = _roundKey,
        SizeCategory = _sizeCategory,
        Discipline = _discipline,
        IsTeamRound = _isTeamRound,
        Judge = _judge,
        Sct = _sct,
        Mct = _mct,
        CourseLength = _courseLength,
        OriginalSizeCategory = _originalSizeCategory,
    };
}
