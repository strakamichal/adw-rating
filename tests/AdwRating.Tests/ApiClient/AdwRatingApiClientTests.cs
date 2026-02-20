using System.Net;
using System.Text.Json;
using AdwRating.ApiClient;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace AdwRating.Tests.ApiClient;

[TestFixture]
public class AdwRatingApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static AdwRatingApiClient CreateClient(
        HttpStatusCode statusCode,
        object? responseBody = null,
        Action<HttpRequestMessage>? requestInspector = null)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseBody, requestInspector);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com/")
        };
        return new AdwRatingApiClient(httpClient, NullLogger<AdwRatingApiClient>.Instance);
    }

    #region Rankings

    [Test]
    public async Task GetRankingsAsync_WithFilters_BuildsCorrectUrl()
    {
        Uri? capturedUri = null;
        var responseData = new PagedResult<TeamRankingDto>([], 0, 1, 50);

        var client = CreateClient(
            HttpStatusCode.OK,
            responseData,
            req => capturedUri = req.RequestUri);

        var filter = new RankingFilter(SizeCategory.L, "CZE", "Rex", 2, 25);
        await client.GetRankingsAsync(filter);

        Assert.That(capturedUri, Is.Not.Null);
        var url = capturedUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("size=L"));
            Assert.That(url, Does.Contain("country=CZE"));
            Assert.That(url, Does.Contain("search=Rex"));
            Assert.That(url, Does.Contain("page=2"));
            Assert.That(url, Does.Contain("pageSize=25"));
        });
    }

    [Test]
    public async Task GetRankingsAsync_ReturnsPagedResult()
    {
        var items = new List<TeamRankingDto>
        {
            new(1, "john-rex", "John", "CZE", "Rex", "Rex von Haus", SizeCategory.L,
                1500f, 50f, 1, null, 10, 8, 3, false, TierLabel.Elite)
        };
        var responseData = new PagedResult<TeamRankingDto>(items, 1, 1, 50);

        var client = CreateClient(HttpStatusCode.OK, responseData);
        var filter = new RankingFilter(SizeCategory.L, null, null);

        var result = await client.GetRankingsAsync(filter);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].Slug, Is.EqualTo("john-rex"));
            Assert.That(result.Items[0].Rating, Is.EqualTo(1500f));
        });
    }

    [Test]
    public async Task GetRankingsAsync_WithoutOptionalFilters_OmitsOptionalParams()
    {
        Uri? capturedUri = null;
        var responseData = new PagedResult<TeamRankingDto>([], 0, 1, 50);

        var client = CreateClient(
            HttpStatusCode.OK,
            responseData,
            req => capturedUri = req.RequestUri);

        var filter = new RankingFilter(SizeCategory.M, null, null);
        await client.GetRankingsAsync(filter);

        var url = capturedUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("size=M"));
            Assert.That(url, Does.Not.Contain("country="));
            Assert.That(url, Does.Not.Contain("search="));
        });
    }

    #endregion

    #region Summary

    [Test]
    public async Task GetSummaryAsync_ReturnsSummary()
    {
        var responseData = new RankingSummary(150, 25, 500);

        var client = CreateClient(HttpStatusCode.OK, responseData);

        var result = await client.GetSummaryAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.QualifiedTeams, Is.EqualTo(150));
            Assert.That(result.Competitions, Is.EqualTo(25));
            Assert.That(result.Runs, Is.EqualTo(500));
        });
    }

    #endregion

    #region Teams

    [Test]
    public async Task GetTeamAsync_ExistingSlug_ReturnsTeam()
    {
        var responseData = new TeamDetailDto(
            1, "john-rex", "John", "john", "CZE", "Rex", "Rex von Test", "Border Collie",
            SizeCategory.L, 1500f, 50f, 1480f, 1520f,
            20, 18, 5, true, false, TierLabel.Elite,
            90f, 27.8f, 2.5f);

        var client = CreateClient(HttpStatusCode.OK, responseData);

        var result = await client.GetTeamAsync("john-rex");

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Slug, Is.EqualTo("john-rex"));
            Assert.That(result.HandlerName, Is.EqualTo("John"));
            Assert.That(result.Rating, Is.EqualTo(1500f));
        });
    }

    [Test]
    public async Task GetTeamAsync_NonExistentSlug_ReturnsNull()
    {
        var client = CreateClient(HttpStatusCode.NotFound);

        var result = await client.GetTeamAsync("nonexistent");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetTeamHistoryAsync_ReturnsSnapshots()
    {
        var snapshots = new List<RatingSnapshotDto>
        {
            new(new DateOnly(2024, 1, 15), 25.0f, 8.333f, 1200f),
            new(new DateOnly(2024, 3, 20), 26.5f, 7.0f, 1350f)
        };

        var client = CreateClient(HttpStatusCode.OK, snapshots);

        var result = await client.GetTeamHistoryAsync("john-rex");

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Date, Is.EqualTo(new DateOnly(2024, 1, 15)));
            Assert.That(result[0].Rating, Is.EqualTo(1200f));
            Assert.That(result[1].Mu, Is.EqualTo(26.5f));
        });
    }

    [Test]
    public async Task GetTeamHistoryAsync_NotFound_ReturnsEmptyList()
    {
        var client = CreateClient(HttpStatusCode.NotFound);

        var result = await client.GetTeamHistoryAsync("nonexistent");

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTeamResultsAsync_ReturnsPagedResult()
    {
        Uri? capturedUri = null;
        var items = new List<TeamResultDto>
        {
            new("awc2024", "AWC 2024", new DateOnly(2024, 10, 3), SizeCategory.L,
                Discipline.Agility, false, 1, 0, 0f, 35.2f, 4.5f, false)
        };
        var responseData = new PagedResult<TeamResultDto>(items, 1, 2, 10);

        var client = CreateClient(
            HttpStatusCode.OK,
            responseData,
            req => capturedUri = req.RequestUri);

        var result = await client.GetTeamResultsAsync("john-rex", 2, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Items, Has.Count.EqualTo(1));
            Assert.That(result.Items[0].CompetitionName, Is.EqualTo("AWC 2024"));
        });

        var url = capturedUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("page=2"));
            Assert.That(url, Does.Contain("pageSize=10"));
        });
    }

    [Test]
    public async Task GetTeamResultsAsync_NotFound_ReturnsEmptyPagedResult()
    {
        var client = CreateClient(HttpStatusCode.NotFound);

        var result = await client.GetTeamResultsAsync("nonexistent");

        Assert.Multiple(() =>
        {
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.TotalCount, Is.EqualTo(0));
        });
    }

    #endregion

    #region Competitions

    [Test]
    public async Task GetCompetitionsAsync_WithFilters_BuildsCorrectUrl()
    {
        Uri? capturedUri = null;
        var responseData = new PagedResult<CompetitionDetailDto>([], 0, 1, 20);

        var client = CreateClient(
            HttpStatusCode.OK,
            responseData,
            req => capturedUri = req.RequestUri);

        var filter = new CompetitionFilter(2024, 1, "CZE", "AWC", 2, 10);
        await client.GetCompetitionsAsync(filter);

        var url = capturedUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("year=2024"));
            Assert.That(url, Does.Contain("tier=1"));
            Assert.That(url, Does.Contain("country=CZE"));
            Assert.That(url, Does.Contain("search=AWC"));
            Assert.That(url, Does.Contain("page=2"));
            Assert.That(url, Does.Contain("pageSize=10"));
        });
    }

    [Test]
    public async Task GetCompetitionsAsync_WithoutOptionalFilters_OmitsOptionalParams()
    {
        Uri? capturedUri = null;
        var responseData = new PagedResult<CompetitionDetailDto>([], 0, 1, 20);

        var client = CreateClient(
            HttpStatusCode.OK,
            responseData,
            req => capturedUri = req.RequestUri);

        var filter = new CompetitionFilter(null, null, null, null);
        await client.GetCompetitionsAsync(filter);

        var url = capturedUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Not.Contain("year="));
            Assert.That(url, Does.Not.Contain("tier="));
            Assert.That(url, Does.Not.Contain("country="));
            Assert.That(url, Does.Not.Contain("search="));
            Assert.That(url, Does.Contain("page=1"));
            Assert.That(url, Does.Contain("pageSize=20"));
        });
    }

    #endregion

    #region Search

    [Test]
    public async Task SearchAsync_BuildsCorrectUrl()
    {
        Uri? capturedUri = null;
        var responseData = new List<SearchResult>();

        var client = CreateClient(
            HttpStatusCode.OK,
            responseData,
            req => capturedUri = req.RequestUri);

        await client.SearchAsync("Rex", 5);

        var url = capturedUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("q=Rex"));
            Assert.That(url, Does.Contain("limit=5"));
        });
    }

    [Test]
    public async Task SearchAsync_ReturnsResults()
    {
        var responseData = new List<SearchResult>
        {
            new("team", "john-rex", "John & Rex", "CZE · L · 1500"),
            new("handler", "john", "John Smith", "CZE")
        };

        var client = CreateClient(HttpStatusCode.OK, responseData);

        var result = await client.SearchAsync("John");

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Type, Is.EqualTo("team"));
            Assert.That(result[0].Slug, Is.EqualTo("john-rex"));
            Assert.That(result[1].Type, Is.EqualTo("handler"));
        });
    }

    #endregion

    /// <summary>
    /// A mock HttpMessageHandler that returns a predefined response
    /// and optionally captures the request for URL inspection.
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly object? _responseBody;
        private readonly Action<HttpRequestMessage>? _requestInspector;

        public MockHttpMessageHandler(
            HttpStatusCode statusCode,
            object? responseBody = null,
            Action<HttpRequestMessage>? requestInspector = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _requestInspector = requestInspector;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _requestInspector?.Invoke(request);

            var response = new HttpResponseMessage(_statusCode);

            if (_responseBody is not null)
            {
                var json = JsonSerializer.Serialize(_responseBody, JsonOptions);
                response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}
