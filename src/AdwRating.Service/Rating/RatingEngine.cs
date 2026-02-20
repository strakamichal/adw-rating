using OpenSkillSharp.Models;
using OpenSkillSharp.Rating;

namespace AdwRating.Service.Rating;

/// <summary>
/// Thin wrapper around OpenSkillSharp PlackettLuce model,
/// isolating the library from the rest of the system.
/// </summary>
public class RatingEngine
{
    private readonly PlackettLuce _model;

    public RatingEngine(double mu0 = 25.0, double sigma0 = 8.333)
    {
        _model = new PlackettLuce
        {
            Mu = mu0,
            Sigma = sigma0,
            Tau = 0, // We handle uncertainty dynamics ourselves (SigmaDecay/SigmaMin)
        };
    }

    /// <summary>
    /// Creates initial rating values.
    /// </summary>
    public (double Mu, double Sigma) CreateRating()
    {
        var rating = _model.Rating();
        return (rating.Mu, rating.Sigma);
    }

    /// <summary>
    /// Creates a rating with specific mu/sigma values.
    /// </summary>
    public (double Mu, double Sigma) CreateRating(double mu, double sigma)
    {
        return (mu, sigma);
    }

    /// <summary>
    /// Processes a single run through the PlackettLuce model and returns updated ratings.
    /// </summary>
    /// <param name="teamRatings">Current (Mu, Sigma) for each team in the run.</param>
    /// <param name="ranks">Rank for each team (1-based, lower is better). Ties allowed (e.g., eliminated teams share last rank).</param>
    /// <param name="weight">Weight multiplier for rating changes (1.0 = normal, 1.2 = major event).</param>
    /// <returns>Updated (Mu, Sigma) for each team, in the same order as input.</returns>
    public IReadOnlyList<(double Mu, double Sigma)> ProcessRun(
        IReadOnlyList<(double Mu, double Sigma)> teamRatings,
        IReadOnlyList<int> ranks,
        double weight = 1.0)
    {
        if (teamRatings.Count != ranks.Count)
            throw new ArgumentException("teamRatings and ranks must have the same length.");

        if (teamRatings.Count < 2)
            throw new ArgumentException("A run must have at least 2 teams.");

        // Build OpenSkillSharp teams (single-player teams)
        var teams = new List<ITeam>(teamRatings.Count);
        for (int i = 0; i < teamRatings.Count; i++)
        {
            var (mu, sigma) = teamRatings[i];
            var rating = _model.Rating(mu: mu, sigma: sigma);
            teams.Add(new Team { Players = [rating] });
        }

        // Convert ranks to double list for OpenSkillSharp
        var rankDoubles = ranks.Select(r => (double)r).ToList();

        // Run PlackettLuce model
        var updatedTeams = _model.Rate(teams, ranks: rankDoubles).ToList();

        // Extract updated ratings and apply weight to deltas
        var results = new List<(double Mu, double Sigma)>(teamRatings.Count);
        for (int i = 0; i < teamRatings.Count; i++)
        {
            var original = teamRatings[i];
            var updated = updatedTeams[i].Players.First();

            double newMu, newSigma;

            if (Math.Abs(weight - 1.0) < 1e-9)
            {
                // No weighting needed
                newMu = updated.Mu;
                newSigma = updated.Sigma;
            }
            else
            {
                // Apply weight to the delta: amplify/dampen the change
                double muDelta = updated.Mu - original.Mu;
                double sigmaDelta = updated.Sigma - original.Sigma;

                newMu = original.Mu + weight * muDelta;
                newSigma = original.Sigma + weight * sigmaDelta;
            }

            results.Add((newMu, newSigma));
        }

        return results;
    }
}
