using System.Text.RegularExpressions;
using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Helpers;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AdwRating.Service;

public class IdentityResolutionService : IIdentityResolutionService
{
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

    public async Task<(Handler Handler, bool IsNew)> ResolveHandlerAsync(string rawName, string country)
    {
        var normalizedName = NameNormalizer.Normalize(rawName);

        // 1. Check alias table
        var alias = await _handlerAliasRepo.FindByAliasNameAsync(normalizedName);
        if (alias is not null)
        {
            var canonical = await _handlerRepo.GetByIdAsync(alias.CanonicalHandlerId);
            _logger.LogDebug("Handler '{RawName}' resolved via alias to '{CanonicalName}'",
                rawName, canonical!.Name);
            await BackfillHandlerProfileAsync(canonical!, rawName, country);
            return (canonical!, false);
        }

        // 2. Exact match on normalized name + country
        var exact = await _handlerRepo.FindByNormalizedNameAndCountryAsync(normalizedName, country);
        if (exact is not null)
        {
            _logger.LogDebug("Handler '{RawName}' resolved via exact match", rawName);
            await BackfillHandlerProfileAsync(exact, rawName, country);
            return (exact, false);
        }

        // 3. Country-agnostic fallback — same name, different country
        //    Only for multi-token names to avoid false matches on single names like "Martin"
        var nameTokens = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (nameTokens.Length >= 2)
        {
            var nameOnlyMatches = await _handlerRepo.FindByNormalizedNameAsync(normalizedName);
            if (nameOnlyMatches.Count == 1)
            {
                var match = nameOnlyMatches[0];
                _logger.LogInformation(
                    "Handler '{RawName}' ({Country}) matched country-agnostic to '{MatchName}' ({MatchCountry})",
                    rawName, country, match.Name, match.Country);
                await BackfillHandlerProfileAsync(match, rawName, country);
                return (match, false);
            }
        }

        // 4. Containment/substring match — "Adrian Bajo" ↔ "Adrian Bajo Alonso"
        //    Requires: name length >= 10, 2+ tokens, same country, exactly 1 match
        if (normalizedName.Length >= 10 && nameTokens.Length >= 2)
        {
            var containmentMatches = await _handlerRepo.FindByNormalizedNameContainingAsync(normalizedName, country);
            // Filter out exact self-match (already checked above)
            containmentMatches = containmentMatches
                .Where(h => !string.Equals(h.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (containmentMatches.Count == 1)
            {
                var match = containmentMatches[0];
                _logger.LogInformation(
                    "Handler '{RawName}' matched via containment to '{MatchName}' ({MatchCountry})",
                    rawName, match.Name, match.Country);

                // Create alias for instant future lookups
                try
                {
                    await _handlerAliasRepo.CreateAsync(new HandlerAlias
                    {
                        AliasName = normalizedName,
                        CanonicalHandlerId = match.Id,
                        Source = AliasSource.FuzzyMatch,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Containment alias already exists for '{Name}'", normalizedName);
                }

                await BackfillHandlerProfileAsync(match, rawName, country);
                return (match, false);
            }
        }

        // 5. No match — create new handler
        var displayName = NameNormalizer.CleanDisplayName(rawName);
        _logger.LogInformation("Handler '{RawName}' not found, creating new record", rawName);

        var newHandler = await _handlerRepo.CreateAsync(new Handler
        {
            Name = displayName,
            NormalizedName = normalizedName,
            Country = country,
            Slug = await GenerateUniqueHandlerSlugAsync(SlugHelper.GenerateSlug(displayName))
        });

        await TryCreateReversedHandlerAliasesAsync(newHandler);

        return (newHandler, true);
    }

    /// <summary>
    /// Backfills empty handler country and display name when new data provides them.
    /// </summary>
    private async Task BackfillHandlerProfileAsync(Handler handler, string rawName, string country)
    {
        var updated = false;

        if (string.IsNullOrEmpty(handler.Country) && !string.IsNullOrEmpty(country))
        {
            _logger.LogInformation("Backfilling country '{Country}' for handler '{Name}'", country, handler.Name);
            handler.Country = country;
            updated = true;
        }

        // If stored name has comma (old import) but new raw name doesn't, prefer cleaned version
        if (handler.Name.Contains(',') && !rawName.Contains(','))
        {
            var cleaned = NameNormalizer.CleanDisplayName(handler.Name);
            if (cleaned != handler.Name)
            {
                _logger.LogInformation("Cleaning handler name '{Old}' -> '{New}'", handler.Name, cleaned);
                handler.Name = cleaned;
                updated = true;
            }
        }

        if (updated)
            await _handlerRepo.UpdateAsync(handler);
    }

    private async Task TryCreateReversedHandlerAliasesAsync(Handler canonicalHandler)
    {
        var aliases = BuildNameRotations(canonicalHandler.NormalizedName);

        foreach (var alias in aliases)
        {
            var existing = await _handlerAliasRepo.FindByAliasNameAsync(alias);
            if (existing is not null)
                continue;

            try
            {
                await _handlerAliasRepo.CreateAsync(new HandlerAlias
                {
                    AliasName = alias,
                    CanonicalHandlerId = canonicalHandler.Id,
                    Source = AliasSource.Import,
                    CreatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Skipping reversed handler alias creation for handler {HandlerId}", canonicalHandler.Id);
            }
        }
    }

    /// <summary>
    /// Builds name rotations to match "Firstname Surname" vs "Surname Firstname" patterns.
    /// For 2 tokens [A B]: returns [B A].
    /// For 3+ tokens [A B C]: returns [C A B] (last-to-front) and [B C A] (first-to-back).
    /// This covers "De Groote Andy" <-> "Andy De Groote" and "Ganzi Karl Heinz" <-> "Karl Heinz Ganzi".
    /// </summary>
    internal static List<string> BuildNameRotations(string normalizedName)
    {
        var parts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return [];

        var result = new List<string>();

        if (parts.Length == 2)
        {
            // Simple swap: [A B] -> [B A]
            result.Add($"{parts[1]} {parts[0]}");
        }
        else
        {
            // Rotate right (last to front): [A B C] -> [C A B]
            var rotateRight = new string[parts.Length];
            rotateRight[0] = parts[^1];
            Array.Copy(parts, 0, rotateRight, 1, parts.Length - 1);
            result.Add(string.Join(' ', rotateRight));

            // Rotate left (first to back): [A B C] -> [B C A]
            var rotateLeft = new string[parts.Length];
            Array.Copy(parts, 1, rotateLeft, 0, parts.Length - 1);
            rotateLeft[^1] = parts[0];
            var leftStr = string.Join(' ', rotateLeft);
            if (leftStr != result[0]) // avoid duplicate if rotation produces same result
                result.Add(leftStr);
        }

        return result;
    }

    public async Task<(Dog Dog, bool IsNew)> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size, int handlerId)
    {
        // Clean titles and suffixes: "A3Ch Dagny Ballarat (cp)" → "Dagny Ballarat"
        var cleanedDogName = NameNormalizer.CleanDogName(rawDogName);
        var normalizedFull = NameNormalizer.Normalize(cleanedDogName);

        // Extract call name from parentheses/quotes if present
        var (extractedCallName, extractedRegistered) = NameNormalizer.ExtractCallName(cleanedDogName);
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
                    if (BackfillDogNames(canonical!, cleanedDogName, extractedCallName, extractedRegistered))
                        await _dogRepo.UpdateAsync(canonical!);
                    return (canonical!, false);
                }
                // Remember global hit but don't return yet — keep looking for handler-scoped match
                aliasGlobalHit ??= await _dogRepo.GetByIdAsync(alias.CanonicalDogId);
            }
        }

        // 2. Exact match on normalized name + size — prefer handler's dogs
        Dog? exactGlobalHit = null;
        foreach (var nameVariant in namesToTry)
        {
            var candidates = await _dogRepo.FindAllByNormalizedNameAndSizeAsync(nameVariant, size);
            foreach (var candidate in candidates)
            {
                if (BelongsToHandler(candidate.Id))
                {
                    var exactUpdated = false;
                    if (candidate.Breed is null && breed is not null)
                    {
                        candidate.Breed = breed;
                        exactUpdated = true;
                    }
                    exactUpdated |= BackfillDogNames(candidate, cleanedDogName, extractedCallName, extractedRegistered);
                    if (exactUpdated)
                        await _dogRepo.UpdateAsync(candidate);
                    await CreateDogAliasesIfNeeded(candidate.Id, normalizedFull, normalizedCallName, normalizedRegistered, candidate.NormalizedCallName);
                    _logger.LogDebug("Dog '{RawName}' resolved via exact match to handler's dog '{CallName}'",
                        rawDogName, candidate.CallName);
                    return (candidate, false);
                }
                exactGlobalHit ??= candidate;
            }
        }

        // 3. Handler-scoped fuzzy match: containment + size relaxation
        //    "Berta" <-> "Berta z Kojca Coli", or same dog in adjacent size (S<->M, I<->L)
        if (handlerDogIds.Count > 0)
        {
            var fuzzyMatches = new List<Dog>();
            foreach (var dogId in handlerDogIds)
            {
                var handlerDog = await _dogRepo.GetByIdAsync(dogId);
                if (handlerDog is null) continue;

                // Collect all normalized names for this dog
                var dogNames = new List<string> { handlerDog.NormalizedCallName };
                if (!string.IsNullOrEmpty(handlerDog.NormalizedRegisteredName))
                    dogNames.Add(handlerDog.NormalizedRegisteredName);

                foreach (var nameVariant in namesToTry)
                {
                    if (nameVariant.Length < 3) continue;

                    foreach (var dogName in dogNames)
                    {
                        if (string.IsNullOrEmpty(dogName)) continue;

                        // Exact name with adjacent size, or word-boundary containment with same/adjacent size
                        var nameExact = string.Equals(nameVariant, dogName, StringComparison.OrdinalIgnoreCase);
                        var nameContained = !nameExact && IsWordBoundaryContainment(nameVariant, dogName);

                        if ((nameExact || nameContained)
                            && IsAdjacentOrSameSize(size, handlerDog.SizeCategory))
                        {
                            fuzzyMatches.Add(handlerDog);
                            goto nextDog; // Only match each dog once
                        }
                    }
                }
                nextDog:;
            }

            if (fuzzyMatches.Count == 1)
            {
                var match = fuzzyMatches[0];
                _logger.LogInformation(
                    "Dog '{RawName}' ({Size}) fuzzy-matched to handler's dog '{CallName}' ({MatchSize}) via containment",
                    rawDogName, size, match.CallName, match.SizeCategory);

                var dogUpdated = false;

                if (match.Breed is null && breed is not null)
                {
                    match.Breed = breed;
                    dogUpdated = true;
                }

                // Backfill RegisteredName/CallName from the longer/shorter variant
                dogUpdated |= BackfillDogNames(match, rawDogName, extractedCallName, extractedRegistered);

                if (dogUpdated)
                    await _dogRepo.UpdateAsync(match);

                await CreateDogAliasesIfNeeded(match.Id, normalizedFull, normalizedCallName, normalizedRegistered, match.NormalizedCallName);
                return (match, false);
            }
        }

        // 4. No handler-scoped match found. Use global match ONLY if the name is
        //    specific enough (registered name or full name match, NOT just a short call name).
        //    Short call names like "Beat", "Day" are too ambiguous for cross-handler matching.
        var globalHit = aliasGlobalHit ?? exactGlobalHit;
        if (globalHit is not null)
        {
            var matchedName = normalizedFull;
            var isSpecificEnough = matchedName.Length > 10
                || (normalizedRegistered is not null && normalizedRegistered.Length > 10);

            if (isSpecificEnough)
            {
                var globalUpdated = false;
                if (globalHit.Breed is null && breed is not null)
                {
                    globalHit.Breed = breed;
                    globalUpdated = true;
                }
                globalUpdated |= BackfillDogNames(globalHit, cleanedDogName, extractedCallName, extractedRegistered);
                if (globalUpdated)
                    await _dogRepo.UpdateAsync(globalHit);
                await CreateDogAliasesIfNeeded(globalHit.Id, normalizedFull, normalizedCallName, normalizedRegistered, globalHit.NormalizedCallName);
                _logger.LogInformation("Dog '{RawName}' matched globally to '{CallName}' (no handler association)",
                    rawDogName, globalHit.CallName);
                return (globalHit, false);
            }

            _logger.LogDebug("Dog '{RawName}' has global match '{CallName}' but name too short for cross-handler match — creating new",
                rawDogName, globalHit.CallName);
        }

        // 5. No match — create new dog (prefer extracted call name)
        var storedCallName = extractedCallName ?? cleanedDogName;
        var storedNormalizedCall = normalizedCallName ?? normalizedFull;
        string? storedRegistered = extractedRegistered ?? (extractedCallName is not null ? cleanedDogName : null);
        var storedNormalizedRegistered = string.Empty;

        // Word-count heuristic: 3+ words without explicit call name → treat as registered name
        // CallName left empty (unknown), NormalizedCallName is empty (call name unknown)
        if (extractedCallName is null && storedCallName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3)
        {
            storedRegistered = storedCallName;
            storedCallName = string.Empty;
            storedNormalizedCall = string.Empty;
            storedNormalizedRegistered = normalizedFull;
        }

        // Populate NormalizedRegisteredName when we have a registered name
        if (!string.IsNullOrEmpty(storedRegistered) && string.IsNullOrEmpty(storedNormalizedRegistered))
            storedNormalizedRegistered = NameNormalizer.Normalize(storedRegistered);

        _logger.LogInformation("Dog '{RawName}' not found, creating new record with CallName='{CallName}'",
            rawDogName, storedCallName);

        var newDog = await _dogRepo.CreateAsync(new Dog
        {
            CallName = storedCallName,
            NormalizedCallName = storedNormalizedCall,
            RegisteredName = storedRegistered,
            NormalizedRegisteredName = storedNormalizedRegistered,
            Breed = breed,
            SizeCategory = size
        });

        // Create aliases for all name variants
        await CreateDogAliasesIfNeeded(newDog.Id, normalizedFull, normalizedCallName, normalizedRegistered, storedNormalizedCall);

        return (newDog, true);
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

    public async Task<(Team Team, bool IsNew)> ResolveTeamAsync(int handlerId, int dogId)
    {
        // 1. Check for existing team
        var existing = await _teamRepo.GetByHandlerAndDogAsync(handlerId, dogId);
        if (existing is not null)
        {
            _logger.LogDebug("Team found for handler={HandlerId}, dog={DogId}", handlerId, dogId);
            return (existing, false);
        }

        // 2. Create new team with rating defaults from active config
        var config = await _configRepo.GetActiveAsync();
        var handler = await _handlerRepo.GetByIdAsync(handlerId);
        var dog = await _dogRepo.GetByIdAsync(dogId);

        var slug = await GenerateUniqueTeamSlugAsync(SlugHelper.GenerateSlug($"{handler!.Name} {dog!.CallName}"));

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

        return (newTeam, true);
    }

    private async Task<string> GenerateUniqueHandlerSlugAsync(string baseSlug)
    {
        var slug = baseSlug;
        var suffix = 2;

        while (await _handlerRepo.GetBySlugAsync(slug) is not null)
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }

    /// <summary>
    /// When fuzzy matching connects a short name ("Berta") with a long name ("Berta z Kojca Coli"),
    /// backfill the dog's CallName/RegisteredName if they were missing.
    /// Returns true if any field was updated.
    /// </summary>
    internal static bool BackfillDogNames(Dog dog, string rawDogName, string? extractedCallName, string? extractedRegistered)
    {
        var updated = false;
        var incomingFull = NameNormalizer.CleanDogName(rawDogName.Trim());

        // Collect all known non-empty names
        var allNames = new List<string>();
        if (!string.IsNullOrEmpty(dog.CallName)) allNames.Add(dog.CallName);
        if (!string.IsNullOrEmpty(dog.RegisteredName)) allNames.Add(dog.RegisteredName);
        if (!string.IsNullOrEmpty(incomingFull)) allNames.Add(incomingFull);
        if (!string.IsNullOrEmpty(extractedCallName)) allNames.Add(extractedCallName);
        if (!string.IsNullOrEmpty(extractedRegistered)) allNames.Add(extractedRegistered);

        if (allNames.Count == 0) return false;

        var shortest = allNames.OrderBy(n => n.Length).First();
        var longest = allNames.OrderByDescending(n => n.Length).First();

        if (shortest.Length == longest.Length)
            return false; // All same length — nothing to infer

        // Backfill RegisteredName if missing and we have a longer variant
        if (string.IsNullOrEmpty(dog.RegisteredName) && longest.Length > shortest.Length)
        {
            dog.RegisteredName = longest;
            dog.NormalizedRegisteredName = NameNormalizer.Normalize(longest);
            updated = true;
        }

        // Backfill CallName if empty (3+ word heuristic left it blank) and we now have a short name
        if (string.IsNullOrEmpty(dog.CallName) && shortest.Length >= 2 && shortest != dog.RegisteredName)
        {
            dog.CallName = shortest;
            dog.NormalizedCallName = NameNormalizer.Normalize(shortest);
            updated = true;
        }
        // Update CallName to shorter variant if we found a shorter one
        else if (!string.IsNullOrEmpty(dog.CallName) && shortest.Length < dog.CallName.Length && shortest.Length >= 2)
        {
            dog.CallName = shortest;
            dog.NormalizedCallName = NameNormalizer.Normalize(shortest);
            updated = true;
        }

        // Ensure NormalizedRegisteredName is consistent if RegisteredName exists but normalized form is missing
        if (!string.IsNullOrEmpty(dog.RegisteredName) && string.IsNullOrEmpty(dog.NormalizedRegisteredName))
        {
            dog.NormalizedRegisteredName = NameNormalizer.Normalize(dog.RegisteredName);
            updated = true;
        }

        return updated;
    }

    /// <summary>
    /// Checks if the shorter string appears as complete word(s) within the longer string.
    /// "berta" in "berta z kojca coli" -> true (word boundary match)
    /// "lis" in "borealis" -> false (not at word boundary)
    /// Strings must differ (not exact match — that's handled by earlier steps).
    /// </summary>
    internal static bool IsWordBoundaryContainment(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return false;

        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;

        if (shorter.Length < 3)
            return false;

        // Use word boundary regex: shorter must appear as whole word(s)
        var pattern = @"(?<!\w)" + Regex.Escape(shorter) + @"(?!\w)";
        return Regex.IsMatch(longer, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Returns true if sizes are the same or adjacent in the FCI progression: S<->M, I<->L.
    /// </summary>
    internal static bool IsAdjacentOrSameSize(SizeCategory a, SizeCategory b)
    {
        if (a == b) return true;
        return (a, b) switch
        {
            (SizeCategory.S, SizeCategory.M) => true,
            (SizeCategory.M, SizeCategory.S) => true,
            (SizeCategory.I, SizeCategory.L) => true,
            (SizeCategory.L, SizeCategory.I) => true,
            _ => false
        };
    }

    private async Task<string> GenerateUniqueTeamSlugAsync(string baseSlug)
    {
        var slug = baseSlug;
        var suffix = 2;

        while (await _teamRepo.GetBySlugAsync(slug) is not null)
        {
            slug = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return slug;
    }
}
