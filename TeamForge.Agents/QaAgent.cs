namespace TeamForge.Agents;

public sealed class QaAgent(IAgentChatClientFactory chatClients) : StructuredAgent<QaAnalysisInput, QaAnalysisResult>(chatClients)
{
    public override string Name => "QA Engineer";

    protected override string SystemPrompt => AgentPrompts.QaEngineer;
}
