# Implementation Plan

## Principles

- **Small tasks**: Each task = 1-3 files, max 15-30 min agent work
- **Correct ordering**: Foundation → Data → Import → Admin CLI → Rating → API → Web
- **Data quality first**: Import + admin tools (inspect, merge, fix) come before rating engine and web UI, enabling early iteration on data quality
- **Explicit dependencies**: Prerequisites clearly stated
- **Test-driven completion**: Every task must pass tests before moving on

## Task Completion Checklist

**Before marking any task as complete, you MUST:**

1. **Build passes**: `dotnet build` succeeds with no errors
2. **Write tests**: Create appropriate tests for the new code
3. **Run tests**: All tests pass (not just new ones)
4. **Commit**: Commit the changes with a descriptive message

**If tests fail:**
- Fix the issue before proceeding
- Do NOT skip to the next task
- Re-run all tests after fix

## Testing Guidelines

| Task type | Required tests |
|-----------|----------------|
| Entity + DB mapping | Unit test for validation, integration test for persistence |
| Repository | Integration test with real DB |
| Service | Unit test with mocked dependencies |
| API Controller | Integration test for endpoints |
| API Client | Unit test with mocked HTTP |
| UI Component/Page | E2E test for key flows |

**Test naming convention**: `[MethodName]_[Scenario]_[ExpectedResult]`

---

## MVP Phases

### Phase 1 — Foundation

**Goal**: Solution structure, domain model, database schema, test infrastructure.

- [x] **1.1** Create solution and project structure *(done — uses `.slnx` format for .NET 10)*
  - Create `AdwRating.sln` with all 7 `src/` projects and 3 `tests/` projects. Set up project references per dependency rules in `04-architecture-and-interfaces.md` section 2. Add `.editorconfig`, `global.json` (.NET 10), `Directory.Build.props`.
  - Files: `AdwRating.sln`, all `.csproj` files, `global.json`, `Directory.Build.props`
  - Dependencies: none
  - Tests: `dotnet build` passes
  - **Completion gates**: build

- [x] **1.2** Domain enums
  - Create all enums: `SizeCategory` (S, M, I, L), `Discipline` (Agility, Jumping, Final), `TierLabel` (Elite, Champion, Expert, Competitor), `AliasSource` (Manual, Import, FuzzyMatch), `DogAliasType` (CallName, RegisteredName), `ImportStatus` (Success, Rejected, PartialWarning).
  - Files: `Domain/Enums/*.cs` (one file per enum or single `Enums.cs`)
  - Dependencies: 1.1
  - Tests: none (trivial enum definitions)
  - **Completion gates**: build

- [x] **1.3a** Domain entity — Handler
  - Create `Handler` entity class with all fields from `03-domain-and-data.md` section 2: Id, Name, NormalizedName, Country, Slug.
  - Files: `Domain/Entities/Handler.cs`
  - Dependencies: 1.2
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.3b** Domain entity — Dog
  - Create `Dog` entity class with all fields from `03-domain-and-data.md` section 2: Id, CallName, NormalizedCallName, RegisteredName, Breed, SizeCategory, SizeCategoryOverride.
  - Files: `Domain/Entities/Dog.cs`
  - Dependencies: 1.2
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.3c** Domain entity — Team
  - Create `Team` entity class with all fields from `03-domain-and-data.md` section 2: Id, HandlerId, DogId, Slug, Mu, Sigma, Rating, PrevMu, PrevSigma, PrevRating, RunCount, FinishedRunCount, Top3RunCount, IsActive, IsProvisional, TierLabel, PeakRating. Include navigation properties to Handler and Dog. Rating field defaults (Mu, Sigma) come from `RatingConfiguration.Mu0` and `RatingConfiguration.Sigma0` at runtime — the entity itself has no hardcoded defaults.
  - Files: `Domain/Entities/Team.cs`
  - Dependencies: 1.3a, 1.3b
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.3d** Domain helpers — SlugHelper and NameNormalizer (basic) *(done — includes diacritics stripping with Unicode FormD + special char mappings)*
  - Create **basic** slug generation helper and name normalization helper (static utility methods). Just enough for entity creation: slug from name (lowercase, replace spaces with hyphens, remove non-alphanumeric), name normalization (lowercase, trim). Full production implementation with diacritics stripping, name reordering is done in task 3.1.
  - Files: `Domain/Helpers/SlugHelper.cs`, `Domain/Helpers/NameNormalizer.cs`
  - Dependencies: 1.1
  - Tests: Unit tests for basic `SlugHelper` (simple name → slug) and `NameNormalizer` (lowercase, trim whitespace)
  - **Completion gates**: build | tests

- [x] **1.4a** Domain entity — Competition
  - Create `Competition` entity class with all fields from `03-domain-and-data.md`: Id, Slug, Name, Date, EndDate, Country, Location, Tier, Organization. Include navigation property to Runs collection.
  - Files: `Domain/Entities/Competition.cs`
  - Dependencies: 1.2
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.4b** Domain entity — Run
  - Create `Run` entity class with all fields from `03-domain-and-data.md`: Id, CompetitionId, Date, RunNumber, RoundKey, SizeCategory, Discipline, IsTeamRound, Judge, Sct, Mct, CourseLength, OriginalSizeCategory. Include navigation properties.
  - Files: `Domain/Entities/Run.cs`
  - Dependencies: 1.2, 1.4a
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.4c** Domain entity — RunResult
  - Create `RunResult` entity class with all fields from `03-domain-and-data.md`: Id, RunId, TeamId, Rank, Faults, Refusals, TimeFaults, TotalFaults, Time, Speed, Eliminated, StartNo. Include navigation properties.
  - Files: `Domain/Entities/RunResult.cs`
  - Dependencies: 1.2, 1.3c, 1.4b
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.5a** Domain entities — HandlerAlias, DogAlias
  - Create `HandlerAlias` entity (Id, AliasName, CanonicalHandlerId, Source, CreatedAt) and `DogAlias` entity (Id, AliasName, CanonicalDogId, AliasType, Source, CreatedAt) per `03-domain-and-data.md`.
  - Files: `Domain/Entities/HandlerAlias.cs`, `Domain/Entities/DogAlias.cs`
  - Dependencies: 1.2
  - Tests: none (simple POCOs)
  - **Completion gates**: build

- [x] **1.5b** Domain entity — ImportLog
  - Create `ImportLog` entity with all fields from `03-domain-and-data.md`: Id, CompetitionId, FileName, ImportedAt, Status, RowCount, NewHandlersCount, NewDogsCount, NewTeamsCount, Errors, Warnings.
  - Files: `Domain/Entities/ImportLog.cs`
  - Dependencies: 1.2
  - Tests: none (simple POCO)
  - **Completion gates**: build

- [x] **1.5c** Domain entities — RatingSnapshot, RatingConfiguration
  - Create `RatingSnapshot` entity (Id, TeamId, RunResultId, CompetitionId, Date, Mu, Sigma, Rating) and `RatingConfiguration` entity with all fields from `03-domain-and-data.md` (all rating parameters including `Mu0`, `Sigma0`, with defaults from `08-rating-rules.md` section 8).
  - Files: `Domain/Entities/RatingSnapshot.cs`, `Domain/Entities/RatingConfiguration.cs`
  - Dependencies: 1.2
  - Tests: none (simple POCOs)
  - **Completion gates**: build

- [x] **1.6a** Domain models — PagedResult, filters, ImportResult, CompetitionMetadata
  - Create shared models: `PagedResult<T>`, `RankingFilter`, `CompetitionFilter`, `ImportResult`, `CompetitionMetadata`, `RankingSummary`, `SearchResult`. All definitions are in `04-architecture-and-interfaces.md` section 3 (Common types).
  - Files: `Domain/Models/PagedResult.cs`, `Domain/Models/RankingFilter.cs`, `Domain/Models/CompetitionFilter.cs`, `Domain/Models/ImportResult.cs`, `Domain/Models/CompetitionMetadata.cs`, `Domain/Models/RankingSummary.cs`, `Domain/Models/SearchResult.cs`
  - Dependencies: 1.2
  - Tests: none (record definitions)
  - **Completion gates**: build

- [x] **1.6b** Domain DTOs — TeamRankingDto, TeamDetailDto, TeamResultDto
  - Create DTOs used by Rankings and Team profile APIs. Definitions in `04-architecture-and-interfaces.md` section 3 (API DTOs).
  - Files: `Domain/Models/TeamRankingDto.cs`, `Domain/Models/TeamDetailDto.cs`, `Domain/Models/TeamResultDto.cs`
  - Dependencies: 1.2
  - Tests: none (record definitions)
  - **Completion gates**: build

- [x] **1.6c** Domain DTOs — CompetitionDetailDto, HandlerDetailDto, HandlerTeamSummaryDto
  - Create remaining MVP DTOs. Definitions in `04-architecture-and-interfaces.md` section 3 (API DTOs).
  - Files: `Domain/Models/CompetitionDetailDto.cs`, `Domain/Models/HandlerDetailDto.cs`, `Domain/Models/HandlerTeamSummaryDto.cs`
  - Dependencies: 1.2
  - Tests: none (record definitions)
  - **Completion gates**: build

- [x] **1.7a** Domain interfaces — Handler, Dog, Team repositories
  - Create `IHandlerRepository`, `IDogRepository`, `ITeamRepository` interfaces from `04-architecture-and-interfaces.md` section 3 (Repository interfaces).
  - Files: `Domain/Interfaces/IHandlerRepository.cs`, `Domain/Interfaces/IDogRepository.cs`, `Domain/Interfaces/ITeamRepository.cs`
  - Dependencies: 1.3c, 1.6a
  - Tests: none (interface definitions)
  - **Completion gates**: build

- [x] **1.7b** Domain interfaces — Competition, Run, RunResult repositories
  - Create `ICompetitionRepository`, `IRunRepository`, `IRunResultRepository` interfaces from `04-architecture-and-interfaces.md` section 3.
  - Files: `Domain/Interfaces/ICompetitionRepository.cs`, `Domain/Interfaces/IRunRepository.cs`, `Domain/Interfaces/IRunResultRepository.cs`
  - Dependencies: 1.4c, 1.6a
  - Tests: none (interface definitions)
  - **Completion gates**: build

- [x] **1.7c** Domain interfaces — Alias, ImportLog, RatingSnapshot, RatingConfiguration repositories
  - Create `IHandlerAliasRepository`, `IDogAliasRepository`, `IRatingSnapshotRepository`, `IRatingConfigurationRepository`, `IImportLogRepository` from `04-architecture-and-interfaces.md` section 3.
  - Files: `Domain/Interfaces/IHandlerAliasRepository.cs`, `Domain/Interfaces/IDogAliasRepository.cs`, `Domain/Interfaces/IRatingSnapshotRepository.cs`, `Domain/Interfaces/IRatingConfigurationRepository.cs`, `Domain/Interfaces/IImportLogRepository.cs`
  - Dependencies: 1.5a, 1.5b, 1.5c
  - Tests: none (interface definitions)
  - **Completion gates**: build

- [x] **1.8a** Domain interfaces — Import, Rating, Ranking services
  - Create `IImportService`, `IRatingService`, `IRankingService` from `04-architecture-and-interfaces.md` section 3 (Service interfaces).
  - Files: `Domain/Interfaces/IImportService.cs`, `Domain/Interfaces/IRatingService.cs`, `Domain/Interfaces/IRankingService.cs`
  - Dependencies: 1.6a
  - Tests: none (interface definitions)
  - **Completion gates**: build

- [x] **1.8b** Domain interfaces — Search, IdentityResolution, TeamProfile, Merge services
  - Create `ISearchService`, `IIdentityResolutionService`, `ITeamProfileService`, `IMergeService` from `04-architecture-and-interfaces.md` section 3. Exclude `IHandlerProfileService` (Phase 1.5), `ICountryRankingService` and `IJudgeProfileService` (Phase 2).
  - Files: `Domain/Interfaces/ISearchService.cs`, `Domain/Interfaces/IIdentityResolutionService.cs`, `Domain/Interfaces/ITeamProfileService.cs`, `Domain/Interfaces/IMergeService.cs`
  - Dependencies: 1.6a, 1.6b
  - Tests: none (interface definitions)
  - **Completion gates**: build

- [x] **1.9a** Data.Mssql — DbContext
  - Create `AppDbContext` with `DbSet<>` for all MVP entities (Handler, Dog, Team, Competition, Run, RunResult, HandlerAlias, DogAlias, ImportLog, RatingSnapshot, RatingConfiguration).
  - Files: `Data.Mssql/AppDbContext.cs`
  - Dependencies: 1.3c, 1.4c, 1.5a, 1.5b, 1.5c
  - Tests: none
  - **Completion gates**: build

- [x] **1.9b** Data.Mssql — EF config for Handler
  - Create `IEntityTypeConfiguration<Handler>` with proper indexes, unique constraints on Slug, NormalizedName+Country, field lengths.
  - Files: `Data.Mssql/Configurations/HandlerConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.9c** Data.Mssql — EF config for Dog
  - Create `IEntityTypeConfiguration<Dog>` with proper indexes, field lengths, enum conversion for SizeCategory/SizeCategoryOverride.
  - Files: `Data.Mssql/Configurations/DogConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.9d** Data.Mssql — EF config for Team
  - Create `IEntityTypeConfiguration<Team>` with unique constraint on (HandlerId, DogId), unique Slug, relationships to Handler and Dog, enum conversion for TierLabel.
  - Files: `Data.Mssql/Configurations/TeamConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.10a** Data.Mssql — EF config for Competition
  - Create `IEntityTypeConfiguration<Competition>` with unique constraint on Slug, field lengths, cascade delete to Runs.
  - Files: `Data.Mssql/Configurations/CompetitionConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.10b** Data.Mssql — EF config for Run
  - Create `IEntityTypeConfiguration<Run>` with unique constraint on (CompetitionId, RoundKey), enum conversions, cascade delete to RunResults.
  - Files: `Data.Mssql/Configurations/RunConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.10c** Data.Mssql — EF config for RunResult
  - Create `IEntityTypeConfiguration<RunResult>` with unique constraint on (RunId, TeamId), relationships.
  - Files: `Data.Mssql/Configurations/RunResultConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.11a** Data.Mssql — EF configs for HandlerAlias, DogAlias
  - Create EF configurations for HandlerAlias (unique AliasName) and DogAlias (unique AliasName+AliasType).
  - Files: `Data.Mssql/Configurations/HandlerAliasConfiguration.cs`, `Data.Mssql/Configurations/DogAliasConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.11b** Data.Mssql — EF configs for ImportLog, RatingSnapshot, RatingConfiguration
  - Create EF configurations for ImportLog, RatingSnapshot (unique TeamId+RunResultId), RatingConfiguration.
  - Files: `Data.Mssql/Configurations/ImportLogConfiguration.cs`, `Data.Mssql/Configurations/RatingSnapshotConfiguration.cs`, `Data.Mssql/Configurations/RatingConfigurationConfiguration.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.11c** Data.Mssql — DI registration
  - Add DI registration extension method `AddDataMssql(this IServiceCollection, string connectionString)` that registers `AppDbContext` and all repository implementations (stubs for now — actual implementations come in Phase 2).
  - Files: `Data.Mssql/ServiceCollectionExtensions.cs`
  - Dependencies: 1.9a
  - Tests: none
  - **Completion gates**: build

- [x] **1.12** Data.Mssql — Initial migration
  - Generate initial EF Core migration capturing the full MVP schema. Verify migration SQL looks correct.
  - Files: `Data.Mssql/Migrations/*`
  - Dependencies: 1.9a, 1.9b, 1.9c, 1.9d, 1.10a, 1.10b, 1.10c, 1.11a, 1.11b
  - Tests: none (migration is verified by applying to a test DB in integration tests)
  - **Completion gates**: build

- [x] **1.13a** Test infrastructure — Unit test project + entity builders *(done — uses NUnit instead of xUnit)*
  - Set up `tests/AdwRating.Tests/` with xUnit. Add shared test entity builders — `HandlerBuilder`, `DogBuilder`, `TeamBuilder`, `CompetitionBuilder`, `RunBuilder`, `RunResultBuilder` — fluent API for creating test entities with sensible defaults (e.g., `new HandlerBuilder().WithName("John Smith").WithCountry("GBR").Build()`).
  - Files: `tests/AdwRating.Tests/` (project + `Builders/*.cs`)
  - Dependencies: 1.12
  - Tests: One smoke test verifying builder creates valid entities
  - **Completion gates**: build | tests

- [x] **1.13b** Test infrastructure — Integration test project + database fixture *(done — uses NUnit + Testcontainers.MsSql instead of xUnit + LocalDB)*
  - Set up `tests/AdwRating.IntegrationTests/` with xUnit. Create `DatabaseFixture` (`IAsyncLifetime`): creates a SQL Server LocalDB database, applies migrations (`context.Database.MigrateAsync()`), provides a scoped `AppDbContext`. Each test class gets a fresh DB (or uses transactions with rollback).
  - Files: `tests/AdwRating.IntegrationTests/TestBase.cs`, `tests/AdwRating.IntegrationTests/DatabaseFixture.cs`
  - Dependencies: 1.12
  - Tests: One smoke test — DB created via migrations, a Handler can be inserted and retrieved
  - **Completion gates**: build | tests

- [x] **1.13c** Test infrastructure — E2E test project scaffold
  - Set up `tests/AdwRating.E2ETests/` with Playwright. No actual tests yet — just project structure and configuration.
  - Files: `tests/AdwRating.E2ETests/` (project scaffold)
  - Dependencies: 1.1
  - Tests: `dotnet build` passes
  - **Completion gates**: build

---

### Phase 2 — Repositories

**Goal**: Implement all repository classes with integration tests. This completes the data access layer.

- [x] **2.1a** HandlerRepository
  - Implement `IHandlerRepository` in Data.Mssql: CRUD, slug/name lookups, search (LIKE on NormalizedName), merge (reassign teams + aliases, delete source).
  - Files: `Data.Mssql/Repositories/HandlerRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create handler, find by normalized name, search, merge
  - **Completion gates**: build | tests

- [x] **2.1b** HandlerAliasRepository
  - Implement `IHandlerAliasRepository` in Data.Mssql: alias lookup by normalized name, create, get by handler ID.
  - Files: `Data.Mssql/Repositories/HandlerAliasRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create alias, lookup by name, get by handler ID
  - **Completion gates**: build | tests

- [x] **2.2a** DogRepository
  - Implement `IDogRepository` in Data.Mssql: CRUD, name+size lookup, search, merge (same-size only, reassign teams + aliases).
  - Files: `Data.Mssql/Repositories/DogRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create dog, find by name+size, search, merge
  - **Completion gates**: build | tests

- [x] **2.2b** DogAliasRepository
  - Implement `IDogAliasRepository` in Data.Mssql: alias+type lookup, create, get by dog ID.
  - Files: `Data.Mssql/Repositories/DogAliasRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create alias, lookup by name+type, get by dog ID
  - **Completion gates**: build | tests

- [x] **2.3** TeamRepository
  - Implement `ITeamRepository`: GetByHandlerAndDog, GetByHandlerId, GetRankedTeamsAsync (paginated, filtered by size/country/search, ordered by Rating desc, only active teams), GetAllAsync, CreateAsync, UpdateBatchAsync.
  - Files: `Data.Mssql/Repositories/TeamRepository.cs`
  - Dependencies: 2.1a, 2.2a
  - Tests: Integration tests — create team, ranked query with filters, batch update
  - **Completion gates**: build | tests

- [x] **2.4a** CompetitionRepository
  - Implement `ICompetitionRepository`: CRUD, slug lookup, paginated list with filters, cascade delete.
  - Files: `Data.Mssql/Repositories/CompetitionRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create competition, slug lookup, paginated list, cascade delete
  - **Completion gates**: build | tests

- [x] **2.4b** RunRepository
  - Implement `IRunRepository`: batch create, get by competition, get by roundKey, get all in window (for rating recalculation).
  - Files: `Data.Mssql/Repositories/RunRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — batch create runs, window query
  - **Completion gates**: build | tests

- [x] **2.4c** RunResultRepository
  - Implement `IRunResultRepository`: batch create, get by run, get by multiple runs (batch loading), get by team with date filter.
  - Files: `Data.Mssql/Repositories/RunResultRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — batch create results, get by run IDs, team results query
  - **Completion gates**: build | tests

- [x] **2.5a** RatingSnapshotRepository
  - Implement `IRatingSnapshotRepository`: get by team, replace all (delete all existing, insert new).
  - Files: `Data.Mssql/Repositories/RatingSnapshotRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — snapshot replace-all, get by team
  - **Completion gates**: build | tests

- [x] **2.5b** RatingConfigurationRepository
  - Implement `IRatingConfigurationRepository`: get active, create (ensure only one active at a time).
  - Files: `Data.Mssql/Repositories/RatingConfigurationRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create config, get active, only one active constraint
  - **Completion gates**: build | tests

- [x] **2.5c** ImportLogRepository
  - Implement `IImportLogRepository`: create, get recent (ordered by ImportedAt desc, limited).
  - Files: `Data.Mssql/Repositories/ImportLogRepository.cs`
  - Dependencies: Phase 1
  - Tests: Integration tests — create log, get recent with limit
  - **Completion gates**: build | tests

- [x] **2.6** Update Data.Mssql DI registration with all repositories
  - Update `ServiceCollectionExtensions.AddDataMssql()` to register all repository implementations from 2.1–2.5.
  - Files: `Data.Mssql/ServiceCollectionExtensions.cs`
  - Dependencies: 2.1a–2.5c
  - Tests: none (DI wiring verified by integration tests)
  - **Completion gates**: build

---

### Phase 3 — Import Pipeline

**Goal**: CSV import with identity resolution via CLI. After this phase, competition data can be loaded into the database.

- [x] **3.1a** NameNormalizer — full production implementation *(done: added "Last, First" reordering and typographic quote normalization)*
  - Extend `NameNormalizer` from task 1.3d with full production logic per `08-rating-rules.md` section 1.3: strip diacritics (e.g., "Kateřina Třičová" → "katerina tricova"), normalize whitespace, lowercase, unify "Last, First" ↔ "First Last" name order, normalize typographic quotes.
  - Files: `Domain/Helpers/NameNormalizer.cs`
  - Dependencies: 1.3d
  - Tests: Unit tests — diacritics stripping (Czech, German, French, Polish characters), "Last, First" → "First Last" reordering, typographic quote normalization, edge cases (empty string, null, double spaces)
  - **Completion gates**: build | tests

- [x] **3.1b** SlugHelper — full production implementation *(done: added edge-case tests)*
  - Extend `SlugHelper` from task 1.3d with full production logic: URL-safe slug from name (replace spaces with hyphens, remove non-alphanumeric except hyphens, collapse multiple hyphens), collision suffix (append `-2`, `-3` etc.). Pure function — collision checking done by the caller.
  - Files: `Domain/Helpers/SlugHelper.cs`
  - Dependencies: 1.3d
  - Tests: Unit tests — slug from name with special characters, slug collision suffix generation, diacritics in slugs
  - **Completion gates**: build | tests

- [x] **3.2a** Identity resolution — handler resolution
  - Implement handler resolution part of `IIdentityResolutionService` in Service project. `ResolveHandlerAsync(rawName, country)`: 1) Normalize name, 2) Check alias repo, 3) Check handler repo by normalized name+country, 4) Fuzzy match (Levenshtein ≤ 2, same country → create alias), 5) Create new handler. Log all resolution decisions.
  - Files: `Service/IdentityResolutionService.cs`
  - Dependencies: 3.1a, 2.1a, 2.1b
  - Tests: Unit tests with mocked repos — exact match, alias match, fuzzy match (creates alias), no match (creates new)
  - **Completion gates**: build | tests

- [x] **3.2b** Identity resolution — dog and team resolution
  - Add dog and team resolution to `IdentityResolutionService`. `ResolveDogAsync(rawDogName, breed, size)`: same pattern as handler (normalize, alias, exact, fuzzy, new). `ResolveTeamAsync(handlerId, dogId)`: lookup or create with default ratings.
  - Files: `Service/IdentityResolutionService.cs`
  - Dependencies: 3.2a, 2.2a, 2.2b, 2.3
  - Tests: Unit tests — dog exact/alias/fuzzy/new, breed updated on re-encounter, team created with correct defaults
  - **Completion gates**: build | tests
  - **Reference**: `08-rating-rules.md` section 1.3, `03-domain-and-data.md` section 2

- [x] **3.3a** CSV parsing — CsvResultParser and ImportRow
  - Create a CSV parser that reads competition result files into a structured `ImportRow` intermediate format. Handle: header row matching by column name, BOM markers, trailing commas, inconsistent line endings, quoted fields with commas, empty rows. CSV format is defined in the CSV Format Specification at the end of this document.
  - Files: `Service/Import/CsvResultParser.cs`, `Service/Import/ImportRow.cs`
  - Dependencies: 1.6a
  - Tests: Unit tests — valid CSV parses correctly, BOM handling, empty rows ignored, quoted fields with commas
  - **Completion gates**: build | tests

- [x] **3.3b** CSV validation
  - Add validation to `CsvResultParser`: required fields present, valid placement (positive integer or empty if eliminated), valid size categories, no duplicate (handler+dog) within a run. All-or-nothing — collect all errors at once.
  - Files: `Service/Import/CsvResultParser.cs`
  - Dependencies: 3.3a
  - Tests: Unit tests — missing required fields rejected, invalid placement rejected, duplicate team detected
  - **Completion gates**: build | tests

- [x] **3.3c** SizeCategoryMapper
  - Create `SizeCategoryMapper` for non-FCI organization size mapping based on the mapping table in `03-domain-and-data.md` section 3. Map AKC, USDAA, WAO, UKI, IFCS sizes to FCI S/M/I/L. Exclude AKC Preferred.
  - Files: `Service/Import/SizeCategoryMapper.cs`
  - Dependencies: 1.2
  - Tests: Unit tests — AKC "20 inch" → I, USDAA "22 inch" → L, WAO "500" → L, AKC Preferred → excluded, FCI passthrough
  - **Completion gates**: build | tests

- [x] **3.4** Import service
  - Implement `IImportService.ImportCompetitionAsync(filePath, slug, metadata)`. Orchestration: 1) Parse CSV, 2) Validate, 3) Create Competition, 4) Group by RoundKey → create Runs, 5) Resolve identities → create RunResults, 6) Write ImportLog, 7) Return ImportResult. Transactional — rolls back on failure. Reject duplicate slugs.
  - Files: `Service/ImportService.cs`
  - Dependencies: 3.2b, 3.3b, 3.3c, 2.4a, 2.4b, 2.4c, 2.5c
  - Tests: Unit tests with mocked repos — successful import, validation failure, duplicate slug rejected
  - **Completion gates**: build | tests

- [x] **3.5a** Service DI registration
  - Create `AddServices(this IServiceCollection)` extension method in Service project that registers all service implementations with their interfaces.
  - Files: `Service/ServiceCollectionExtensions.cs`
  - Dependencies: 3.4
  - Tests: none
  - **Completion gates**: build

- [x] **3.5b** CLI project setup and import command
  - Set up `AdwRating.Cli` with System.CommandLine. Wire DI (`AddDataMssql` + `AddServices`). Implement `import` command: reads CSV path + `CompetitionMetadata` from CLI args, calls `IImportService.ImportCompetitionAsync`, prints summary. Add `--connection`, `--verbose`, `--dry-run` global options. CLI syntax: `import <file> --competition <slug> --name <name> --date <date> --tier <1|2> [--country <cc>] [--location <loc>] [--end-date <date>] [--organization <org>]`.
  - Files: `Cli/Program.cs`, `Cli/Commands/ImportCommand.cs`
  - Dependencies: 3.5a, 1.11c
  - Tests: Unit test for command argument parsing
  - **Completion gates**: build | tests

- [x] **3.5c** CLI — seed-config command
  - Implement `seed-config` command: creates default `RatingConfiguration` if none exists. Use defaults from `08-rating-rules.md` section 8.
  - Files: `Cli/Commands/SeedConfigCommand.cs`
  - Dependencies: 3.5b
  - Tests: none (idempotent command, verified manually)
  - **Completion gates**: build

---

### Phase 4 — Admin CLI (Data Quality Tools)

**Goal**: Complete admin tooling so you can import data, inspect it, fix duplicates, and iterate on data quality — all before building the rating engine or web UI.

- [x] **4.1** MergeService
  - Implement `IMergeService` in Service — `MergeHandlersAsync` (reassign teams + aliases from source to target, delete source, handle slug conflicts), `MergeDogsAsync` (same-size validation, reassign teams + aliases, handle duplicate team merging).
  - Files: `Service/MergeService.cs`
  - Dependencies: 2.1a, 2.2a, 2.3
  - Tests: Unit tests — handler merge (teams reassigned, aliases moved), dog merge (same-size check, duplicate team handling)
  - **Completion gates**: build | tests

- [x] **4.2a** CLI — list commands
  - Implement: `list competitions`, `list handlers --search`, `list dogs --search`, `list imports`. Table-formatted console output.
  - Files: `Cli/Commands/ListCommands.cs`
  - Dependencies: 3.5b, Phase 2
  - Tests: Smoke test — commands parse correctly
  - **Completion gates**: build | tests

- [x] **4.2b** CLI — show and alias list commands
  - Implement: `show handler <id>`, `show dog <id>`, `list aliases handler <id>`, `list aliases dog <id>`.
  - Files: `Cli/Commands/ShowCommands.cs`
  - Dependencies: 3.5b, Phase 2
  - Tests: Smoke test — commands parse correctly
  - **Completion gates**: build | tests

- [x] **4.3a** CLI — merge commands
  - Implement `merge handler` and `merge dog` with confirmation prompt and `--dry-run`.
  - Files: `Cli/Commands/MergeCommands.cs`
  - Dependencies: 4.1, 3.5b
  - Tests: Unit test for confirmation prompt logic
  - **Completion gates**: build | tests

- [x] **4.3b** CLI — delete, update, add-alias commands
  - Implement: `delete competition` (with confirmation), `update handler --country`, `add alias handler`, `add alias dog`.
  - Files: `Cli/Commands/DeleteCommands.cs`, `Cli/Commands/UpdateCommands.cs`, `Cli/Commands/AliasCommands.cs`
  - Dependencies: 3.5b, Phase 2
  - Tests: Smoke test — commands parse correctly
  - **Completion gates**: build | tests

---

### Phase 5 — Rating Engine

**Goal**: Full rating recalculation from run data. After this phase, the system can compute and store ratings.

- [x] **5.1** OpenSkill integration — RatingEngine wrapper *(done — uses OpenSkillSharp 1.1.0, Tau=0 since we handle sigma decay ourselves)*
  - Add `openskill.net` NuGet package. Create a thin wrapper (`RatingEngine`) in Service that isolates the library: `CreateRating()` → initial (mu=25.0, sigma≈8.333), `ProcessRun(teams, ranks, weight)` → calls PlackettLuce model → returns updated (mu, sigma).
  - Files: `Service/Rating/RatingEngine.cs`
  - Dependencies: 1.1
  - Tests: Unit test — process a small run (5 teams), verify: winner's mu increases, loser's mu decreases, all sigmas decrease, weight > 1.0 produces larger changes
  - **Completion gates**: build | tests
  - **Reference**: `08-rating-rules.md` sections 3.1–3.3

- [x] **5.2** Rating recalculation — core loop
  - Implement `IRatingService.RecalculateAllAsync()` core loop per `08-rating-rules.md` sections 2–3. Load config, reset teams, compute cutoff, load runs in window, batch-load results, process each qualifying run (build rank list, apply weight, update mu/sigma, apply sigma decay, track counts). Save intermediate mu/sigma per team.
  - Files: `Service/Rating/RatingService.cs`
  - Dependencies: 5.1, 2.3, 2.4b, 2.4c, 2.5a, 2.5b
  - Tests: Unit tests with mocked repos — 2-3 competitions, verify: mu changes for winners/losers, sigma decreases, counts correct, runs below MinFieldSize skipped, eliminated get tied last rank
  - **Completion gates**: build | tests
  - **Reference**: `08-rating-rules.md` sections 2 and 3

- [x] **5.3a** Rating recalculation — display scaling and podium boost
  - Add display scaling pipeline to `RatingService`: base rating (`DISPLAY_BASE + DISPLAY_SCALE * (mu - RATING_SIGMA_MULTIPLIER * sigma)`), podium boost (quality factor from top3 percentage).
  - Files: `Service/Rating/RatingService.cs`
  - Dependencies: 5.2
  - Tests: Unit tests — verify display scaling with known inputs, podium boost (0% top3 → factor 0.85, 50% → 1.05)
  - **Completion gates**: build | tests
  - **Reference**: `08-rating-rules.md` sections 4.1, 4.2

- [x] **5.3b** Rating recalculation — cross-size normalization and tiers
  - Add cross-size normalization (z-score per size category → target mean 1500, std 150), IsActive/IsProvisional flags, tier labels (percentile-based: Elite top 2%, Champion top 10%, Expert top 30%), PeakRating tracking.
  - Files: `Service/Rating/RatingService.cs`
  - Dependencies: 5.3a
  - Tests: Unit tests — normalization produces mean≈1500 std≈150, tier labels at correct percentiles, PeakRating only increases, flags correct
  - **Completion gates**: build | tests
  - **Reference**: `08-rating-rules.md` sections 4.3, 5, 6

- [x] **5.3c** Rating recalculation — snapshots and persistence
  - Add RatingSnapshot generation (for each team+run, create snapshot with final normalization params) and final persistence (`UpdateBatchAsync` + `ReplaceAllAsync`). Apply normalization to PrevRating for consistent trend display.
  - Files: `Service/Rating/RatingService.cs`
  - Dependencies: 5.3b
  - Tests: Unit tests — snapshot count equals total run results processed, persistence called correctly
  - **Completion gates**: build | tests
  - **Reference**: `03-domain-and-data.md` RatingSnapshot rules

- [x] **5.4** CLI recalculate command
  - Add `recalculate` command to CLI that calls `IRatingService.RecalculateAllAsync()`. Print summary: teams processed, time elapsed, tier distribution.
  - Files: `Cli/Commands/RecalculateCommand.cs`
  - Dependencies: 5.3c, 3.5b
  - Tests: Smoke test — recalculate on empty DB completes without error
  - **Completion gates**: build | tests

---

### Phase 6 — API

**Goal**: REST API serving ranking, team, competition, and search data.

- [x] **6.1** API project setup *(done — Program.cs with DI, JSON, CORS, ProblemDetails, health check)*
  - Set up `AdwRating.Api` with `Program.cs`. Configure: DI (`AddDataMssql` + `AddServices`), JSON serialization (camelCase, enum as string, ignore null), CORS (allow Web origin), global exception handler (ProblemDetails), health check (`GET /health`).
  - Files: `Api/Program.cs`, `Api/appsettings.json`, `Api/appsettings.Development.json`
  - Dependencies: Phase 1 (DI), Phase 2 (repos)
  - Tests: Integration test — health endpoint returns 200
  - **Completion gates**: build | tests
  - **Reference**: `04-architecture-and-interfaces.md` section 5

- [x] **6.2a** RankingService *(done — delegates to repo, computes summary from repos)*
  - Implement `IRankingService` in Service: delegates to `ITeamRepository.GetRankedTeamsAsync` with mapping to `TeamRankingDto`, computes rank numbers and rank change from PrevRating. Implement `GetSummaryAsync`.
  - Files: `Service/RankingService.cs`
  - Dependencies: 2.3
  - Tests: Unit test — mapping, rank computation, rank change
  - **Completion gates**: build | tests

- [x] **6.2b** Rankings controller *(done — GET /api/rankings with size/country/search/pagination, GET /api/rankings/summary)*
  - Create `RankingsController` with `GET /api/rankings` (paginated, filtered) and `GET /api/rankings/summary`.
  - Files: `Api/Controllers/RankingsController.cs`
  - Dependencies: 6.1, 6.2a
  - Tests: Integration test — query with size filter, pagination, summary endpoint, verify camelCase JSON
  - **Completion gates**: build | tests

- [x] **6.3a** TeamProfileService *(done — loads team, computes stats, paginates results)*
  - Implement `ITeamProfileService` in Service: `GetBySlugAsync` (load team, compute FinishedPct/Top3Pct/AvgRank, map to `TeamDetailDto`), `GetResultsAsync` (paginated, join RunResult+Run+Competition, map to `TeamResultDto`).
  - Files: `Service/TeamProfileService.cs`
  - Dependencies: 2.3, 2.4a, 2.4c, 2.5a
  - Tests: Unit test — stat computation, mapping
  - **Completion gates**: build | tests

- [x] **6.3b** Teams controller *(done — GET /api/teams/{slug}, /history, /results with pagination and 404 handling)*
  - Create `TeamsController` with `GET /api/teams/{slug}`, `GET /api/teams/{slug}/history`, `GET /api/teams/{slug}/results`.
  - Files: `Api/Controllers/TeamsController.cs`
  - Dependencies: 6.1, 6.3a
  - Tests: Integration test — team detail, history snapshots, paginated results, 404 for unknown slug
  - **Completion gates**: build | tests

- [x] **6.4** Competitions controller *(done — GET /api/competitions with year/tier/country/search filters, RunCount/ParticipantCount=0 for MVP)*
  - Create `CompetitionsController` with `GET /api/competitions` (paginated list, filters: year/tier/country/search). Map to `CompetitionDetailDto` with computed RunCount and ParticipantCount.
  - Files: `Api/Controllers/CompetitionsController.cs`
  - Dependencies: 6.1, 2.4a
  - Tests: Integration test — list with filters, pagination, empty result
  - **Completion gates**: build | tests

- [x] **6.5a** SearchService *(done — searches handlers, dogs→teams, competitions; combines results)*
  - Implement `ISearchService`: search handlers (by normalized name), dogs (by normalized call name, mapped to teams), competitions (by name). Combine as `IReadOnlyList<SearchResult>`.
  - Files: `Service/SearchService.cs`
  - Dependencies: 2.1a, 2.2a, 2.3, 2.4a
  - Tests: Unit test — result merging, type labeling
  - **Completion gates**: build | tests

- [x] **6.5b** Search controller *(done — GET /api/search with min 2 char validation, limit param)*
  - Create `SearchController` with `GET /api/search?q=&limit=`. Min 2 chars validation.
  - Files: `Api/Controllers/SearchController.cs`
  - Dependencies: 6.1, 6.5a
  - Tests: Integration test — search returns mixed results, min 2 chars validation
  - **Completion gates**: build | tests

---

### Phase 7 — Web Design & UI

**Goal**: Design the visual identity first, validate with static HTML/CSS prototypes, then implement as Blazor SSR consuming the API.

#### Phase 7A — Design (HTML/CSS Prototypes)

**Goal**: Create static HTML/CSS prototypes for all MVP pages. These serve as the approved visual reference for Blazor implementation. **The user validates the design before any Blazor code is written.**

- [x] **7.1a** Design system — color palette and typography
  - Define ADW Rating visual identity: color palette (primary, secondary, accent, background, surface, text, semantic colors for tiers, trends, status), typography (heading scale, body text, data/numbers font). Create CSS custom properties file.
  - Files: `design/css/variables.css`
  - Dependencies: none
  - Tests: none (visual validation by user)
  - **Completion gates**: user approval

- [x] **7.1b** Design system — component patterns
  - Create reusable CSS component classes: buttons, pills/tabs, badges (tier, status), cards (stat, team), tables, pagination, filter bar, dropdown, search input. Create a visual reference page showing all components.
  - Files: `design/design-system.html`, `design/css/components.css`, `design/css/utilities.css`
  - Dependencies: 7.1a
  - Tests: none (visual validation by user)
  - **Completion gates**: user approval

- [ ] **7.2a** Static prototype — layout (header, footer, nav)
  - Create shared layout shell: header (logo + nav + search input), footer (How it works + disclaimer). Mobile: hamburger menu.
  - Files: `design/layout.html`
  - Dependencies: 7.1b
  - Tests: none (visual validation)
  - **Completion gates**: user approval

- [ ] **7.2b** Static prototype — home page
  - Hero with search bar, 3 summary stat cards, top 3 teams per size category, recent 5 competitions, about section. Mobile responsive.
  - Files: `design/home.html`
  - Dependencies: 7.2a
  - Tests: none (visual validation)
  - **Completion gates**: user approval

- [ ] **7.2c** Static prototype — rankings page
  - Sticky filter bar, summary row, rankings table with trend/tier/runs columns, tier distribution, pagination. Include Elite, Champion, provisional, NEW examples.
  - Files: `design/rankings.html`
  - Dependencies: 7.2a
  - Tests: none (visual validation)
  - **Completion gates**: user approval

- [ ] **7.2d** Static prototype — team profile page
  - Hero card (initials, rating, tier, trend, peak), quick stats row, rating chart placeholder, competition history table with podium highlighting. Both active and inactive variants.
  - Files: `design/team-profile.html`
  - Dependencies: 7.2a
  - Tests: none (visual validation)
  - **Completion gates**: user approval

- [ ] **7.2e** Static prototype — competition list page
  - Filter bar (year, tier, country, search), entries grouped by year, each with name, dates, location, tier badge, participant count. Major event distinction.
  - Files: `design/competitions.html`
  - Dependencies: 7.2a
  - Tests: none (visual validation)
  - **Completion gates**: user approval

- [ ] **7.2f** Static prototype — How It Works page
  - Static content: hero, what is ADW Rating, methodology, data sources, size mapping table, limitations and disclaimers.
  - Files: `design/how-it-works.html`
  - Dependencies: 7.2a
  - Tests: none (visual validation)
  - **Completion gates**: user approval

- [ ] **7.2g** Static prototype — search dropdown + empty/error states
  - Search dropdown showing grouped results (Teams, Handlers, Competitions). Empty states: "No teams match", 404, loading skeletons.
  - Files: `design/search-dropdown.html`, `design/states.html`
  - Dependencies: 7.2a
  - Tests: none (visual validation)
  - **Completion gates**: user approval
  - **Reference**: `05-ui-structure.md` section 4

- [ ] **7.2h** Page-specific CSS
  - Create any page-specific styles not covered by the component library.
  - Files: `design/css/pages.css`
  - Dependencies: 7.2b–7.2g
  - Tests: none
  - **Completion gates**: user approval

> ⚠️ **CHECKPOINT**: User must validate and approve the design prototypes (7.1 + 7.2) before proceeding to Phase 7B. The approved HTML/CSS is the visual contract for Blazor implementation.

#### Phase 7B — Blazor Implementation

**Goal**: Implement all MVP pages as Blazor SSR components, using the approved HTML/CSS design from Phase 7A. Copy CSS from `design/` into `Web/wwwroot/css/`. Convert static HTML structures into Razor components with data binding.

- [ ] **7.3a** Web project setup
  - Set up `AdwRating.Web` with Blazor Static SSR, Enhanced Navigation. Create `App.razor`, `_Imports.razor`, error page. Copy CSS from `design/css/` into `Web/wwwroot/css/`. Configure `ApiBaseUrl` from `appsettings.json`.
  - Files: `Web/Program.cs`, `Web/Components/App.razor`, `Web/appsettings.json`, `Web/wwwroot/css/`
  - Dependencies: 7.2h
  - Tests: `dotnet build` passes
  - **Completion gates**: build

- [ ] **7.3b** ApiClient — stub setup
  - Set up `AdwRating.ApiClient` with `AdwRatingApiClient` (typed HttpClient wrapper with stub methods returning empty/default data). Register in Web DI.
  - Files: `ApiClient/AdwRatingApiClient.cs`
  - Dependencies: 1.6a, 1.6b, 1.6c
  - Tests: `dotnet build` passes
  - **Completion gates**: build

- [ ] **7.4a** ApiClient — rankings and summary methods
  - Implement `GetRankingsAsync(RankingFilter)` and `GetSummaryAsync()` in `AdwRatingApiClient`.
  - Files: `ApiClient/AdwRatingApiClient.cs`
  - Dependencies: 7.3b, Phase 6
  - Tests: Unit tests — verify URL construction with query params, deserialization, 404 handling
  - **Completion gates**: build | tests

- [ ] **7.4b** ApiClient — team methods
  - Implement `GetTeamAsync(slug)`, `GetTeamHistoryAsync(slug)`, `GetTeamResultsAsync(slug, page, pageSize)`.
  - Files: `ApiClient/AdwRatingApiClient.cs`
  - Dependencies: 7.3b, Phase 6
  - Tests: Unit tests — URL construction, deserialization, 404 → null
  - **Completion gates**: build | tests

- [ ] **7.4c** ApiClient — competitions and search methods
  - Implement `GetCompetitionsAsync(CompetitionFilter)` and `SearchAsync(query, limit)`.
  - Files: `ApiClient/AdwRatingApiClient.cs`
  - Dependencies: 7.3b, Phase 6
  - Tests: Unit tests — URL construction, deserialization
  - **Completion gates**: build | tests

- [ ] **7.5** Layout — header, footer, navigation
  - Convert `design/layout.html` into Blazor components: persistent header (logo, nav links, search placeholder), footer (How It Works, disclaimer). Mobile hamburger menu with JS interop.
  - Files: `Web/Components/Layout/MainLayout.razor`, `Web/Components/Layout/NavMenu.razor`, `Web/Components/Layout/Footer.razor`
  - Dependencies: 7.3a
  - Tests: none (visual — tested via E2E later)
  - **Completion gates**: build

- [ ] **7.6** Home page
  - Convert `design/home.html` into Blazor `/` page. Hero with search placeholder, summary stats (3 cards from `GetSummaryAsync`), top 3 per size (4 API calls), recent competitions (last 5), about section. Mobile: size categories as tabs.
  - Files: `Web/Components/Pages/Home.razor`
  - Dependencies: 7.4a, 7.4c, 7.5
  - Tests: E2E — page loads, summary stats visible, top 3 teams per category, search bar present
  - **Completion gates**: build | tests

- [ ] **7.7** Rankings page
  - Convert `design/rankings.html` into Blazor `/rankings` page. Sticky filter bar (size pills, country dropdown, name search), summary row, rankings table (rank, trend, team, rating, tier, runs), tier distribution, pagination. Enhanced Navigation.
  - Files: `Web/Components/Pages/Rankings.razor`
  - Dependencies: 7.4a, 7.5
  - Tests: E2E — default loads Large, switch to Medium, filter by country, click team navigates, pagination
  - **Completion gates**: build | tests

- [ ] **7.8a** Team Profile — hero card and quick stats
  - Convert team profile hero card into Blazor: initials avatar, handler+dog info, rating, tier badge, trend, peak rating, FEW RUNS/INACTIVE badges. Quick stats row (4 cards: runs, finish rate, podium rate, avg rank). Handler link.
  - Files: `Web/Components/Pages/TeamProfile.razor`
  - Dependencies: 7.4b, 7.5
  - Tests: E2E — page loads with hero card, rating visible, 404 for invalid slug
  - **Completion gates**: build | tests

- [ ] **7.8b** Team Profile — rating chart
  - Add rating progression chart using Chart.js (JS interop): line chart with dates on x-axis, rating on y-axis, sigma band, peak marker.
  - Files: `Web/Components/Pages/TeamProfile.razor` (add chart section), `Web/wwwroot/js/rating-chart.js`
  - Dependencies: 7.8a
  - Tests: E2E — chart renders with data points
  - **Completion gates**: build | tests

- [ ] **7.8c** Team Profile — competition history table
  - Add paginated competition history table to team profile: date, competition name, discipline, rank/ELIM, faults, time, speed. Podium highlighting. Open Graph meta tags via `<HeadContent>`.
  - Files: `Web/Components/Pages/TeamProfile.razor` (add history section)
  - Dependencies: 7.8a
  - Tests: E2E — history table has rows, pagination works
  - **Completion gates**: build | tests

- [ ] **7.9** Competition List page
  - Convert `design/competitions.html` into Blazor `/competitions` page. Filter bar (year pills, tier, country, search), entries grouped by year, date formatting, tier badges, pagination.
  - Files: `Web/Components/Pages/CompetitionList.razor`
  - Dependencies: 7.4c, 7.5
  - Tests: E2E — list loads, year filter works, major events have badge, pagination
  - **Completion gates**: build | tests

- [ ] **7.10** How It Works page
  - Convert `design/how-it-works.html` into Blazor `/how-it-works`. Pure static Razor markup, no API calls. All content sections from `05-ui-structure.md` section 3.18.
  - Files: `Web/Components/Pages/HowItWorks.razor`
  - Dependencies: 7.5
  - Tests: E2E — page loads, key content present, footer link works
  - **Completion gates**: build | tests

- [ ] **7.11** Global search component
  - Implement search dropdown: JS interop for debounce (300ms), click-outside-to-close, keyboard navigation. Calls `SearchAsync`. Results grouped by type. Integrate into header and home hero.
  - Files: `Web/Components/Shared/SearchDropdown.razor`, `Web/wwwroot/js/search.js`
  - Dependencies: 7.4c, 7.5
  - Tests: E2E — type 2+ chars, dropdown appears, click result navigates, empty state
  - **Completion gates**: build | tests

---

### Phase 8 — Polish and Launch Prep

**Goal**: SEO, performance, deployment configuration, final integration testing.

- [ ] **8.1** SEO — page titles, meta descriptions, Open Graph tags
  - Add `<title>`, `<meta name="description">`, and OG tags to all pages per `05-ui-structure.md` section 6. Use `HeadContent` component.
  - Files: Modify all page components in `Web/Components/Pages/`
  - Dependencies: Phase 7
  - Tests: E2E — verify title and meta tags on key pages
  - **Completion gates**: build | tests

- [ ] **8.2a** Error handling — 404 and empty states
  - Add 404 pages (team not found, competition not found). Add empty states ("No teams match your filters", "No competitions found").
  - Files: `Web/Components/Pages/NotFound.razor`, `Web/Components/Shared/EmptyState.razor`, modifications to existing pages
  - Dependencies: Phase 7
  - Tests: E2E — 404 shown for invalid slug, empty state with no-match filters
  - **Completion gates**: build | tests

- [ ] **8.2b** Loading skeletons and error boundary
  - Add loading skeleton patterns for Enhanced Navigation transitions. Global error boundary component.
  - Files: `Web/Components/Shared/LoadingSkeleton.razor`, `Web/Components/Shared/ErrorBoundary.razor`
  - Dependencies: Phase 7
  - Tests: none (visual)
  - **Completion gates**: build

- [ ] **8.3** CI/CD pipeline
  - Create GitHub Actions workflow: restore → build → test (unit + integration) on PR/push. On main: publish win-x64 self-contained. Store artifact.
  - Files: `.github/workflows/ci.yml`
  - Dependencies: all previous phases
  - Tests: Pipeline runs green
  - **Completion gates**: build | tests

- [ ] **8.4** Deployment configuration
  - Create production `appsettings.Production.json` template, IIS web.config for Api and Web, deployment script (PowerShell).
  - Files: `Api/web.config`, `Web/web.config`, `scripts/deploy.ps1`, `DEPLOY.md`
  - Dependencies: 8.3
  - Tests: none (infrastructure)
  - **Completion gates**: build

- [ ] **8.5** Full integration test with sample data
  - End-to-end smoke test: import 2-3 sample CSV files via CLI → recalculate ratings → verify API returns correct data → verify Web pages render. Create sample dataset.
  - Files: `tests/AdwRating.IntegrationTests/FullPipelineTests.cs`, `tests/fixtures/sample-competition-*.csv`
  - Dependencies: all previous phases
  - Tests: Full pipeline integration test
  - **Completion gates**: build | tests

---

## Post-MVP Phases (to be planned)

### Phase 1.5 — Profiles & Detail

**Goal**: Handler profiles and competition detail pages. See `02-to-be.md` section "Scope (Phase 1.5)".

- [ ] **1.5.1** Domain DTOs — RunSummaryDto, RunResultDto
  - Create `RunSummaryDto` and `RunResultDto` from `04-architecture-and-interfaces.md` section 3 (API DTOs). These are needed for competition detail endpoints.
  - Files: `Domain/Models/RunSummaryDto.cs`, `Domain/Models/RunResultDto.cs`
  - Dependencies: MVP Phase 1 (enums)
  - Tests: none (record definitions)
  - **Completion gates**: build

- [ ] **1.5.2** Domain interface — IHandlerProfileService
  - Create `IHandlerProfileService` from `04-architecture-and-interfaces.md` section 3 (Service interfaces).
  - Files: `Domain/Interfaces/IHandlerProfileService.cs`
  - Dependencies: MVP Phase 1 (DTOs)
  - Tests: none (interface definition)
  - **Completion gates**: build

- [ ] **1.5.3** HandlerProfileService implementation
  - Implement `IHandlerProfileService` in Service: `GetBySlugAsync` loads handler with all teams (including peak ratings), maps to `HandlerDetailDto`.
  - Files: `Service/HandlerProfileService.cs`
  - Dependencies: 1.5.2, MVP Phase 2 (repositories)
  - Tests: Unit tests — mapping, peak rating per team
  - **Completion gates**: build | tests

- [ ] **1.5.4** Handlers controller
  - Create `HandlersController` with `GET /api/handlers/{slug}` and `GET /api/handlers/{slug}/teams`.
  - Files: `Api/Controllers/HandlersController.cs`
  - Dependencies: 1.5.3, MVP Phase 6 (API setup)
  - Tests: Integration test — handler detail, 404 for unknown slug
  - **Completion gates**: build | tests

- [ ] **1.5.5** Competition detail endpoints
  - Add to `CompetitionsController`: `GET /api/competitions/{slug}`, `GET /api/competitions/{slug}/runs`, `GET /api/competitions/{slug}/runs/{roundKey}/results`.
  - Files: `Api/Controllers/CompetitionsController.cs`
  - Dependencies: 1.5.1, MVP Phase 6 (API setup)
  - Tests: Integration tests — competition detail, runs list, run results, 404 for unknown slug/roundKey
  - **Completion gates**: build | tests

- [ ] **1.5.6** ApiClient — handler and competition detail methods
  - Add `GetHandlerAsync(slug)`, `GetCompetitionDetailAsync(slug)`, `GetCompetitionRunsAsync(slug)`, `GetRunResultsAsync(slug, roundKey)` to `AdwRatingApiClient`.
  - Files: `ApiClient/AdwRatingApiClient.cs`
  - Dependencies: 1.5.1, 1.5.4, 1.5.5
  - Tests: Unit tests — URL construction, deserialization
  - **Completion gates**: build | tests

- [ ] **1.5.7** Handler profile page
  - Create Blazor `/handlers/{slug}` page: handler name, country, list of teams with dog name, rating, tier, peak rating, active/provisional status.
  - Files: `Web/Components/Pages/HandlerProfile.razor`
  - Dependencies: 1.5.6, MVP Phase 7 (design system)
  - Tests: E2E — page loads, teams listed, 404 for invalid slug
  - **Completion gates**: build | tests

- [ ] **1.5.8** Competition detail page
  - Create Blazor `/competitions/{slug}` page: competition metadata, runs grouped by date/size/discipline, expandable run results tables.
  - Files: `Web/Components/Pages/CompetitionDetail.razor`
  - Dependencies: 1.5.6, MVP Phase 7 (design system)
  - Tests: E2E — page loads, runs grouped correctly, results table expandable
  - **Completion gates**: build | tests

- [ ] **1.5.9** Update existing pages with links
  - Update team profile: handler name links to `/handlers/{slug}`. Update competition list: competition names link to `/competitions/{slug}`.
  - Files: `Web/Components/Pages/TeamProfile.razor`, `Web/Components/Pages/CompetitionList.razor`
  - Dependencies: 1.5.7, 1.5.8
  - Tests: E2E — links navigate correctly
  - **Completion gates**: build | tests

### Phase 2 — Crowdsourcing & Country Rankings

**Goal**: Country rankings, judge profiles, admin auth, crowdsourced uploads. See `02-to-be.md` section "Scope (Phase 2)".

*Detailed tasks will be added after Phase 1.5.*

### Phase 3 — Automation & Growth

**Goal**: Automated imports, user accounts, premium features. See `02-to-be.md` section "Scope (Phase 3)".

*Detailed tasks will be added after Phase 2.*

---

## Notes

### Constraints
- .NET 10 required (see `04-architecture-and-interfaces.md`)
- SQL Server 2022 for database
- No authentication in MVP — all pages public, admin via CLI only
- Competition data is immutable after import — corrections via delete + re-import

### Technology decisions
- **Charting**: Chart.js (via JS interop from Blazor) for the rating progression chart on team profiles. Lightweight, well-documented, supports line charts with fill (for sigma bands). Added via CDN link in `_Host.cshtml` or `App.razor`.
- **Country flags**: Emoji flags (e.g., 🇨🇿 for CZE) for simplicity in MVP. Maps ISO 3166-1 alpha-2 to regional indicator emoji. If emoji rendering is inconsistent across browsers, switch to a lightweight SVG flag set (e.g., `flag-icons` CSS library).
- **CSS approach**: Custom CSS with CSS custom properties (variables) — no CSS framework (Bootstrap, Tailwind) in MVP. Keeps the design unique and avoids generic "Bootstrap look". The design system in Phase 7A defines all tokens.

### Risks
- **openskill.net** package may be unmaintained — PlackettLuce core is ~200 lines, can be reimplemented if needed
- Non-FCI size category mapping is approximate — documented limitation
- Large dataset performance (2-3 years of international competitions) — monitor query times, add indexes as needed

### Dependencies
- No external service dependencies in MVP
- Data collection (CSV files) is a manual effort outside this plan

### CSV Format Specification

Competition results are imported from CSV files with the following format. One CSV file = one competition. Each row = one team's result in one run.

**Required columns:**

| Column | Type | Description | Example |
|--------|------|-------------|---------|
| `round_key` | string | Unique round identifier within the competition | `ind_agility_large_1` |
| `date` | date | Run date (YYYY-MM-DD) | `2024-10-03` |
| `size_category` | string | Size category (FCI: `S`/`M`/`I`/`L`; non-FCI: `20 inch`, `500`, etc.) | `L`, `20 inch` |
| `discipline` | string | `Agility`, `Jumping`, or `Final` | `Agility` |
| `is_team_round` | bool | `true` for team rounds, `false` for individual | `false` |
| `handler_name` | string | Handler's full name (original diacritics) | `Kateřina Třičová` |
| `handler_country` | string | ISO 3166-1 alpha-3 country code | `CZE` |
| `dog_call_name` | string | Dog's call name | `Fame` |
| `rank` | int/empty | Placement (1-based), empty if eliminated | `3` |
| `eliminated` | bool | `true` if DIS/DSQ/NFC/RET/WD | `false` |

**Optional columns:**

| Column | Type | Description |
|--------|------|-------------|
| `dog_registered_name` | string | Full kennel/registered name |
| `dog_breed` | string | Breed name |
| `faults` | int | Obstacle faults |
| `refusals` | int | Refusals |
| `time_faults` | float | Time penalty |
| `total_faults` | float | Total faults |
| `time` | float | Run time in seconds |
| `speed` | float | Speed in m/s |
| `judge` | string | Judge name |
| `sct` | float | Standard Course Time |
| `mct` | float | Maximum Course Time |
| `course_length` | float | Course length in meters |
| `start_no` | int | Start number |
| `run_number` | int | Sequence number of run within discipline+size (default: 1) |

**Rules:**
- Header row is mandatory (column order is flexible — matched by column name)
- If `eliminated = true`, performance fields (`rank`, `faults`, `time`, etc.) should be empty
- If `eliminated = false`, `rank` must be a positive integer
- Encoding: UTF-8 (with or without BOM)
- Delimiter: comma (`,`)
- Fields containing commas must be quoted

**Example:**
```csv
round_key,date,size_category,discipline,is_team_round,handler_name,handler_country,dog_call_name,rank,eliminated,faults,time_faults,time,speed,judge,sct
ind_agility_large_1,2024-10-03,L,Agility,false,Kateřina Třičová,CZE,Fame,1,false,0,0,32.45,5.12,Jan Novák,38.0
ind_agility_large_1,2024-10-03,L,Agility,false,John Smith,GBR,Rex,2,false,5,0,33.12,5.01,Jan Novák,38.0
ind_agility_large_1,2024-10-03,L,Agility,false,Maria Garcia,ESP,Luna,,true,,,,,,38.0
```
