# API project still has default WeatherForecast template code

- **Type**: improvement
- **Priority**: medium
- **Status**: resolved

## Description

`AdwRating.Api/Program.cs` still contains the default ASP.NET Core template code with the `/weatherforecast` endpoint and `WeatherForecast` record. This should be replaced with the actual API endpoints or cleaned up to be a minimal empty host ready for real controllers.

## Where to look

- `src/AdwRating.Api/Program.cs`

## Acceptance criteria

- [x] Remove the WeatherForecast template code
- [x] Set up minimal API host or MVC controller pattern ready for real endpoints
- [x] Build passes

## Resolution

Removed the WeatherForecast record, summaries array, and /weatherforecast endpoint from Program.cs. The file now contains only the minimal WebApplication builder/run setup with HTTPS redirection, ready for real controllers and endpoints.

## Notes

The Api project references Domain, Service, and Data.Mssql in its csproj, so DI registration infrastructure is ready. Only the template code in Program.cs needs cleanup.
