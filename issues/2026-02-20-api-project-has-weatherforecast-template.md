# API project still has default WeatherForecast template code

- **Type**: improvement
- **Priority**: medium
- **Status**: open

## Description

`AdwRating.Api/Program.cs` still contains the default ASP.NET Core template code with the `/weatherforecast` endpoint and `WeatherForecast` record. This should be replaced with the actual API endpoints or cleaned up to be a minimal empty host ready for real controllers.

## Where to look

- `src/AdwRating.Api/Program.cs`

## Acceptance criteria

- [ ] Remove the WeatherForecast template code
- [ ] Set up minimal API host or MVC controller pattern ready for real endpoints
- [ ] Build passes

## Notes

The Api project references Domain, Service, and Data.Mssql in its csproj, so DI registration infrastructure is ready. Only the template code in Program.cs needs cleanup.
