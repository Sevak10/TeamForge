using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OllamaSharp;
using System.ClientModel;
using System.ClientModel.Primitives;
using TeamForge.Agents;
using TeamForge.Application;

namespace TeamForge.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTeamForgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TeamForge")
            ?? throw new InvalidOperationException("ConnectionStrings:TeamForge must be configured.");
        services.AddDbContext<TeamForgeDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<IAgentChatClientFactory, AgentChatClientFactory>();
        return services;
    }
}

internal sealed class AgentChatClientFactory(IConfiguration configuration) : IAgentChatClientFactory, IDisposable
{
    private readonly Dictionary<string, IChatClient> clients = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<HttpClient> httpClients = [];

    public IChatClient Create(AiModelSelection selection)
    {
        var provider = selection.Provider.Trim();
        var model = selection.ModelName.Trim();
        var key = $"{provider}|{model}";
        if (clients.TryGetValue(key, out var existing)) return existing;

        IChatClient client = provider.ToUpperInvariant() switch
        {
            "OLLAMA" => CreateOllama(model),
            "OPENAI" => CreateOpenAi(model, "Ai:ApiKey", null),
            "GEMINI" => CreateOpenAi(model, "Ai:GeminiApiKey", "https://generativelanguage.googleapis.com/v1beta/openai/"),
            _ => throw new InvalidOperationException($"Unsupported AI provider '{provider}'. Choose Ollama, Gemini, or OpenAI.")
        };
        clients.Add(key, client);
        return client;
    }

    private IChatClient CreateOllama(string model)
    {
        var endpoint = new Uri(configuration["Ai:Endpoint"] ?? "http://localhost:11434");
        var timeoutSetting = configuration["Ai:RequestTimeoutSeconds"];
        var timeoutSeconds = timeoutSetting is null ? 600 : int.TryParse(timeoutSetting, out var value)
            ? value
            : throw new InvalidOperationException("Ai:RequestTimeoutSeconds must be a whole number.");
        if (timeoutSeconds <= 0) throw new InvalidOperationException("Ai:RequestTimeoutSeconds must be greater than zero.");

        var httpClient = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        httpClients.Add(httpClient);
        return new OllamaApiClient(httpClient, model);
    }

    private IChatClient CreateOpenAi(string model, string apiKeySetting, string? endpoint)
    {
        var apiKey = configuration[apiKeySetting];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("replace-with-", StringComparison.OrdinalIgnoreCase))
        {
            var envName = apiKeySetting == "Ai:GeminiApiKey" ? "Ai__GeminiApiKey" : "Ai__ApiKey";
            var provider = apiKeySetting == "Ai:GeminiApiKey" ? "Gemini" : "OpenAI";
            throw new InvalidOperationException($"Set {apiKeySetting} with user secrets or the {envName} environment variable when using {provider}.");
        }

        var timeoutSetting = configuration["Ai:CloudRequestTimeoutSeconds"];
        var timeoutSeconds = timeoutSetting is null ? 180 : int.TryParse(timeoutSetting, out var configuredTimeout)
            ? configuredTimeout
            : throw new InvalidOperationException("Ai:CloudRequestTimeoutSeconds must be a whole number.");
        if (timeoutSeconds <= 0) throw new InvalidOperationException("Ai:CloudRequestTimeoutSeconds must be greater than zero.");

        var options = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0)
        };
        if (endpoint is not null) options.Endpoint = new Uri(endpoint);
        var credential = new ApiKeyCredential(apiKey);
        var openAi = new OpenAIClient(credential, options);
        return openAi.GetChatClient(model).AsIChatClient();
    }

    public void Dispose()
    {
        foreach (var client in clients.Values.Distinct()) client.Dispose();
        foreach (var httpClient in httpClients) httpClient.Dispose();
        clients.Clear();
        httpClients.Clear();
    }
}
