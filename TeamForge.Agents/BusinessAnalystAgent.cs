namespace TeamForge.Agents;

public sealed class BusinessAnalystAgent(IAgentChatClientFactory chatClients) : StructuredAgent<BusinessAnalysisInput, BusinessAnalysisResult>(chatClients)
{
    public override string Name => "Business Analyst";

    protected override string SystemPrompt => AgentPrompts.BusinessAnalyst;
}
