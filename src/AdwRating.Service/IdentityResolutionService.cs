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

    public async Task<Dog> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size, int handlerId)
    {
        var normalizedFull = NameNormalizer.Normalize(rawDogName);

        // Extract call name from parentheses/quotes if present
        var (extractedCallName, extractedRegistered) = NameNormalizer.ExtractCallName(rawDogName);
        var normalizedCallName = extractedCallName is not null
            ? NameNormalizer.Normalize(extractedCallName)
            : null;
        var normalizedRegistered = extractedRegistered is not null
            ? NameNormalizer.Normalize(extractedRegistered)
            : null;

        // Names to try for matching (call name first, then full input)
        var namesToTry = new List<string>();
        if (normalizedCallName is not null)
            namesToTry.Add(normalizedCallName);
        namesToTry.Add(normalizedFull);
        if (normalizedRegistered is not null && !namesToTry.Contains(normalizedRegistered))
            namesToTry.Add(normalizedRegistered);

        // Build set of dog IDs already associated with this handler (via teams)
        var handlerTeams = await _teamRepo.GetByHandlerIdAsync(handlerId);
        var handlerDogIds = new HashSet<int>(handlerTeams.Select(t => t.DogId));

        // Helper: check if a dog candidate belongs to this handler
        bool BelongsToHandler(int dogId) => handlerDogIds.Contains(dogId);

        // 1. Check alias table — prefer alias hits that belong to this handler
        Dog? aliasGlobalHit = null;
        foreach (var nameVariant in namesToTry)
        {
            var alias = await _dogAliasRepo.FindByAliasNameAndTypeAsync(nameVariant, DogAliasType.CallName);
            if (alias is not null)
            {
                if (BelongsToHandler(alias.CanonicalDogId))
                {
                    var canonical = await _dogRepo.GetByIdAsync(alias.CanonicalDogId);
                    _logger.LogDebug("Dog '{RawName}' resolved via alias to handler's dog '{CallName}'",
                        rawDogName, canonical!.CallName);
                    return canonical!;
                }
                // Remember global hit but don't return yet — keep looking for handler-scoped match
                aliasGlobalHit ??= await _dogRepo.GetByIdAsync(alias.CanonicalDogId);
            }
        }

        // 2. Exact match on normalized name + size — prefer handler's dogs
        Dog? exactGlobalHit = null;
        foreach (var nameVariant in namesToTry)
        {
            var exact = await _dogRepo.FindByNormalizedNameAndSizeAsync(nameVariant, size);
            if (exact is not null)
            {
                if (BelongsToHandler(exact.Id))
                {
                    if (exact.Breed is null && breed is not null)
                    {
                        exact.Breed = breed;
                        await _dogRepo.UpdateAsync(exact);
                    }
                    await CreateDogAliasesIfNeeded(exact.Id, normalizedFull, normalizedCallName, normalizedRegistered, exact.NormalizedCallName);
                    _logger.LogDebug("Dog '{RawName}' resolved via exact match to handler's dog '{CallName}'",
                        rawDogName, exact.CallName);
                    return exact;
                }
                exactGlobalHit ??= exact;
            }
        }

        // 3. Fuzzy match — prefer handler's dogs
        Dog? fuzzyGlobalHit = null;
        foreach (var nameVariant in namesToTry)
        {
            var candidates = await _dogRepo.SearchAsync(nameVariant, FuzzySearchLimit);
            foreach (var candidate in candidates)
            {
                if (candidate.SizeCategory != size)
                    continue;

                var distance = LevenshteinDistance.Compute(nameVariant, candidate.NormalizedCallName);
                if (distance <= MaxLevenshteinDistance)
                {
                    if (BelongsToHandler(candidate.Id))
                    {
                        _logger.LogInformation("Dog '{RawName}' fuzzy-matched to handler's dog '{CallName}'",
                            rawDogName, candidate.CallName);
                        await CreateDogAliasesIfNeeded(candidate.Id, normalizedFull, normalizedCallName, normalizedRegistered, candidate.NormalizedCallName);
                        return candidate;
                    }
                    fuzzyGlobalHit ??= candidate;
                }
            }
        }

        // 4. No handler-scoped match found. Use global match ONLY if the name is
        //    specific enough (registered name or full name match, NOT just a short call name).
        //    Short call names like "Beat", "Day" are too ambiguous for cross-handler matching.
        var globalHit = aliasGlobalHit ?? exactGlobalHit ?? fuzzyGlobalHit;
        if (globalHit is not null)
        {
            var matchedName = normalizedFull;
            var isSpecificEnough = matchedName.Length > 10
                || (normalizedRegistered is not null && normalizedRegistered.Length > 10);

            if (isSpecificEnough)
            {
                if (globalHit.Breed is null && breed is not null)
                {
                    globalHit.Breed = breed;
                    await _dogRepo.UpdateAsync(globalHit);
                }
                await CreateDogAliasesIfNeeded(globalHit.Id, normalizedFull, normalizedCallName, normalizedRegistered, globalHit.NormalizedCallName);
                _logger.LogInformation("Dog '{RawName}' matched globally to '{CallName}' (no handler association)",
                    rawDogName, globalHit.CallName);
                return globalHit;
            }

            _logger.LogDebug("Dog '{RawName}' has global match '{CallName}' but name too short for cross-handler match — creating new",
                rawDogName, globalHit.CallName);
        }

        // 5. No match — create new dog (prefer extracted call name)
        var storedCallName = extractedCallName ?? rawDogName;
        var storedNormalized = normalizedCallName ?? normalizedFull;

        _logger.LogInformation("Dog '{RawName}' not found, creating new record with CallName='{CallName}'",
            rawDogName, storedCallName);

        var newDog = await _dogRepo.CreateAsync(new Dog
        {
            CallName = storedCallName,
            NormalizedCallName = storedNormalized,
            RegisteredName = extractedRegistered ?? (extractedCallName is not null ? rawDogName : null),
            Breed = breed,
            SizeCategory = size
        });

        // Create aliases for all name variants
        await CreateDogAliasesIfNeeded(newDog.Id, normalizedFull, normalizedCallName, normalizedRegistered, storedNormalized);

        return newDog;
    }

    private async Task CreateDogAliasesIfNeeded(int dogId, string normalizedFull, string? normalizedCallName, string? normalizedRegistered, string canonicalNormalized)
    {
        // Collect distinct name variants that differ from the canonical normalized name
        var aliasNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(normalizedFull, canonicalNormalized, StringComparison.OrdinalIgnoreCase))
            aliasNames.Add(normalizedFull);
        if (normalizedCallName is not null && !string.Equals(normalizedCallName, canonicalNormalized, StringComparison.OrdinalIgnoreCase))
            aliasNames.Add(normalizedCallName);
        if (normalizedRegistered is not null && !string.Equals(normalizedRegistered, canonicalNormalized, StringComparison.OrdinalIgnoreCase))
            aliasNames.Add(normalizedRegistered);

        foreach (var aliasName in aliasNames)
        {
            if (string.IsNullOrWhiteSpace(aliasName))
                continue;

            // Check if alias already exists
            var existing = await _dogAliasRepo.FindByAliasNameAndTypeAsync(aliasName, DogAliasType.CallName);
            if (existing is not null)
                continue;

            try
            {
                await _dogAliasRepo.CreateAsync(new DogAlias
                {
                    AliasName = aliasName,
                    CanonicalDogId = dogId,
                    AliasType = DogAliasType.CallName,
                    Source = AliasSource.Import,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogDebug("Created dog alias '{Alias}' for dog {DogId}", aliasName, dogId);
            }
            catch (Exception ex)
            {
                // Unique constraint violation — alias was already created by another path
                _logger.LogDebug("Dog alias '{Alias}' already exists: {Error}", aliasName, ex.Message);
            }
        }
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
