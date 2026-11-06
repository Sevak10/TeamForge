using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace TeamForge.Agents;

public abstract class StructuredAgent<TInput, TOutput>(IAgentChatClientFactory chatClients) : IAgent<TInput, TOutput>
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public abstract string Name { get; }
    protected abstract string SystemPrompt { get; }

    public async Task<TOutput> RunAsync(TInput input, CancellationToken cancellationToken)
    {
        if (input is not IAiModelInput modelInput)
            throw new InvalidOperationException("Agent input must specify an AI provider and model.");
        var chatClient = chatClients.Create(modelInput.Model);
        var userMessage = JsonSerializer.Serialize(input, JsonOptions);
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, SystemPrompt), new ChatMessage(ChatRole.User, userMessage)],
            new ChatOptions { ResponseFormat = ChatResponseFormat.Json, MaxOutputTokens = 3500 },
            cancellationToken);

        var output = JsonSerializer.Deserialize<TOutput>(response.Text, JsonOptions);
        return output ?? throw new InvalidOperationException($"{Name} returned an empty or invalid JSON response.");
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
