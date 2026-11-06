using System.Text.Json;
using System.Text.Json.Serialization;
using TeamForge.Agents;
using TeamForge.Domain;

namespace TeamForge.Application;

public sealed record RequirementAnalysis(
    RequirementSubmission Submission,
    BusinessAnalysisResult BusinessAnalysis,
    DeveloperAnalysisResult DeveloperAnalysis,
    QaAnalysisResult QaAnalysis);

public sealed record AnalysisProgress(int Step, string Role, string Message, object? Result = null);

public sealed class RequirementWorkflow(
    IAgent<BusinessAnalysisInput, BusinessAnalysisResult> businessAnalyst,
    IAgent<DeveloperAnalysisInput, DeveloperAnalysisResult> developer,
    IAgent<QaAnalysisInput, QaAnalysisResult> qa,
    ISubmissionRepository submissions)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<RequirementAnalysis> AnalyzeAsync(string requirement, CancellationToken cancellationToken) =>
        AnalyzeAsync(requirement, RequirementAgentOptions.Default, cancellationToken);

    public async Task<RequirementAnalysis> AnalyzeAsync(
        string requirement,
        RequirementAgentOptions options,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress = null)
    {
        if (string.IsNullOrWhiteSpace(requirement))
            throw new ArgumentException("Enter a business requirement before submitting.", nameof(requirement));
        ValidateOptions(options);

        var cleanedRequirement = requirement.Trim();
        if (reportProgress is not null)
            await reportProgress(new(1, "Business Analyst", "Clarifying the requirement and drafting user stories."));
        var businessResult = await businessAnalyst.RunAsync(new(cleanedRequirement, options.BusinessAnalyst, options.Model), cancellationToken);
        ValidateBusinessResult(businessResult);
        businessResult = businessResult with { AppliedOptions = options.BusinessAnalyst };
        if (reportProgress is not null)
            await reportProgress(new(1, "Business Analyst", "Business Analyst response is ready.", businessResult));

        if (reportProgress is not null)
            await reportProgress(new(2, "Developer", "Planning the implementation and developer unit tests."));
        var developerResult = await developer.RunAsync(new(cleanedRequirement, businessResult, options.Developer, options.Model), cancellationToken);
        ValidateDeveloperResult(developerResult, businessResult);
        developerResult = developerResult with { AppliedOptions = options.Developer };
        if (reportProgress is not null)
            await reportProgress(new(2, "Developer", "Developer response is ready.", developerResult));

        if (reportProgress is not null)
            await reportProgress(new(3, "QA Engineer", "Checking acceptance, negative, boundary, and edge cases."));
        var qaInput = new QaAnalysisInput(cleanedRequirement, businessResult, developerResult, options.QaEngineer, options.Model);
        var qaResult = await qa.RunAsync(qaInput, cancellationToken);
        try
        {
            ValidateQaResult(qaResult, businessResult);
        }
        catch (InvalidOperationException validationException)
        {
            if (reportProgress is not null)
                await reportProgress(new(3, "QA Engineer", "QA coverage was incomplete. Asking QA to correct the missing scenarios."));
            qaResult = await qa.RunAsync(qaInput with { ValidationFeedback = validationException.Message }, cancellationToken);
            ValidateQaResult(qaResult, businessResult);
        }
        qaResult = qaResult with { AppliedOptions = options.QaEngineer };
        if (reportProgress is not null)
            await reportProgress(new(3, "QA Engineer", "QA Engineer response is ready.", qaResult));

        if (reportProgress is not null)
            await reportProgress(new(4, "Saving results", "Saving the team's analysis for your review."));
        var submission = new RequirementSubmission(cleanedRequirement, DateTimeOffset.UtcNow);
        submission.SetAnalysis(
            JsonSerializer.Serialize(businessResult, JsonOptions),
            JsonSerializer.Serialize(developerResult, JsonOptions),
            JsonSerializer.Serialize(qaResult, JsonOptions));

        await submissions.AddAsync(submission, cancellationToken);
        return new(submission, businessResult, developerResult, qaResult);
    }

    public Task<IReadOnlyList<RequirementSubmission>> GetRecentAsync(CancellationToken cancellationToken) =>
        submissions.GetRecentAsync(20, cancellationToken);

    public Task<RequirementSubmission?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        submissions.GetByIdAsync(id, cancellationToken);

    public async Task SaveReviewAsync(Guid id, string? notes, bool approve, CancellationToken cancellationToken)
    {
        var submission = await submissions.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("The requirement analysis was not found.");

        submission.SaveReviewerNotes(notes);
        if (approve)
            submission.Approve();
        await submissions.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateBusinessResult(BusinessAnalysisResult result)
    {
        if (result.ClarificationQuestions is null || result.UserStories is null || result.UserStories.Count == 0 ||
            result.UserStories.Any(story => story is null || string.IsNullOrWhiteSpace(story.Id) ||
                string.IsNullOrWhiteSpace(story.Title) || string.IsNullOrWhiteSpace(story.AsA) ||
                string.IsNullOrWhiteSpace(story.IWant) || string.IsNullOrWhiteSpace(story.SoThat) ||
                story.AcceptanceCriteria is null || story.AcceptanceCriteria.Count == 0 ||
                story.AcceptanceCriteria.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidOperationException("Business Analyst output must include user stories with acceptance criteria.");
    }

    private static void ValidateOptions(RequirementAgentOptions options)
    {
        if (options is null || options.BusinessAnalyst is null || options.Developer is null || options.QaEngineer is null || options.Model is null ||
            !Enum.IsDefined(options.BusinessAnalyst.Level) || !Enum.IsDefined(options.Developer.Level) || !Enum.IsDefined(options.QaEngineer.Level))
            throw new ArgumentException("Choose Junior, Mid, or Senior for each agent.", nameof(options));

        if (options.Model.Provider is not ("Ollama" or "Gemini" or "OpenAI") || string.IsNullOrWhiteSpace(options.Model.ModelName) || options.Model.ModelName.Length > 120)
            throw new ArgumentException("Choose Ollama, Gemini, or OpenAI and enter a model name (up to 120 characters).", nameof(options));

        if (new[] { options.BusinessAnalyst.AdditionalGuidance, options.Developer.AdditionalGuidance, options.QaEngineer.AdditionalGuidance }
            .Any(guidance => guidance?.Length > 2000))
            throw new ArgumentException("Additional guidance must be 2,000 characters or fewer for each agent.", nameof(options));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static void ValidateDeveloperResult(DeveloperAnalysisResult result, BusinessAnalysisResult business)
    {
        var storyIds = business.UserStories.Select(story => story.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (result.ClarificationQuestions is null || result.Dependencies is null || string.IsNullOrWhiteSpace(result.ImplementationApproach) ||
            result.UnitTestPlans is null || storyIds.Any(id => !result.UnitTestPlans.Any(plan =>
                string.Equals(plan.UserStoryId, id, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(plan.Target) && plan.Tests is { Count: > 0 })))
            throw new InvalidOperationException("Developer output must include a unit test plan for every user story.");
    }

    private static void ValidateQaResult(QaAnalysisResult result, BusinessAnalysisResult business)
    {
        var storyIds = business.UserStories.Select(story => story.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (result.ClarificationQuestions is null || result.TestCases is null)
            throw new InvalidOperationException("QA output must include clarification questions and acceptance test cases.");

        var requiredCategories = new[] { "Positive", "Negative", "Boundary", "Edge" };
        foreach (var id in storyIds)
        {
            var storyCases = result.TestCases.Where(test => string.Equals(test.UserStoryId, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (storyCases.Any(test => string.IsNullOrWhiteSpace(test.Scenario) || string.IsNullOrWhiteSpace(test.ExpectedResult) || test.Steps is not { Count: > 0 }))
                throw new InvalidOperationException($"QA output contains an incomplete acceptance test case for {id}.");

            if (requiredCategories.Any(category => !storyCases.Any(test => string.Equals(test.Category, category, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException($"QA output must cover positive, negative, boundary, and edge cases for {id}.");
        }
    }
}
