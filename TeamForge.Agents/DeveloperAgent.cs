namespace TeamForge.Agents;

public sealed class DeveloperAgent(IAgentChatClientFactory chatClients) : StructuredAgent<DeveloperAnalysisInput, DeveloperAnalysisResult>(chatClients)
{
    public override string Name => "Developer";

    protected override string SystemPrompt => AgentPrompts.Developer;
}
