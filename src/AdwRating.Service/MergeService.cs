using AdwRating.Domain.Entities;
using AdwRating.Domain.Enums;
using AdwRating.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace AdwRating.Service;

public class MergeService : IMergeService
{
    private readonly IHandlerRepository _handlerRepo;
    private readonly IDogRepository _dogRepo;
    private readonly IHandlerAliasRepository _handlerAliasRepo;
    private readonly IDogAliasRepository _dogAliasRepo;
    private readonly ILogger<MergeService> _logger;

    public MergeService(
        IHandlerRepository handlerRepo,
        IDogRepository dogRepo,
        IHandlerAliasRepository handlerAliasRepo,
        IDogAliasRepository dogAliasRepo,
        ILogger<MergeService> logger)
    {
        _handlerRepo = handlerRepo;
        _dogRepo = dogRepo;
        _handlerAliasRepo = handlerAliasRepo;
        _dogAliasRepo = dogAliasRepo;
        _logger = logger;
    }

    public async Task MergeHandlersAsync(int sourceHandlerId, int targetHandlerId)
    {
        var source = await _handlerRepo.GetByIdAsync(sourceHandlerId)
            ?? throw new InvalidOperationException($"Source handler {sourceHandlerId} not found.");
        var target = await _handlerRepo.GetByIdAsync(targetHandlerId)
            ?? throw new InvalidOperationException($"Target handler {targetHandlerId} not found.");

        _logger.LogInformation("Merging handler {SourceId} ({SourceName}) into {TargetId} ({TargetName})",
            source.Id, source.Name, target.Id, target.Name);

        await _handlerRepo.MergeAsync(sourceHandlerId, targetHandlerId);

        await _handlerAliasRepo.CreateAsync(new HandlerAlias
        {
            AliasName = source.Name,
            CanonicalHandlerId = targetHandlerId,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation("Handler merge complete. Created alias '{AliasName}' → handler {TargetId}",
            source.Name, targetHandlerId);
    }

    public async Task MergeDogsAsync(int sourceDogId, int targetDogId)
    {
        var source = await _dogRepo.GetByIdAsync(sourceDogId)
            ?? throw new InvalidOperationException($"Source dog {sourceDogId} not found.");
        var target = await _dogRepo.GetByIdAsync(targetDogId)
            ?? throw new InvalidOperationException($"Target dog {targetDogId} not found.");

        if (source.SizeCategory != target.SizeCategory)
            throw new InvalidOperationException(
                $"Cannot merge dogs with different size categories: {source.SizeCategory} vs {target.SizeCategory}.");

        _logger.LogInformation("Merging dog {SourceId} ({SourceName}) into {TargetId} ({TargetName})",
            source.Id, source.CallName, target.Id, target.CallName);

        await _dogRepo.MergeAsync(sourceDogId, targetDogId);

        await _dogAliasRepo.CreateAsync(new DogAlias
        {
            AliasName = source.CallName,
            CanonicalDogId = targetDogId,
            AliasType = DogAliasType.CallName,
            Source = AliasSource.Manual,
            CreatedAt = DateTime.UtcNow
        });

        _logger.LogInformation("Dog merge complete. Created alias '{AliasName}' → dog {TargetId}",
            source.CallName, targetDogId);
    }
}
