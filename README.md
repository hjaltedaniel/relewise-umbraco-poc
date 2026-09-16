# Relewise Umbraco PoC

Umbraco 17 proof of concept using the Relewise integration and a custom content mapper for extracting readable text from Block List properties.

## Local setup

Requires the .NET 10 SDK.

1. Copy `UmbracoPoC.Web/appsettings.example.json` to `UmbracoPoC.Web/appsettings.json`.
2. Copy `UmbracoPoC.Web/appsettings.Development.example.json` to `UmbracoPoC.Web/appsettings.Development.json`.
3. Set the Relewise dataset ID, API key, and server URL in your local configuration.
4. Run `dotnet restore UmbracoPoC.slnx`, then `dotnet run --project UmbracoPoC.Web`.
5. Complete the Umbraco installation for a new local database.

Local configuration, credentials, databases, uploaded media, logs, and generated files are excluded from Git. The original site's content and editor configuration live in its local database; cloning this repository does not reproduce that content. The installed Clean starter kit provides the starting site for a new installation.

## Mapping

`Program.cs` registers `RelewiseContentMapper` through `UseMapper(...)`. Ordinary properties use Relewise's standard conversion; Block List properties use the custom text extractor in `UmbracoPoC.Web/Relewise`.

Build with `dotnet build UmbracoPoC.slnx`.

The optional `--relewise-blocklist-self-test` application argument checks specific content IDs from the original local PoC database; it is not a general test suite for a fresh installation.
