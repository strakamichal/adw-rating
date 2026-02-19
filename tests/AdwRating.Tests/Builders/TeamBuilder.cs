using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;

namespace AdwRating.Tests.Builders;

public class TeamBuilder
{
    private int _id = 1;
    private int _handlerId = 1;
    private int _dogId = 1;
    private string _slug = "john-smith-rex";
    private float _mu = 25.0f;
    private float _sigma = 8.333f;
    private float _rating = 1500f;
    private float _prevMu;
    private float _prevSigma;
    private float _prevRating;
    private int _runCount;
    private int _finishedRunCount;
    private int _top3RunCount;
    private bool _isActive = true;
    private bool _isProvisional = true;
    private TierLabel? _tierLabel;
    private float _peakRating;

    public TeamBuilder WithId(int id) { _id = id; return this; }
    public TeamBuilder WithHandlerId(int handlerId) { _handlerId = handlerId; return this; }
    public TeamBuilder WithDogId(int dogId) { _dogId = dogId; return this; }
    public TeamBuilder WithSlug(string slug) { _slug = slug; return this; }
    public TeamBuilder WithMu(float mu) { _mu = mu; return this; }
    public TeamBuilder WithSigma(float sigma) { _sigma = sigma; return this; }
    public TeamBuilder WithRating(float rating) { _rating = rating; return this; }
    public TeamBuilder WithPrevMu(float prevMu) { _prevMu = prevMu; return this; }
    public TeamBuilder WithPrevSigma(float prevSigma) { _prevSigma = prevSigma; return this; }
    public TeamBuilder WithPrevRating(float prevRating) { _prevRating = prevRating; return this; }
    public TeamBuilder WithRunCount(int runCount) { _runCount = runCount; return this; }
    public TeamBuilder WithFinishedRunCount(int finishedRunCount) { _finishedRunCount = finishedRunCount; return this; }
    public TeamBuilder WithTop3RunCount(int top3RunCount) { _top3RunCount = top3RunCount; return this; }
    public TeamBuilder WithIsActive(bool isActive) { _isActive = isActive; return this; }
    public TeamBuilder WithIsProvisional(bool isProvisional) { _isProvisional = isProvisional; return this; }
    public TeamBuilder WithTierLabel(TierLabel? tierLabel) { _tierLabel = tierLabel; return this; }
    public TeamBuilder WithPeakRating(float peakRating) { _peakRating = peakRating; return this; }

    public Team Build() => new()
    {
        Id = _id,
        HandlerId = _handlerId,
        DogId = _dogId,
        Slug = _slug,
        Mu = _mu,
        Sigma = _sigma,
        Rating = _rating,
        PrevMu = _prevMu,
        PrevSigma = _prevSigma,
        PrevRating = _prevRating,
        RunCount = _runCount,
        FinishedRunCount = _finishedRunCount,
        Top3RunCount = _top3RunCount,
        IsActive = _isActive,
        IsProvisional = _isProvisional,
        TierLabel = _tierLabel,
        PeakRating = _peakRating,
    };
}
