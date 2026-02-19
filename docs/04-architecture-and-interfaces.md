# Architecture and Interfaces

## 1. Technical stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Language | C# | .NET 10 |
| Web framework | ASP.NET Core | 10 |
| UI framework | Blazor (Static SSR + Enhanced Navigation) | 10 |
| Database | Microsoft SQL Server | 2022 |
| ORM | Entity Framework Core | 10 |
| Auth | None (MVP is fully public, read-only) | — |
| Rating engine | [openskill.net](https://github.com/nicholasgasior/openskill.net) (PlackettLuce) | latest |
| Testing | xUnit + WebApplicationFactory + Playwright | latest |
| CLI framework | System.CommandLine | latest |
| Reverse proxy | IIS (with ARR + URL Rewrite) | 10 |
| CI/CD | GitHub Actions | — |

### Key library choices

- **Blazor Static SSR** — pages are rendered as pure HTML on the server. No SignalR circuit, no WebSocket. Enhanced Navigation provides SPA-like transitions (partial DOM swap via `fetch`) without a JS framework. Ideal for a read-only, SEO-critical site.
- **openskill.net** — C# port of the OpenSkill PlackettLuce model, matching the Python `openskill` library used in the current scripts. The library provides `model.Rating()` (initial mu/sigma) and `model.Rate(teams, ranks, weights)` — the same API surface we use today. All additional logic (sigma decay, display scaling, podium boost, cross-size normalization) is implemented in our `Service` layer, not in the library. If the package proves unmaintained, replacing it with a custom implementation is straightforward — the PlackettLuce core is ~200 lines.
- **System.CommandLine** — for the admin CLI tool (import, recalculation, merge). Provides argument parsing, help generation, and a clean command structure.
- **EF Core** — standard ORM for .NET. Code-first migrations, LINQ queries, change tracking. SQL Server provider.

## 2. Application structure

### Project layout

```
src/
├── AdwRating.Domain/          # Entities, interfaces, enums — no dependencies
├── AdwRating.Service/         # Business logic — depends on Domain only
├── AdwRating.Data.Mssql/  # EF Core DbContext, migrations, repository implementations
├── AdwRating.Api/             # REST API (ASP.NET Core controllers)
├── AdwRating.Web/             # Blazor SSR pages — consumes API via ApiClient
├── AdwRating.ApiClient/       # Typed HttpClient wrapper for the API
└── AdwRating.Cli/             # Admin CLI tool (import, recalculate, merge)

tests/
├── AdwRating.Tests/               # Unit tests (Service, Domain logic)
├── AdwRating.IntegrationTests/    # Integration tests (DB, API via WebApplicationFactory)
└── AdwRating.E2ETests/            # End-to-end tests (Playwright, full UI)
```

### Dependency rules

```
Web ──► ApiClient ──► Domain
Api ──► Service ──► Domain ◄── Data.Mssql
Cli ──► Service ──► Domain ◄── Data.Mssql
```

| Project | Can depend on | CANNOT depend on |
|---------|---------------|------------------|
| **Domain** | nothing | anything else |
| **Service** | Domain | Data.*, Api, Web |
| **Data.Mssql** | Domain | Service, Api, Web |
| **Api** | Domain, Service | Web, Cli |
| **Web** | Domain, ApiClient | Service, Data.* |
| **ApiClient** | Domain | Service, Data.* |
| **Cli** | Domain, Service, Data.Mssql (DI registration only) | Api, Web |

**Key rules:**
1. **Never use `AppDbContext` or any `Data.*` types outside of `Data.Mssql`** (except DI registration in `Program.cs` of host projects).
2. **All data access goes through repository interfaces** defined in `Domain/Interfaces/`.
3. **Service layer depends only on interfaces**, never on concrete implementations.
4. **Web never calls Service directly** — it goes through the API via `ApiClient`. This keeps the Web project as a pure presentation layer and ensures the API is the single entry point for all data access.
5. **Cli calls Service directly** — admin operations (import, recalculation, merge) are heavy batch processes that don't benefit from HTTP overhead.

### Why separate Api and Web?

- **API first** — the API is the contract. Web is just one consumer; future consumers (mobile app, third-party integrations, crowdsourcing UI) use the same API.
- **SEO** — Blazor Static SSR renders HTML on the server. The Web project fetches data from the API via `ApiClient` and renders it as HTML.
- **Deployment** — API and Web run as separate processes behind IIS (see section 8). Web reads the API base URL from configuration (`ApiBaseUrl` in `appsettings.json`, e.g., `http://localhost:5001`). This URL is injected into `ApiClient` via DI. In production both processes run on the same VPS and communicate over `localhost`.

### ApiClient approach

`AdwRating.ApiClient` is a **manually written typed `HttpClient` wrapper**. It contains one class (`AdwRatingApiClient`) with async methods that map 1:1 to the API endpoints:

```csharp
public class AdwRatingApiClient
{
    private readonly HttpClient _http;

    public AdwRatingApiClient(HttpClient http) => _http = http;

    public Task<PagedResult<TeamRankingDto>> GetRankingsAsync(RankingFilter filter) { ... }
    public Task<RankingSummary> GetSummaryAsync() { ... }
    public Task<TeamDetailDto> GetTeamAsync(string slug) { ... }
    // ... one method per API endpoint
}
```

Registration in `Web/Program.cs`:

```csharp
builder.Services.AddHttpClient<AdwRatingApiClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!));
```

For MVP (~15 endpoints), manual typing is simpler than OpenAPI code generation. The client uses `System.Text.Json` deserialization — the same serializer as the API. If the API grows significantly (Phase 2+), consider generating the client from an OpenAPI spec.

## 3. Internal interfaces and flows

### Request lifecycle (Web → API)

```
[Browser] → GET /rankings?size=L&country=CZE
  → [IIS reverse proxy]
  → [AdwRating.Web] (Blazor Static SSR)
    → Page component calls ApiClient.GetRankingsAsync(size, country, page)
    → [ApiClient] → HTTP GET /api/rankings?size=L&country=CZE&page=1
    → [AdwRating.Api]
      → [RankingsController.GetList()]
      → [IRankingService.GetRankingsAsync(filter)]  // defined in Domain/Interfaces/
      → [ITeamRepository.GetRankedTeamsAsync(filter)]
      → [EF Core → SQL Server]
    ← JSON response (PagedResult<TeamRankingDto>)
  ← Rendered HTML page
← HTML to browser (Enhanced Navigation: partial DOM swap)
```

### Request lifecycle (CLI — import)

```
$ adw-cli import --file data/awc2024/awc2024_results.csv --competition awc2024
  → [AdwRating.Cli]
    → [IImportService.ImportCompetitionAsync(filePath, slug)]
      → Parse CSV
      → Validate all rows
      → Identity resolution (handler/dog matching via aliases + fuzzy)
      → Create/resolve Handler, Dog, Team entities
      → Create Competition, Run, RunResult entities
      → Write ImportLog
    → [IRatingService.RecalculateAsync()]  (if --recalculate flag)
      → Process all runs chronologically
      → Update Team rating fields
      → Generate RatingSnapshots
  ← Console output: summary report
```

### Key interfaces (Domain)

#### Repository interfaces

```csharp
// Domain/Interfaces/IHandlerRepository.cs
public interface IHandlerRepository
{
    Task<Handler?> GetByIdAsync(int id);
    Task<Handler?> GetBySlugAsync(string slug);
    Task<Handler?> FindByNormalizedNameAndCountryAsync(string normalizedName, string country);
    Task<IReadOnlyList<Handler>> SearchAsync(string query, int limit);
    Task<Handler> CreateAsync(Handler handler);
    Task UpdateAsync(Handler handler);
    Task MergeAsync(int sourceId, int targetId);
}

// Domain/Interfaces/IDogRepository.cs
public interface IDogRepository
{
    Task<Dog?> GetByIdAsync(int id);
    Task<Dog?> FindByNormalizedNameAndSizeAsync(string normalizedCallName, SizeCategory size);
    Task<IReadOnlyList<Dog>> SearchAsync(string query, int limit);
    Task<Dog> CreateAsync(Dog dog);
    Task UpdateAsync(Dog dog);
    Task MergeAsync(int sourceId, int targetId);
}

// Domain/Interfaces/ITeamRepository.cs
public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(int id);
    Task<Team?> GetBySlugAsync(string slug);
    Task<Team?> GetByHandlerAndDogAsync(int handlerId, int dogId);
    Task<IReadOnlyList<Team>> GetByHandlerIdAsync(int handlerId);
    Task<PagedResult<Team>> GetRankedTeamsAsync(RankingFilter filter);
    Task<IReadOnlyList<Team>> GetAllAsync();   // for full rating recalculation
    Task<Team> CreateAsync(Team team);
    Task UpdateBatchAsync(IEnumerable<Team> teams);
}

// Domain/Interfaces/ICompetitionRepository.cs
public interface ICompetitionRepository
{
    Task<Competition?> GetByIdAsync(int id);
    Task<Competition?> GetBySlugAsync(string slug);
    Task<PagedResult<Competition>> GetListAsync(CompetitionFilter filter);
    Task<Competition> CreateAsync(Competition competition);
    Task DeleteCascadeAsync(int id);
}

// Domain/Interfaces/IRunRepository.cs
public interface IRunRepository
{
    Task<IReadOnlyList<Run>> GetByCompetitionIdAsync(int competitionId);
    Task<Run?> GetByCompetitionAndRoundKeyAsync(int competitionId, string roundKey);
    Task<IReadOnlyList<Run>> GetAllInWindowAsync(DateOnly cutoffDate);  // for rating recalculation
    Task CreateBatchAsync(IEnumerable<Run> runs);
}

// Domain/Interfaces/IRunResultRepository.cs
public interface IRunResultRepository
{
    Task<IReadOnlyList<RunResult>> GetByRunIdAsync(int runId);
    /// <summary>
    /// Returns run results for multiple runs in a single query (batch loading).
    /// Used during rating recalculation to avoid N+1 queries.
    /// Results are grouped by RunId in the returned list.
    /// </summary>
    Task<IReadOnlyList<RunResult>> GetByRunIdsAsync(IEnumerable<int> runIds);
    /// <summary>
    /// Returns run results for a team, optionally filtered to runs on or after the given date.
    /// Implementation note: RunResult has no date field — the query joins through
    /// RunResult.Run.Date (navigation property) to filter by date.
    /// Results are returned ordered by Run.Date ascending, then Run.RunNumber.
    /// </summary>
    Task<IReadOnlyList<RunResult>> GetByTeamIdAsync(int teamId, DateOnly? after = null);
    Task CreateBatchAsync(IEnumerable<RunResult> results);
}

// Domain/Interfaces/IHandlerAliasRepository.cs
public interface IHandlerAliasRepository
{
    Task<HandlerAlias?> FindByAliasNameAsync(string normalizedAliasName);
    Task<IReadOnlyList<HandlerAlias>> GetByHandlerIdAsync(int handlerId);
    Task CreateAsync(HandlerAlias alias);
}

// Domain/Interfaces/IDogAliasRepository.cs
public interface IDogAliasRepository
{
    Task<DogAlias?> FindByAliasNameAndTypeAsync(string normalizedAliasName, DogAliasType type);
    Task<IReadOnlyList<DogAlias>> GetByDogIdAsync(int dogId);
    Task CreateAsync(DogAlias alias);
}

// Domain/Interfaces/IRatingSnapshotRepository.cs
public interface IRatingSnapshotRepository
{
    Task<IReadOnlyList<RatingSnapshot>> GetByTeamIdAsync(int teamId);
    Task ReplaceAllAsync(IEnumerable<RatingSnapshot> snapshots);
}

// Domain/Interfaces/IRatingConfigurationRepository.cs
public interface IRatingConfigurationRepository
{
    Task<RatingConfiguration> GetActiveAsync();
    Task CreateAsync(RatingConfiguration config);
}

// Domain/Interfaces/IImportLogRepository.cs
public interface IImportLogRepository
{
    Task CreateAsync(ImportLog log);
    Task<IReadOnlyList<ImportLog>> GetRecentAsync(int count);
}
```

#### Service interfaces

```csharp
// Domain/Interfaces/IImportService.cs
public interface IImportService
{
    Task<ImportResult> ImportCompetitionAsync(string filePath, string competitionSlug,
        CompetitionMetadata metadata);
}

// Domain/Interfaces/IRatingService.cs
public interface IRatingService
{
    /// <summary>
    /// Full recalculation: processes all runs within the live window chronologically,
    /// updates mu/sigma/rating for every team, computes cross-size normalization,
    /// assigns tier labels, and regenerates all RatingSnapshots.
    /// Mirrors the logic in scripts/calculate_rating_live_final.py.
    /// </summary>
    Task RecalculateAllAsync();
}

// Domain/Interfaces/IRankingService.cs
public interface IRankingService
{
    Task<PagedResult<Team>> GetRankingsAsync(RankingFilter filter);
    Task<RankingSummary> GetSummaryAsync();
}

// Domain/Interfaces/ISearchService.cs
public interface ISearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int limit = 10);
}

// Domain/Interfaces/IIdentityResolutionService.cs
public interface IIdentityResolutionService
{
    Task<Handler> ResolveHandlerAsync(string rawName, string country);
    Task<Dog> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size);
    Task<Team> ResolveTeamAsync(int handlerId, int dogId);
}

// Domain/Interfaces/ITeamProfileService.cs
public interface ITeamProfileService
{
    /// <summary>
    /// Returns team detail with computed stats (finished %, top3 %, avg rank).
    /// Returns the team regardless of IsActive status (inactive teams are still viewable).
    /// </summary>
    Task<TeamDetailDto?> GetBySlugAsync(string slug);

    /// <summary>
    /// Returns paginated competition results for a team, ordered by date descending.
    /// Each row combines RunResult + Run + Competition data.
    /// </summary>
    Task<PagedResult<TeamResultDto>> GetResultsAsync(string teamSlug, int page = 1, int pageSize = 20);
}

// Domain/Interfaces/IHandlerProfileService.cs
public interface IHandlerProfileService
{
    /// <summary>
    /// Returns handler detail with all teams, including peak rating per team.
    /// Peak rating is the highest Rating the team ever achieved
    /// (tracked via RatingSnapshots or a dedicated PeakRating field on Team).
    /// </summary>
    Task<HandlerDetailDto?> GetBySlugAsync(string slug);
}

// Domain/Interfaces/ICountryRankingService.cs
public interface ICountryRankingService
{
    /// <summary>
    /// Returns all countries ranked by Country Score (average of top N teams).
    /// Only countries with ≥ MinTeamsForCountryRanking active teams are included.
    /// Country Score uses normalized Rating values (comparable across size categories).
    /// </summary>
    Task<IReadOnlyList<CountryRankingDto>> GetRankingsAsync();

    /// <summary>
    /// Returns detail for a single country including its top N teams.
    /// Returns null if country code doesn't exist in the dataset.
    /// </summary>
    Task<CountryDetailDto?> GetByCountryCodeAsync(string countryCode);
}

// Domain/Interfaces/IMergeService.cs
public interface IMergeService
{
    Task MergeHandlersAsync(int sourceHandlerId, int targetHandlerId);
    Task MergeDogsAsync(int sourceDogId, int targetDogId);
}
```

#### Common types

```csharp
// Domain/Models/PagedResult.cs
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// Domain/Models/RankingFilter.cs
public record RankingFilter(
    SizeCategory Size,           // required — rankings are always per size category
    string? Country,
    string? Search,
    int Page = 1,
    int PageSize = 50
);

// Domain/Models/CompetitionFilter.cs
public record CompetitionFilter(
    int? Year,
    int? Tier,
    string? Country,
    string? Search,
    int Page = 1,
    int PageSize = 20
);

// Domain/Models/ImportResult.cs
public record ImportResult(
    bool Success,
    int RowCount,
    int NewHandlers,
    int NewDogs,
    int NewTeams,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);

// Domain/Models/CompetitionMetadata.cs
/// <summary>
/// Metadata provided by the admin during import.
/// Tier is validated: must be 1 (major) or 2 (standard).
/// </summary>
public record CompetitionMetadata(
    string Name,
    DateOnly Date,
    DateOnly? EndDate,
    string? Country,           // ISO 3166-1 alpha-3
    string? Location,
    int Tier                   // 1 = major (weight 1.2), 2 = standard (weight 1.0)
);

// Domain/Models/RankingSummary.cs
public record RankingSummary(
    int QualifiedTeams,    // teams meeting min run threshold across all categories
    int Competitions,      // total competitions in the dataset
    int Runs               // total runs in the dataset
);

// Domain/Models/SearchResult.cs
public record SearchResult(
    string Type,           // "team", "handler", or "competition"
    string Slug,
    string DisplayName,
    string? Subtitle       // e.g., country for handler, rating for team, date for competition
);
```

#### API DTOs

DTOs live in `Domain/Models/` and are used by both the API (response serialization) and ApiClient (deserialization).

```csharp
// Domain/Models/TeamRankingDto.cs
public record TeamRankingDto(
    int Id,
    string Slug,
    string HandlerName,
    string HandlerCountry,
    string DogCallName,
    SizeCategory SizeCategory,
    float Rating,              // primary display value (after normalization)
    float Sigma,               // uncertainty (for ± display)
    int Rank,                  // position in current filtered leaderboard
    int? PrevRank,             // previous position (null if NEW)
    int RunCount,
    int Top3RunCount,
    bool IsProvisional,        // true if sigma >= threshold ("FEW RUNS")
    TierLabel? TierLabel       // Elite, Champion, Expert, Competitor
);

// Domain/Models/TeamDetailDto.cs
public record TeamDetailDto(
    int Id,
    string Slug,
    string HandlerName,
    string HandlerSlug,
    string HandlerCountry,
    string DogCallName,
    string? DogRegisteredName,
    string? DogBreed,
    SizeCategory SizeCategory,
    float Rating,
    float Sigma,
    float PrevRating,
    int RunCount,
    int FinishedRunCount,
    int Top3RunCount,
    bool IsActive,
    bool IsProvisional,
    TierLabel? TierLabel,
    // Computed stats (not stored on Team entity)
    float FinishedPct,         // FinishedRunCount / RunCount
    float Top3Pct,             // Top3RunCount / RunCount
    float? AvgRank             // average placement across non-eliminated runs
);

// Domain/Models/TeamResultDto.cs
/// <summary>
/// One row in the team's competition history table.
/// Combines data from RunResult + Run + Competition.
/// </summary>
public record TeamResultDto(
    string CompetitionSlug,
    string CompetitionName,
    DateOnly Date,
    SizeCategory SizeCategory,
    Discipline Discipline,
    bool IsTeamRound,
    int? Rank,                 // null if eliminated
    int? Faults,
    float? TimeFaults,
    float? Time,
    float? Speed,
    bool Eliminated
);

// Domain/Models/HandlerDetailDto.cs
public record HandlerDetailDto(
    int Id,
    string Slug,
    string Name,
    string Country,
    IReadOnlyList<HandlerTeamSummaryDto> Teams
);

// Domain/Models/HandlerTeamSummaryDto.cs
public record HandlerTeamSummaryDto(
    string TeamSlug,
    string DogCallName,
    string? DogBreed,
    SizeCategory SizeCategory,
    float Rating,
    float PeakRating, // highest rating ever achieved
    int RunCount,
    bool IsActive,
    TierLabel? TierLabel
);

// Domain/Models/CompetitionDetailDto.cs
public record CompetitionDetailDto(
    int Id,
    string Slug,
    string Name,
    DateOnly Date,
    DateOnly? EndDate,
    string? Country,
    string? Location,
    int Tier,
    string? Organization,      // e.g., "FCI", "AKC", "WAO" — null defaults to FCI
    int RunCount,
    int ParticipantCount       // distinct teams across all runs
);

// Domain/Models/RunSummaryDto.cs
/// <summary>
/// One row in the competition's run list (GET /api/competitions/{slug}/runs).
/// </summary>
public record RunSummaryDto(
    string RoundKey,
    DateOnly Date,
    SizeCategory SizeCategory,
    string? OriginalSizeCategory, // original source category (e.g., "20 inch", "Small") — null for FCI
    Discipline Discipline,
    bool IsTeamRound,
    int ResultCount             // number of participating teams
);

// Domain/Models/RunResultDto.cs
/// <summary>
/// One row in a run's results table (GET /api/competitions/{slug}/runs/{roundKey}/results).
/// </summary>
public record RunResultDto(
    int? Rank,
    string TeamSlug,
    string HandlerName,
    string HandlerCountry,
    string DogCallName,
    int? Faults,
    int? Refusals,
    float? TimeFaults,
    float? TotalFaults,
    float? Time,
    float? Speed,
    bool Eliminated
);

// Domain/Models/CountryRankingDto.cs
/// <summary>
/// One row in the country ranking table.
/// Country Score = average Rating of top N active non-provisional teams.
/// </summary>
public record CountryRankingDto(
    string CountryCode,          // ISO 3166-1 alpha-3 (e.g., "CZE")
    string CountryName,          // Human-readable (e.g., "Czech Republic")
    int Rank,                    // Position in country ranking
    float CountryScore,          // Average rating of top N teams
    int QualifiedTeamCount,      // Total active non-provisional teams from this country
    bool IsProvisional,          // true if teams < CountryTopN
    int EliteCount,              // Teams with Elite tier label
    int ChampionCount,
    int ExpertCount,
    int CompetitorCount,
    string BestTeamSlug,         // Slug of highest-rated team
    string BestTeamName,         // "Handler & Dog"
    float BestTeamRating         // Rating of the highest-rated team
);

// Domain/Models/CountryDetailDto.cs
/// <summary>
/// Detail view for a single country with its top teams.
/// </summary>
public record CountryDetailDto(
    string CountryCode,
    string CountryName,
    float CountryScore,
    int Rank,
    int QualifiedTeamCount,
    bool IsProvisional,
    int EliteCount,
    int ChampionCount,
    int ExpertCount,
    int CompetitorCount,
    int SCount,                  // Qualified teams in Small
    int MCount,                  // Qualified teams in Medium
    int ICount,                  // Qualified teams in Intermediate
    int LCount,                  // Qualified teams in Large
    IReadOnlyList<CountryTeamDto> TopTeams  // Top N teams used for score calculation
);

// Domain/Models/CountryTeamDto.cs
/// <summary>
/// A team entry within a country detail view.
/// </summary>
public record CountryTeamDto(
    string TeamSlug,
    string HandlerName,
    string DogCallName,
    SizeCategory SizeCategory,
    float Rating,
    TierLabel? TierLabel
);
```

## 4. External integrations

| System | Protocol | Direction | Auth | Purpose |
|--------|----------|-----------|------|---------|
| None in MVP | — | — | — | All data is imported via CSV files through the CLI tool |

**Future (Phase 3):** Scrapers/API integrations with competition management platforms for automated data import.

## 5. API outline

All endpoints are read-only in MVP. No authentication required.

Base path: `/api`

### Rankings

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/rankings` | Paginated leaderboard (`PagedResult<TeamRankingDto>`). Query params: `size` (required: S/M/I/L), `country` (optional), `search` (optional), `page`, `pageSize`. Only active teams (meeting min run threshold) are included |
| GET | `/api/rankings/summary` | Global summary stats (`RankingSummary`): total qualified teams, competitions, runs |

### Teams

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/teams/{slug}` | Team profile (`TeamDetailDto`): rating, computed stats (finished %, top3 %, avg rank), handler+dog info. **Returns both active and inactive teams** — inactive teams are viewable but marked with `isActive: false` |
| GET | `/api/teams/{slug}/history` | Rating progression snapshots for chart (`IReadOnlyList<RatingSnapshot>`). Note: `Rating` in snapshots uses the normalization parameters from the most recent recalculation, not the historical values at each point in time |
| GET | `/api/teams/{slug}/results` | Paginated competition results for this team (`PagedResult<TeamResultDto>`). Each row combines RunResult + Run + Competition data. Ordered by date descending |

### Handlers

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/handlers/{slug}` | Handler profile (`HandlerDetailDto`): all teams with peak ratings, career stats |
| GET | `/api/handlers/{slug}/teams` | List of handler's teams with ratings (`IReadOnlyList<HandlerTeamSummaryDto>`) |

### Countries

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/countries` | Country ranking list (`IReadOnlyList<CountryRankingDto>`), sorted by CountryScore descending. Only countries meeting minimum team threshold are included |
| GET | `/api/countries/{countryCode}` | Country detail (`CountryDetailDto`) with top N teams and category breakdown. Returns 404 if country has no teams in the dataset |

### Competitions

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/competitions` | Paginated competition list (`PagedResult<CompetitionDetailDto>`). Query params: `year`, `tier`, `country`, `search`, `page`, `pageSize` |
| GET | `/api/competitions/{slug}` | Competition detail with metadata (`CompetitionDetailDto`) |
| GET | `/api/competitions/{slug}/runs` | Flat list of all runs for a competition (`IReadOnlyList<RunSummaryDto>`), ordered by date → size → discipline. The UI groups by date/size/discipline for display |
| GET | `/api/competitions/{slug}/runs/{roundKey}/results` | Full results table for a specific run (`IReadOnlyList<RunResultDto>`). The `roundKey` is resolved server-side to a Run ID via `IRunRepository.GetByCompetitionAndRoundKeyAsync` |

### Search

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/search` | Global search across teams, handlers, and competitions. Query params: `q` (required, min 2 chars), `limit` (default 10). Returns mixed results grouped by type — used for the search/autocomplete bar in the UI. Each result includes `type` (team/handler/competition), `slug`, `displayName`, and `subtitle` (e.g., country, rating) |

### Response format

All responses are JSON. Standard envelope:

```jsonc
// Success (single item)
{
  "name": "AWC 2024",
  "date": "2024-10-03",
  // ...
}

// Success (list)
{
  "items": [...],
  "totalCount": 342,
  "page": 1,
  "pageSize": 50
}

// Error
{
  "error": "Competition not found",
  "status": 404
}
```

HTTP status codes: `200` (success), `400` (bad request / invalid params), `404` (not found).

## 6. CLI commands

The CLI tool (`adw-cli`) provides all admin operations. It connects directly to the database via `Service` layer (no HTTP).

### Import and rating

| Command | Description |
|---------|-------------|
| `import <file> --competition <slug> --name <name> --date <date> --tier <tier> [--country <cc>] [--location <loc>] [--end-date <date>] [--recalculate]` | Import competition results from CSV. `--recalculate` triggers full rating recalculation after successful import |
| `recalculate` | Full rating recalculation for all teams (all runs in live window, all sizes) |
| `seed-config` | Create the default `RatingConfiguration` row (idempotent — skips if active config exists). Run once after initial DB setup |

### Competitions

| Command | Description |
|---------|-------------|
| `list competitions [--year <year>] [--tier <tier>]` | List all imported competitions with date, tier, run count |
| `delete competition <slug>` | Delete a competition and cascade to its runs/results (with confirmation prompt) |

### Handlers

| Command | Description |
|---------|-------------|
| `list handlers --search <query> [--country <cc>] [--limit <n>]` | Search handlers by name (uses normalized matching). Shows ID, name, country, team count |
| `show handler <id>` | Show handler detail: name, country, slug, all teams (with dog + rating), all aliases |
| `update handler <id> --country <cc>` | Update a handler's country code (ISO 3166-1 alpha-3) |
| `merge handler <source-id> <target-id>` | Merge two handler records. Reassigns all teams and aliases from source to target, deletes source. Shows preview before confirmation |

### Dogs

| Command | Description |
|---------|-------------|
| `list dogs --search <query> [--size <S\|M\|I\|L>] [--limit <n>]` | Search dogs by call name (uses normalized matching). Shows ID, call name, registered name, size, team count |
| `show dog <id>` | Show dog detail: call name, registered name, breed, size, all teams (with handler + rating), all aliases |
| `merge dog <source-id> <target-id>` | Merge two dog records (same size category only). Reassigns teams and aliases, deletes source. Shows preview before confirmation |

### Aliases

| Command | Description |
|---------|-------------|
| `list aliases handler <handler-id>` | List all aliases for a handler |
| `list aliases dog <dog-id>` | List all aliases for a dog |
| `add alias handler <handler-id> <alias-name>` | Manually add a handler alias (source = Manual) |
| `add alias dog <dog-id> <alias-name> --type <CallName\|RegisteredName>` | Manually add a dog alias (source = Manual) |

### Import logs

| Command | Description |
|---------|-------------|
| `list imports [--limit <n>]` | Show recent import logs with status, row count, new entity counts |

### Common flags

All commands support:
- `--connection <conn-string>` — override the DB connection string (default: from `appsettings.json`)
- `--verbose` — detailed output (SQL queries, identity resolution decisions)
- `--dry-run` — (on `import`, `merge`, `delete`) show what would happen without making changes

## 7. Web pages (Blazor SSR)

All pages use Static SSR with Enhanced Navigation. No SignalR, no WebSocket.

| Route | Component | Description |
|-------|-----------|-------------|
| `/` | `Home` | Landing page with summary stats and links |
| `/rankings` | `Rankings` | Leaderboard with size tabs, country filter, name search, pagination |
| `/teams/{slug}` | `TeamProfile` | Bio card, rating chart, competition history, stats |
| `/handlers/{slug}` | `HandlerProfile` | All dogs/teams, career overview. Dog selector for run history + rating chart |
| `/countries` | `CountryRanking` | Country leaderboard with score, medal table, best team |
| `/countries/{code}` | `CountryDetail` | Country profile with top N teams and category breakdown |
| `/competitions` | `CompetitionList` | Chronological list with year/tier/country filters |
| `/competitions/{slug}` | `CompetitionDetail` | Full results grouped by day → size → discipline |

### SEO

- All pages are server-rendered HTML (Static SSR) — fully crawlable.
- Clean URLs with slugs (e.g., `/teams/john-smith-rex`, `/competitions/awc2024`).
- Open Graph meta tags on team/handler profiles for social media sharing.
- `<title>` and `<meta description>` set per page.

### Enhanced Navigation

Blazor Enhanced Navigation intercepts link clicks and form submissions, fetches the new page via `fetch()`, and patches the DOM — no full page reload. This provides SPA-like UX while keeping pure server-rendered HTML.

Filtering and pagination use query parameters (e.g., `/rankings?size=L&country=CZE&page=2`). Each filter change triggers an Enhanced Navigation request to the server.

## 8. Deployment and runtime

### Hosting (VPS)

- **Server**: Windows VPS (e.g., Hetzner Cloud, any provider with Windows Server)
- **OS**: Windows Server 2022+
- **Runtime**: .NET 10 (self-contained publish or runtime install via `dotnet-install.ps1`)
- **Database**: SQL Server 2022 (native install on the same VPS)
- **Reverse proxy**: IIS 10 with Application Request Routing (ARR) + URL Rewrite (automatic HTTPS via Let's Encrypt with `win-acme`)
- **Process management**: IIS application pools (Api and Web as separate sites) or Windows Services

### Deployment topology

```
                    ┌──────────────────────────────────────────────┐
                    │              Windows Server VPS               │
                    │                                              │
[Internet] ──HTTPS──► [IIS :443]                                  │
                    │   ├── /api/*  → [AdwRating.Api :5001]        │
                    │   └── /*      → [AdwRating.Web :5000]        │
                    │                                              │
                    │   [SQL Server :1433] (native)                 │
                    │                                              │
                    │   [adw-cli] (run manually via RDP / SSH)      │
                    └──────────────────────────────────────────────┘
```

- **IIS** handles TLS termination (via `win-acme` for Let's Encrypt certificates) and reverse proxying via ARR + URL Rewrite.
- **Api** and **Web** run as separate IIS sites (out-of-process Kestrel behind IIS), or as Windows Services for simpler management.
- **Web** calls **Api** via `http://localhost:5001/api/` (no TLS overhead for internal calls).
- **CLI** is run manually via RDP or SSH (OpenSSH server built into Windows Server).
- **SQL Server** runs natively on Windows (no Docker needed).

### CI/CD pipeline (GitHub Actions)

```
[Push to main / PR]
  → dotnet restore
  → dotnet build
  → dotnet test (unit + integration)
  → [On main only] dotnet publish -r win-x64
  → [On main only] Deploy to VPS via SSH + scp (or rsync via WSL)
  → [On main only] Restart IIS sites (appcmd recycle)
```

### Environments

| Environment | URL | Purpose |
|-------------|-----|---------|
| Development | `https://localhost:5000` / `:5001` | Local development (dotnet run) |
| Production | `https://rating.agilitydogsworld.com` (TBD) | Live site on VPS |

### Configuration

- **appsettings.json** — non-sensitive defaults (pagination sizes, display settings)
- **appsettings.Production.json** — production overrides (not in git)
- **Environment variables** — connection strings, secrets (set in IIS site configuration or Windows environment)
- **User secrets** — local development secrets (`dotnet user-secrets`)

## 9. Review log

Issues identified during specification review and their resolutions:

| # | Issue | Resolution |
|---|-------|------------|
| 1 | Rating engine: `openskill.net` or custom? | Use **openskill.net** — matches Python `openskill` library. Custom logic (sigma decay, display scaling, podium boost) lives in Service layer. |
| 2 | .NET version | **.NET 10** — confirmed by project owner. |
| 3 | `RankingFilter.Size` was nullable but API says size is required | Made **non-nullable**. Rankings are always per size category. |
| 4 | `IRankingService` referenced in request lifecycle but never defined | **Added** with `GetRankingsAsync` + `GetSummaryAsync`. |
| 5 | No way to search handlers/dogs directly | **Added** `GET /api/search` endpoint + `ISearchService` interface. |
| 6 | `IRunResultRepository.GetByTeamIdAsync` date filter joins through Run | **Documented** in interface docstring — implementation joins via `Run.Date` navigation property. |
| 7 | Competition runs endpoint: grouped or flat response? | **Flat list** ordered by date/size/discipline. UI groups for display. Simpler API contract. |
| 8 | ApiClient: typed vs generated? | **Manual typed HttpClient wrapper** for MVP. Described approach in section 2. Revisit in Phase 2. |
| 9 | Handler country update only via import? | **Added** `update handler` CLI command for manual country correction. Import still updates automatically. |
| 10 | Missing `ITeamRepository.GetByHandlerIdAsync` | **Added** — needed for handler profile page. |
| 11 | Missing `ITeamRepository.GetAllAsync` | **Added** — needed for full rating recalculation. |
| 12 | Missing `IRunRepository.GetAllInWindowAsync` | **Added** — needed for rating recalculation (runs in time window). |
| 13 | Missing `IRunRepository.GetByCompetitionAndRoundKeyAsync` | **Added** — API endpoint uses `roundKey`, repo only had ID lookup. |
| 14 | Web↔Api communication unclear | **Clarified** — Web reads `ApiBaseUrl` from config, injects into `AdwRatingApiClient` via DI. Deployed side by side on same VPS. |
| 15 | `Data.SqlServer` naming | **Renamed** to `Data.Mssql` throughout (including CLAUDE.md). |
| 16 | No DTO definitions — API contract incomplete | **Added** full DTO definitions section: `TeamRankingDto`, `TeamDetailDto`, `TeamResultDto`, `HandlerDetailDto`, `HandlerTeamSummaryDto`, `CompetitionDetailDto`, `RunSummaryDto`, `RunResultDto`. |
| 17 | No service interfaces for team/handler profiles | **Added** `ITeamProfileService` (detail + results) and `IHandlerProfileService` (detail with peak ratings). Controllers stay thin. |
| 18 | `ISearchService` has no repository support | **Added** `SearchAsync` to `IDogRepository`. `ISearchService` implementation composes results from `IHandlerRepository.SearchAsync`, `IDogRepository.SearchAsync` (via teams), and `ICompetitionRepository.GetListAsync`. |
| 19 | `RatingSnapshot.Rating` uses final normalization params, not historical | **Documented** in API endpoint description for `/api/teams/{slug}/history`. Design choice: snapshots use normalization params from most recent recalculation. |
| 20 | Missing batch loading for rating recalculation (N+1 risk) | **Added** `IRunResultRepository.GetByRunIdsAsync` for batch loading results across runs. |
| 21 | `CompetitionMetadata.Tier` had no validation note | **Added** docstring: Tier must be 1 (major) or 2 (standard). |
| 22 | Inactive teams not explicitly documented in API | **Clarified** `GET /api/teams/{slug}` returns both active and inactive teams. Rankings endpoint only returns active teams. |
| 23 | Deployment target was Linux, should be Windows Server | **Changed** to Windows Server: IIS (ARR + URL Rewrite) instead of Caddy, native SQL Server instead of Docker, Windows Services / IIS app pools instead of systemd, `win-acme` for Let's Encrypt. |
