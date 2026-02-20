# SeedConfigCommand directly uses AppDbContext (architecture violation)

- **Type**: improvement
- **Priority**: high
- **Status**: resolved

## Description

`SeedConfigCommand.cs` directly resolves `AppDbContext` via DI and calls `dbContext.Database.EnsureCreatedAsync()` on line 29. This violates the architecture rule: "Never use AppDbContext or any Data.* types outside of Data.Mssql (except DI registration in Program.cs of host projects)."

The CLI is allowed to reference `Data.Mssql` for DI registration (`services.AddDataMssql()`), but it should not resolve or use `AppDbContext` directly in command logic.

## Where to look

- `src/AdwRating.Cli/Commands/SeedConfigCommand.cs` line 29

## Acceptance criteria

- [x] Remove the direct `AppDbContext` usage from `SeedConfigCommand`
- [x] Either expose an `EnsureDatabaseAsync()` method on a repository/service interface, or move database initialization to the `AddDataMssql` extension method
- [x] Build passes with no direct `AppDbContext` references in `AdwRating.Cli` (other than DI registration namespace import)

## Resolution

Created `IDatabaseInitializer` interface in `Domain/Interfaces/` with `EnsureCreatedAsync()` method. Implemented it in `Data.Mssql/DatabaseInitializer.cs` which wraps `AppDbContext.Database.EnsureCreatedAsync()`. Registered it in `ServiceCollectionExtensions.AddDataMssql()`. Updated `SeedConfigCommand` to resolve `IDatabaseInitializer` instead of `AppDbContext` directly. No more `AppDbContext` references in the CLI project outside of DI namespace imports.

## Notes

The `using Microsoft.EntityFrameworkCore` import was already removed as it became unused. The `AppDbContext` usage itself still remains and needs to be addressed by introducing a proper abstraction.
