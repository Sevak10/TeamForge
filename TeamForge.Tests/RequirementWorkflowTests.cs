using TeamForge.Agents;
using TeamForge.Application;
using TeamForge.Domain;
using Xunit;

namespace TeamForge.Tests;

public sealed class RequirementWorkflowTests
{
    [Fact]
    public async Task AnalyzeAsync_RunsAgentsInOrder_ValidatesCoverage_AndSavesResults()
    {
        var order = new List<string>();
        var story = new UserStory("US-01", "Reset password", "customer", "reset my password", "I can regain access", ["A reset link is sent"]);
        var business = new BusinessAnalysisResult([], [story]);
        var developer = new DeveloperAnalysisResult([], [], "Use a time-limited token.",
            [new UnitTestPlan("US-01", "Reset token service", [new TestSpecification("Expires", "Token older than 30 minutes", "Validate token", "Token is rejected")])]);
        var qa = new QaAnalysisResult([], [
            new QaTestCase("US-01", "Positive", "Valid token", ["Open valid link"], "Password reset form opens"),
            new QaTestCase("US-01", "Negative", "Invalid token", ["Open malformed link"], "Link is rejected"),
            new QaTestCase("US-01", "Boundary", "Expired token", ["Open expired link"], "User sees an expired-link message"),
            new QaTestCase("US-01", "Edge", "Token already used", ["Open used link"], "Link is rejected")]);
        var repository = new InMemorySubmissionRepository();
        var progress = new List<AnalysisProgress>();

        var workflow = new RequirementWorkflow(
            new StubAgent<BusinessAnalysisInput, BusinessAnalysisResult>("Business Analyst", order, business),
            new StubAgent<DeveloperAnalysisInput, DeveloperAnalysisResult>("Developer", order, developer),
            new StubAgent<QaAnalysisInput, QaAnalysisResult>("QA Engineer", order, qa),
            repository);

        var selectedOptions = new RequirementAgentOptions(
            new(AgentSeniority.Senior, "Focus on user identity and auditability."),
            new(AgentSeniority.Junior, "Prefer concrete .NET implementation steps."),
            new(AgentSeniority.Mid, "Emphasize mobile browser coverage."),
            new("Ollama", "qwen3.5:9b"));
        var result = await workflow.AnalyzeAsync(
            "Customers can reset their password by email.",
            selectedOptions,
            CancellationToken.None,
            update =>
            {
                progress.Add(update);
                return Task.CompletedTask;
            });

        Assert.Equal(new[] { "Business Analyst", "Developer", "QA Engineer" }, order);
        Assert.Equal("AwaitingReview", result.Submission.Status.ToString());
        Assert.Equal(result.Submission.Id, repository.Saved!.Id);
        Assert.Contains("US-01", result.Submission.BusinessAnalysisJson);
        Assert.Contains("Reset token service", result.Submission.DeveloperAnalysisJson);
        Assert.Contains("Expired token", result.Submission.QaAnalysisJson);
        Assert.Equal(selectedOptions.BusinessAnalyst, result.BusinessAnalysis.AppliedOptions);
        Assert.Equal(selectedOptions.Developer, result.DeveloperAnalysis.AppliedOptions);
        Assert.Equal(selectedOptions.QaEngineer, result.QaAnalysis.AppliedOptions);
        Assert.Contains("user identity", result.Submission.BusinessAnalysisJson);
        Assert.Contains("junior", result.Submission.DeveloperAnalysisJson);
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 3, 4 }, progress.Select(update => update.Step));
        Assert.Equal(new[] { "Business Analyst", "Developer", "QA Engineer" },
            progress.Where(update => update.Result is not null).Select(update => update.Role));
    }

    [Fact]
    public async Task AnalyzeAsync_StopsWhenBusinessAnalysisHasNoStories()
    {
        var order = new List<string>();
        var workflow = new RequirementWorkflow(
            new StubAgent<BusinessAnalysisInput, BusinessAnalysisResult>("Business Analyst", order, new BusinessAnalysisResult([], [])),
            new StubAgent<DeveloperAnalysisInput, DeveloperAnalysisResult>("Developer", order, new DeveloperAnalysisResult([], [], "", [])),
            new StubAgent<QaAnalysisInput, QaAnalysisResult>("QA Engineer", order, new QaAnalysisResult([], [])),
            new InMemorySubmissionRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.AnalyzeAsync("A requirement with no stories.", CancellationToken.None));
        Assert.Equal(new[] { "Business Analyst" }, order);
    }

    private sealed class StubAgent<TInput, TOutput>(string name, List<string> order, TOutput output) : IAgent<TInput, TOutput>
    {
        public string Name => name;

        public Task<TOutput> RunAsync(TInput input, CancellationToken cancellationToken)
        {
            order.Add(name);
            return Task.FromResult(output);
        }
    }

    private sealed class InMemorySubmissionRepository : ISubmissionRepository
    {
        public RequirementSubmission? Saved { get; private set; }

        public Task AddAsync(RequirementSubmission submission, CancellationToken cancellationToken)
        {
            Saved = submission;
            return Task.CompletedTask;
        }

        public Task<RequirementSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Saved?.Id == id ? Saved : null);

        public Task<IReadOnlyList<RequirementSubmission>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RequirementSubmission>>(Saved is null ? [] : [Saved]);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
