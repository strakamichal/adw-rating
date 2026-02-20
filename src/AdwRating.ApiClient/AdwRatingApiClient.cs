using System.Net;
using System.Net.Http.Json;
using System.Text;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;

namespace AdwRating.ApiClient;

/// <summary>
/// Typed HttpClient wrapper for the ADW Rating API.
/// Each method maps 1:1 to an API endpoint.
/// Registered in DI as a typed HttpClient in the Web project.
/// </summary>
public class AdwRatingApiClient
{
    private readonly HttpClient _http;

    public AdwRatingApiClient(HttpClient http) => _http = http;

    /// <summary>
    /// GET /api/rankings?size={}&amp;country={}&amp;search={}&amp;page={}&amp;pageSize={}
    /// </summary>
    public async Task<PagedResult<TeamRankingDto>> GetRankingsAsync(RankingFilter filter)
    {
        var url = new StringBuilder("api/rankings?");
        url.Append($"size={filter.Size}");
        if (!string.IsNullOrEmpty(filter.Country))
            url.Append($"&country={Uri.EscapeDataString(filter.Country)}");
        if (!string.IsNullOrEmpty(filter.Search))
            url.Append($"&search={Uri.EscapeDataString(filter.Search)}");
        url.Append($"&page={filter.Page}");
        url.Append($"&pageSize={filter.PageSize}");

        var result = await _http.GetFromJsonAsync<PagedResult<TeamRankingDto>>(url.ToString());
        return result ?? new PagedResult<TeamRankingDto>([], 0, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// GET /api/rankings/summary
    /// </summary>
    public async Task<RankingSummary> GetSummaryAsync()
    {
        var result = await _http.GetFromJsonAsync<RankingSummary>("api/rankings/summary");
        return result ?? new RankingSummary(0, 0, 0);
    }

    /// <summary>
    /// GET /api/teams/{slug}
    /// Returns null if the team is not found (404).
    /// </summary>
    public async Task<TeamDetailDto?> GetTeamAsync(string slug)
    {
        var response = await _http.GetAsync($"api/teams/{Uri.EscapeDataString(slug)}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TeamDetailDto>();
    }

    /// <summary>
    /// GET /api/teams/{slug}/history
    /// Returns rating progression snapshots for charting.
    /// </summary>
    public async Task<IReadOnlyList<RatingSnapshotDto>> GetTeamHistoryAsync(string slug)
    {
        var response = await _http.GetAsync($"api/teams/{Uri.EscapeDataString(slug)}/history");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RatingSnapshotDto>>();
        return result ?? [];
    }

    /// <summary>
    /// GET /api/teams/{slug}/results?page={}&amp;pageSize={}
    /// </summary>
    public async Task<PagedResult<TeamResultDto>> GetTeamResultsAsync(string slug, int page = 1, int pageSize = 20)
    {
        var url = $"api/teams/{Uri.EscapeDataString(slug)}/results?page={page}&pageSize={pageSize}";
        var response = await _http.GetAsync(url);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new PagedResult<TeamResultDto>([], 0, page, pageSize);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TeamResultDto>>();
        return result ?? new PagedResult<TeamResultDto>([], 0, page, pageSize);
    }

    /// <summary>
    /// GET /api/competitions?year={}&amp;tier={}&amp;country={}&amp;search={}&amp;page={}&amp;pageSize={}
    /// </summary>
    public async Task<PagedResult<CompetitionDetailDto>> GetCompetitionsAsync(CompetitionFilter filter)
    {
        var url = new StringBuilder("api/competitions?");
        var hasParam = false;

        if (filter.Year.HasValue)
        {
            url.Append($"year={filter.Year.Value}");
            hasParam = true;
        }
        if (filter.Tier.HasValue)
        {
            if (hasParam) url.Append('&');
            url.Append($"tier={filter.Tier.Value}");
            hasParam = true;
        }
        if (!string.IsNullOrEmpty(filter.Country))
        {
            if (hasParam) url.Append('&');
            url.Append($"country={Uri.EscapeDataString(filter.Country)}");
            hasParam = true;
        }
        if (!string.IsNullOrEmpty(filter.Search))
        {
            if (hasParam) url.Append('&');
            url.Append($"search={Uri.EscapeDataString(filter.Search)}");
            hasParam = true;
        }
        if (hasParam) url.Append('&');
        url.Append($"page={filter.Page}&pageSize={filter.PageSize}");

        var result = await _http.GetFromJsonAsync<PagedResult<CompetitionDetailDto>>(url.ToString());
        return result ?? new PagedResult<CompetitionDetailDto>([], 0, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// GET /api/search?q={query}&amp;limit={limit}
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit = 10)
    {
        var url = $"api/search?q={Uri.EscapeDataString(query)}&limit={limit}";
        var result = await _http.GetFromJsonAsync<IReadOnlyList<SearchResult>>(url);
        return result ?? [];
    }
}
