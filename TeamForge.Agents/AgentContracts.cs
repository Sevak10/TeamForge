using Microsoft.Extensions.AI;

namespace TeamForge.Agents;

public interface IAgent<in TInput, TOutput>
{
    string Name { get; }
    Task<TOutput> RunAsync(TInput input, CancellationToken cancellationToken);
}

public enum AgentSeniority
{
    Junior,
    Mid,
    Senior
}

public sealed record AgentRunOptions(AgentSeniority Level, string? AdditionalGuidance);

public sealed record AiModelSelection(string Provider, string ModelName);

public interface IAiModelInput
{
    AiModelSelection Model { get; }
}

public interface IAgentChatClientFactory
{
    IChatClient Create(AiModelSelection selection);
}

public sealed record RequirementAgentOptions(
    AgentRunOptions BusinessAnalyst,
    AgentRunOptions Developer,
    AgentRunOptions QaEngineer,
    AiModelSelection Model)
{
    public static RequirementAgentOptions Default { get; } = new(
        new(AgentSeniority.Mid, null),
        new(AgentSeniority.Mid, null),
        new(AgentSeniority.Mid, null),
        new("Ollama", "qwen3.5:9b"));
}

public sealed record BusinessAnalysisInput(string Requirement, AgentRunOptions Options, AiModelSelection Model) : IAiModelInput;
public sealed record BusinessAnalysisResult(IReadOnlyList<string> ClarificationQuestions, IReadOnlyList<UserStory> UserStories)
{
    public AgentRunOptions? AppliedOptions { get; init; }
}
public sealed record UserStory(string Id, string Title, string AsA, string IWant, string SoThat, IReadOnlyList<string> AcceptanceCriteria);

public sealed record DeveloperAnalysisInput(string Requirement, BusinessAnalysisResult BusinessAnalysis, AgentRunOptions Options, AiModelSelection Model) : IAiModelInput;
public sealed record DeveloperAnalysisResult(
    IReadOnlyList<string> ClarificationQuestions,
    IReadOnlyList<string> Dependencies,
    string ImplementationApproach,
    IReadOnlyList<UnitTestPlan> UnitTestPlans)
{
    public AgentRunOptions? AppliedOptions { get; init; }
}
public sealed record UnitTestPlan(string UserStoryId, string Target, IReadOnlyList<TestSpecification> Tests);
public sealed record TestSpecification(string Name, string Setup, string Action, string ExpectedResult);

public sealed record QaAnalysisInput(
    string Requirement,
    BusinessAnalysisResult BusinessAnalysis,
    DeveloperAnalysisResult DeveloperAnalysis,
    AgentRunOptions Options,
    AiModelSelection Model,
    string? ValidationFeedback = null) : IAiModelInput;
public sealed record QaAnalysisResult(IReadOnlyList<string> ClarificationQuestions, IReadOnlyList<QaTestCase> TestCases)
{
    public AgentRunOptions? AppliedOptions { get; init; }
}
public sealed record QaTestCase(string UserStoryId, string Category, string Scenario, IReadOnlyList<string> Steps, string ExpectedResult);
