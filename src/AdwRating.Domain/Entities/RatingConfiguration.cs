namespace AdwRating.Domain.Entities;

public class RatingConfiguration
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public float Mu0 { get; set; }
    public float Sigma0 { get; set; }
    public int LiveWindowDays { get; set; }
    public int MinRunsForLiveRanking { get; set; }
    public int MinFieldSize { get; set; }
    public float MajorEventWeight { get; set; }
    public float SigmaDecay { get; set; }
    public float SigmaMin { get; set; }
    public float DisplayBase { get; set; }
    public float DisplayScale { get; set; }
    public float RatingSigmaMultiplier { get; set; }
    public float PodiumBoostBase { get; set; }
    public float PodiumBoostRange { get; set; }
    public float PodiumBoostTarget { get; set; }
    public float ProvisionalSigmaThreshold { get; set; }
    public float NormTargetMean { get; set; }
    public float NormTargetStd { get; set; }
    public float EliteTopPercent { get; set; }
    public float ChampionTopPercent { get; set; }
    public float ExpertTopPercent { get; set; }
    public int CountryTopN { get; set; }
    public int MinTeamsForCountryRanking { get; set; }
}
