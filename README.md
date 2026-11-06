# TeamForge

TeamForge is a Razor Pages application that turns a business requirement into a reviewable delivery draft. Its Business Analyst, Developer, and QA agents run in order and return structured results. A reviewer can save notes or approve the analysis.

## Run locally

Requirements: .NET 10 SDK, SQL Server or SQL Server LocalDB, and an AI provider.

1. Start SQL Server / LocalDB. `ConnectionStrings:TeamForge` in `appsettings.json` decides the instance and database. This workspace is currently configured for `(localdb)\\ProjectModels` and database `TeamForgeProjectModels`.
2. For Ollama, start the Ollama service and pull the model named by `Ai:Model` (for example, `ollama pull qwen3.5:9b`).
3. Run `dotnet run --project TeamForge.Web.csproj` from this directory.

EF Core migrations create and update the database schema. The initial `InitialCreate` migration is included and applied to the configured LocalDB database. On app startup, pending migrations are applied with `MigrateAsync`.

For later model changes, create and apply a migration from this directory:

```powershell
dotnet ef migrations add DescribeYourChange --project TeamForge.Infrastructure/TeamForge.Infrastructure.csproj --startup-project TeamForge.Web.csproj --context TeamForgeDbContext --output-dir Migrations
dotnet ef database update --project TeamForge.Infrastructure/TeamForge.Infrastructure.csproj --startup-project TeamForge.Web.csproj --context TeamForgeDbContext
```

## Switch AI provider

Choose Ollama, Gemini, or OpenAI in the workspace's AI model section and enter the model ID for that analysis. Ollama uses `Ai:Endpoint` and `Ai:RequestTimeoutSeconds` from `appsettings.json`. Cloud keys stay on the server: configure `Ai:GeminiApiKey` or `Ai:ApiKey` with .NET user-secrets or the `Ai__GeminiApiKey` / `Ai__ApiKey` environment variables. Never place cloud keys in the browser or checked-in settings. Gemini uses Google's OpenAI-compatible API endpoint through the existing Microsoft.Extensions.AI adapter.

For Ollama, `Ai:RequestTimeoutSeconds` controls how long a slow local model can take before TeamForge stops the request. It defaults to 600 seconds.

Set a cloud key locally with user-secrets, for example `dotnet user-secrets set "Ai:GeminiApiKey" "your-api-key" --project TeamForge.Web.csproj` or `dotnet user-secrets set "Ai:ApiKey" "your-api-key" --project TeamForge.Web.csproj`. Never commit API keys. The selected provider and model apply to all three agent roles for that analysis.

To use a SQL Server other than LocalDB, change `ConnectionStrings:TeamForge` in `appsettings.json` or override it with `ConnectionStrings__TeamForge`.

## Verify

```powershell
dotnet build TeamForge.slnx
dotnet test TeamForge.slnx
```

The tests exercise agent ordering, output validation, and persistence handoff without requiring a live AI provider.
