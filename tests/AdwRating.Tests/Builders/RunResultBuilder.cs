using AdwRating.Domain.Entities;

namespace AdwRating.Tests.Builders;

public class RunResultBuilder
{
    private int _id = 1;
    private int _runId = 1;
    private int _teamId = 1;
    private int? _rank = 1;
    private int? _faults;
    private int? _refusals;
    private float? _timeFaults;
    private float? _totalFaults;
    private float? _time;
    private float? _speed;
    private bool _eliminated;
    private int? _startNo;

    public RunResultBuilder WithId(int id) { _id = id; return this; }
    public RunResultBuilder WithRunId(int runId) { _runId = runId; return this; }
    public RunResultBuilder WithTeamId(int teamId) { _teamId = teamId; return this; }
    public RunResultBuilder WithRank(int? rank) { _rank = rank; return this; }
    public RunResultBuilder WithFaults(int? faults) { _faults = faults; return this; }
    public RunResultBuilder WithRefusals(int? refusals) { _refusals = refusals; return this; }
    public RunResultBuilder WithTimeFaults(float? timeFaults) { _timeFaults = timeFaults; return this; }
    public RunResultBuilder WithTotalFaults(float? totalFaults) { _totalFaults = totalFaults; return this; }
    public RunResultBuilder WithTime(float? time) { _time = time; return this; }
    public RunResultBuilder WithSpeed(float? speed) { _speed = speed; return this; }
    public RunResultBuilder WithEliminated(bool eliminated) { _eliminated = eliminated; return this; }
    public RunResultBuilder WithStartNo(int? startNo) { _startNo = startNo; return this; }

    public RunResult Build() => new()
    {
        Id = _id,
        RunId = _runId,
        TeamId = _teamId,
        Rank = _rank,
        Faults = _faults,
        Refusals = _refusals,
        TimeFaults = _timeFaults,
        TotalFaults = _totalFaults,
        Time = _time,
        Speed = _speed,
        Eliminated = _eliminated,
        StartNo = _startNo,
    };
}
