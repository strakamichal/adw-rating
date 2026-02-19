using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Helpers;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AdwRating.Service;

public class IdentityResolutionService : IIdentityResolutionService
{
    private const int FuzzySearchLimit = 50;
    private const int MaxLevenshteinDistance = 2;

    private readonly IHandlerRepository _handlerRepo;
    private readonly IHandlerAliasRepository _handlerAliasRepo;
    private readonly IDogRepository _dogRepo;
    private readonly IDogAliasRepository _dogAliasRepo;
    private readonly ITeamRepository _teamRepo;
    private readonly IRatingConfigurationRepository _configRepo;
    private readonly ILogger<IdentityResolutionService> _logger;

    public IdentityResolutionService(
        IHandlerRepository handlerRepo,
        IHandlerAliasRepository handlerAliasRepo,
        IDogRepository dogRepo,
        IDogAliasRepository dogAliasRepo,
        ITeamRepository teamRepo,
        IRatingConfigurationRepository configRepo,
        ILogger<IdentityResolutionService> logger)
    {
        _handlerRepo = handlerRepo;
        _handlerAliasRepo = handlerAliasRepo;
        _dogRepo = dogRepo;
        _dogAliasRepo = dogAliasRepo;
        _teamRepo = teamRepo;
        _configRepo = configRepo;
        _logger = logger;
    }

    public async Task<Handler> ResolveHandlerAsync(string rawName, string country)
    {
        var normalizedName = NameNormalizer.Normalize(rawName);

        // 1. Check alias table
        var alias = await _handlerAliasRepo.FindByAliasNameAsync(normalizedName);
        if (alias is not null)
        {
            var canonical = await _handlerRepo.GetByIdAsync(alias.CanonicalHandlerId);
            _logger.LogDebug("Handler '{RawName}' resolved via alias to '{CanonicalName}'",
                rawName, canonical!.Name);
            return canonical!;
        }

        // 2. Exact match on normalized name + country
        var exact = await _handlerRepo.FindByNormalizedNameAndCountryAsync(normalizedName, country);
        if (exact is not null)
        {
            _logger.LogDebug("Handler '{RawName}' resolved via exact match", rawName);
            return exact;
        }

        // 3. Fuzzy match: search candidates, filter same country, Levenshtein <= 2
        var candidates = await _handlerRepo.SearchAsync(normalizedName, FuzzySearchLimit);
        foreach (var candidate in candidates)
        {
            if (!string.Equals(candidate.Country, country, StringComparison.OrdinalIgnoreCase))
                continue;

            var distance = LevenshteinDistance.Compute(normalizedName, candidate.NormalizedName);
            if (distance <= MaxLevenshteinDistance)
            {
                _logger.LogInformation(
                    "Handler '{RawName}' fuzzy-matched to '{CanonicalName}' (distance={Distance})",
                    rawName, candidate.Name, distance);

                await _handlerAliasRepo.CreateAsync(new HandlerAlias
                {
                    AliasName = normalizedName,
                    CanonicalHandlerId = candidate.Id,
                    Source = AliasSource.FuzzyMatch,
                    CreatedAt = DateTime.UtcNow
                });

                return candidate;
            }
        }

        // 4. No match — create new handler
        _logger.LogInformation("Handler '{RawName}' not found, creating new record", rawName);

        var newHandler = await _handlerRepo.CreateAsync(new Handler
        {
            Name = rawName,
            NormalizedName = normalizedName,
            Country = country,
            Slug = SlugHelper.GenerateSlug(rawName)
        });

        return newHandler;
    }

    public async Task<Dog> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size)
    {
        var normalizedName = NameNormalizer.Normalize(rawDogName);

        // 1. Check alias table
        var alias = await _dogAliasRepo.FindByAliasNameAndTypeAsync(normalizedName, DogAliasType.CallName);
        if (alias is not null)
        {
            var canonical = await _dogRepo.GetByIdAsync(alias.CanonicalDogId);
            _logger.LogDebug("Dog '{RawName}' resolved via alias to '{CanonicalName}'",
                rawDogName, canonical!.CallName);
            return canonical!;
        }

        // 2. Exact match on normalized name + size
        var exact = await _dogRepo.FindByNormalizedNameAndSizeAsync(normalizedName, size);
        if (exact is not null)
        {
            // Update breed if previously unknown
            if (exact.Breed is null && breed is not null)
            {
                exact.Breed = breed;
                await _dogRepo.UpdateAsync(exact);
                _logger.LogDebug("Updated breed for dog '{DogName}' to '{Breed}'",
                    exact.CallName, breed);
            }

            _logger.LogDebug("Dog '{RawName}' resolved via exact match", rawDogName);
            return exact;
        }

        // 3. Fuzzy match: search candidates, filter same size, Levenshtein <= 2
        var candidates = await _dogRepo.SearchAsync(normalizedName, FuzzySearchLimit);
        foreach (var candidate in candidates)
        {
            if (candidate.SizeCategory != size)
                continue;

            var distance = LevenshteinDistance.Compute(normalizedName, candidate.NormalizedCallName);
            if (distance <= MaxLevenshteinDistance)
            {
                _logger.LogInformation(
                    "Dog '{RawName}' fuzzy-matched to '{CanonicalName}' (distance={Distance})",
                    rawDogName, candidate.CallName, distance);

                await _dogAliasRepo.CreateAsync(new DogAlias
                {
                    AliasName = normalizedName,
                    CanonicalDogId = candidate.Id,
                    AliasType = DogAliasType.CallName,
                    Source = AliasSource.FuzzyMatch,
                    CreatedAt = DateTime.UtcNow
                });

                return candidate;
            }
        }

        // 4. No match — create new dog
        _logger.LogInformation("Dog '{RawName}' not found, creating new record", rawDogName);

        var newDog = await _dogRepo.CreateAsync(new Dog
        {
            CallName = rawDogName,
            NormalizedCallName = normalizedName,
            Breed = breed,
            SizeCategory = size
        });

        return newDog;
    }

    public async Task<Team> ResolveTeamAsync(int handlerId, int dogId)
    {
        // 1. Check for existing team
        var existing = await _teamRepo.GetByHandlerAndDogAsync(handlerId, dogId);
        if (existing is not null)
        {
            _logger.LogDebug("Team found for handler={HandlerId}, dog={DogId}", handlerId, dogId);
            return existing;
        }

        // 2. Create new team with rating defaults from active config
        var config = await _configRepo.GetActiveAsync();
        var handler = await _handlerRepo.GetByIdAsync(handlerId);
        var dog = await _dogRepo.GetByIdAsync(dogId);

        var slug = SlugHelper.GenerateSlug($"{handler!.Name} {dog!.CallName}");

        _logger.LogInformation("Creating new team: handler={HandlerName}, dog={DogName}",
            handler.Name, dog.CallName);

        var newTeam = await _teamRepo.CreateAsync(new Team
        {
            HandlerId = handlerId,
            DogId = dogId,
            Slug = slug,
            Mu = config.Mu0,
            Sigma = config.Sigma0,
            Rating = 0,
            PrevMu = 0,
            PrevSigma = 0,
            PrevRating = 0,
            RunCount = 0,
            FinishedRunCount = 0,
            Top3RunCount = 0,
            IsActive = false,
            IsProvisional = true,
            TierLabel = null,
            PeakRating = 0
        });

        return newTeam;
    }
}
