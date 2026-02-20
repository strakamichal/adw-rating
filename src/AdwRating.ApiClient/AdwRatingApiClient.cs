using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AdwRating.ApiClient;

/// <summary>
/// Typed HttpClient wrapper for the ADW Rating API.
/// Each method maps 1:1 to an API endpoint.
/// Registered in DI as a typed HttpClient in the Web project.
/// </summary>
public class AdwRatingApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AdwRatingApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AdwRatingApiClient(HttpClient http, ILogger<AdwRatingApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/rankings?size={}&amp;country={}&amp;search={}&amp;page={}&amp;pageSize={}
    /// </summary>
    public async Task<PagedResult<TeamRankingDto>> GetRankingsAsync(RankingFilter filter)
    {
        try
        {
            var url = new StringBuilder("api/rankings?");
            url.Append($"size={filter.Size}");
            if (!string.IsNullOrEmpty(filter.Country))
                url.Append($"&country={Uri.EscapeDataString(filter.Country)}");
            if (!string.IsNullOrEmpty(filter.Search))
                url.Append($"&search={Uri.EscapeDataString(filter.Search)}");
            url.Append($"&page={filter.Page}");
            url.Append($"&pageSize={filter.PageSize}");

            var result = await _http.GetFromJsonAsync<PagedResult<TeamRankingDto>>(url.ToString(), JsonOptions);
            return result ?? new PagedResult<TeamRankingDto>([], 0, filter.Page, filter.PageSize);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch rankings from API");
            return new PagedResult<TeamRankingDto>([], 0, filter.Page, filter.PageSize);
        }
    }

    /// <summary>
    /// GET /api/rankings/summary
    /// </summary>
    public async Task<RankingSummary> GetSummaryAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<RankingSummary>("api/rankings/summary", JsonOptions);
            return result ?? new RankingSummary(0, 0, 0);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch summary from API");
            return new RankingSummary(0, 0, 0);
        }
    }

    /// <summary>
    /// GET /api/teams/{slug}
    /// Returns null if the team is not found (404).
    /// </summary>
    public async Task<TeamDetailDto?> GetTeamAsync(string slug)
    {
        try
        {
            var response = await _http.GetAsync($"api/teams/{Uri.EscapeDataString(slug)}");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TeamDetailDto>(JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch team {Slug} from API", slug);
            return null;
        }
    }

    /// <summary>
    /// GET /api/teams/{slug}/history
    /// Returns rating progression snapshots for charting.
    /// </summary>
    public async Task<IReadOnlyList<RatingSnapshotDto>> GetTeamHistoryAsync(string slug)
    {
        try
        {
            var response = await _http.GetAsync($"api/teams/{Uri.EscapeDataString(slug)}/history");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<RatingSnapshotDto>>(JsonOptions);
            return result ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch team history for {Slug} from API", slug);
            return [];
        }
    }

    /// <summary>
    /// GET /api/teams/{slug}/results?page={}&amp;pageSize={}
    /// </summary>
    public async Task<PagedResult<TeamResultDto>> GetTeamResultsAsync(string slug, int page = 1, int pageSize = 20)
    {
        try
        {
            var url = $"api/teams/{Uri.EscapeDataString(slug)}/results?page={page}&pageSize={pageSize}";
            var response = await _http.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new PagedResult<TeamResultDto>([], 0, page, pageSize);

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<PagedResult<TeamResultDto>>(JsonOptions);
            return result ?? new PagedResult<TeamResultDto>([], 0, page, pageSize);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch team results for {Slug} from API", slug);
            return new PagedResult<TeamResultDto>([], 0, page, pageSize);
        }
    }

    /// <summary>
    /// GET /api/competitions?year={}&amp;tier={}&amp;country={}&amp;search={}&amp;page={}&amp;pageSize={}
    /// </summary>
    public async Task<PagedResult<CompetitionDetailDto>> GetCompetitionsAsync(CompetitionFilter filter)
    {
        try
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

            var result = await _http.GetFromJsonAsync<PagedResult<CompetitionDetailDto>>(url.ToString(), JsonOptions);
            return result ?? new PagedResult<CompetitionDetailDto>([], 0, filter.Page, filter.PageSize);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch competitions from API");
            return new PagedResult<CompetitionDetailDto>([], 0, filter.Page, filter.PageSize);
        }
    }

    /// <summary>
    /// GET /api/search?q={query}&amp;limit={limit}
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit = 10)
    {
        try
        {
            var url = $"api/search?q={Uri.EscapeDataString(query)}&limit={limit}";
            var result = await _http.GetFromJsonAsync<IReadOnlyList<SearchResult>>(url, JsonOptions);
            return result ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to search API for query {Query}", query);
            return [];
        }
    }
}
