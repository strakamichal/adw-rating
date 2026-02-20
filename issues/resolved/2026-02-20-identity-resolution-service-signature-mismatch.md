# IIdentityResolutionService.ResolveDogAsync signature differs from spec

- **Type**: improvement
- **Priority**: low
- **Status**: resolved

## Description

The spec in `docs/04-architecture-and-interfaces.md` defines:
```csharp
Task<Dog> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size);
```

The actual implementation adds a `handlerId` parameter:
```csharp
Task<Dog> ResolveDogAsync(string rawDogName, string? breed, SizeCategory size, int handlerId);
```

The extra parameter is a legitimate enhancement (handler-scoped dog resolution improves matching accuracy), but the spec should be updated to reflect this.

## Where to look

- `src/AdwRating.Domain/Interfaces/IIdentityResolutionService.cs`
- `docs/04-architecture-and-interfaces.md` section 3

## Acceptance criteria

- [x] Update the spec to include `handlerId` parameter in `ResolveDogAsync`

## Resolution

Updated `docs/04-architecture-and-interfaces.md` section 3 to change the `IIdentityResolutionService.ResolveDogAsync` signature from `(string rawDogName, string? breed, SizeCategory size)` to `(string rawDogName, string? breed, SizeCategory size, int handlerId)`, matching the actual implementation in `src/AdwRating.Domain/Interfaces/IIdentityResolutionService.cs`. The extra `handlerId` parameter is a legitimate improvement that enables handler-scoped dog resolution for better matching accuracy.

## Notes

This is a docs-vs-code sync issue, not a bug. The implementation is arguably better than the spec.
