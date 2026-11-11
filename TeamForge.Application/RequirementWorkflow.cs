using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = AnalysisJsonOptions.Create();

    public Task<RequirementAnalysis> AnalyzeAsync(string requirement, CancellationToken cancellationToken) =>
        AnalyzeAsync(requirement, RequirementAgentOptions.Default, cancellationToken);

    public async Task<RequirementAnalysis> AnalyzeAsync(
        string requirement,
        RequirementAgentOptions options,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress = null,
        Guid? projectId = null,
        string? ownerUsername = null)
    {
        var cleanedRequirement = ValidateAndCleanRequirement(requirement);
        RequirementAnalysisValidator.ValidateOptions(options);

        var businessResult = await AnalyzeBusinessRequirementAsync(
            cleanedRequirement, options, cancellationToken, reportProgress);

        var developerResult = await CreateImplementationPlanAsync(
            cleanedRequirement, businessResult, options, cancellationToken, reportProgress);

        var qaResult = await CreateAndValidateQaPlanAsync(
            cleanedRequirement, businessResult, developerResult, options, cancellationToken, reportProgress);

        var submission = await SaveAnalysisAsync(
            cleanedRequirement, businessResult, developerResult, qaResult, cancellationToken, reportProgress,
            projectId, ownerUsername);

        return new RequirementAnalysis(submission, businessResult, developerResult, qaResult);
    }

    private async Task<BusinessAnalysisResult> AnalyzeBusinessRequirementAsync(
        string requirement,
        RequirementAgentOptions options,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress)
    {
        await ReportProgressAsync(reportProgress,
            new(1, "Business Analyst", "Clarifying the requirement and drafting user stories."));

        var input = new BusinessAnalysisInput(requirement, options.BusinessAnalyst, options.Model);
        var result = await businessAnalyst.RunAsync(input, cancellationToken);
        RequirementAnalysisValidator.ValidateBusinessResult(result);

        var completedResult = result with { AppliedOptions = options.BusinessAnalyst };
        await ReportProgressAsync(reportProgress,
            new(1, "Business Analyst", "Business Analyst response is ready.", completedResult));

        return completedResult;
    }

    private async Task<DeveloperAnalysisResult> CreateImplementationPlanAsync(
        string requirement,
        BusinessAnalysisResult businessResult,
        RequirementAgentOptions options,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress)
    {
        await ReportProgressAsync(reportProgress,
            new(2, "Developer", "Planning the implementation and developer unit tests."));

        var input = new DeveloperAnalysisInput(requirement, businessResult, options.Developer, options.Model);
        var result = await developer.RunAsync(input, cancellationToken);
        RequirementAnalysisValidator.ValidateDeveloperResult(result, businessResult);

        var completedResult = result with { AppliedOptions = options.Developer };
        await ReportProgressAsync(reportProgress,
            new(2, "Developer", "Developer response is ready.", completedResult));

        return completedResult;
    }

    private async Task<QaAnalysisResult> CreateAndValidateQaPlanAsync(
        string requirement,
        BusinessAnalysisResult businessResult,
        DeveloperAnalysisResult developerResult,
        RequirementAgentOptions options,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress)
    {
        await ReportProgressAsync(reportProgress,
            new(3, "QA Engineer", "Checking acceptance, negative, boundary, and edge cases."));

        var input = new QaAnalysisInput(
            requirement, businessResult, developerResult, options.QaEngineer, options.Model);
        var result = await qa.RunAsync(input, cancellationToken);
        result = await RetryQaWhenCoverageIsIncompleteAsync(
            input, result, businessResult, cancellationToken, reportProgress);

        var completedResult = result with { AppliedOptions = options.QaEngineer };
        await ReportProgressAsync(reportProgress,
            new(3, "QA Engineer", "QA Engineer response is ready.", completedResult));

        return completedResult;
    }

    private async Task<QaAnalysisResult> RetryQaWhenCoverageIsIncompleteAsync(
        QaAnalysisInput input,
        QaAnalysisResult result,
        BusinessAnalysisResult businessResult,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress)
    {
        try
        {
            RequirementAnalysisValidator.ValidateQaResult(result, businessResult);
            return result;
        }
        catch (InvalidOperationException validationException)
        {
            await ReportProgressAsync(reportProgress,
                new(3, "QA Engineer", "QA coverage was incomplete. Asking QA to correct the missing scenarios."));

            var correctedInput = input with { ValidationFeedback = validationException.Message };
            var correctedResult = await qa.RunAsync(correctedInput, cancellationToken);
            RequirementAnalysisValidator.ValidateQaResult(correctedResult, businessResult);
            return correctedResult;
        }
    }

    private async Task<RequirementSubmission> SaveAnalysisAsync(
        string requirement,
        BusinessAnalysisResult businessResult,
        DeveloperAnalysisResult developerResult,
        QaAnalysisResult qaResult,
        CancellationToken cancellationToken,
        Func<AnalysisProgress, Task>? reportProgress,
        Guid? projectId,
        string? ownerUsername)
    {
        await ReportProgressAsync(reportProgress,
            new(4, "Saving results", "Saving the team's analysis for your review."));

        var submission = new RequirementSubmission(requirement, DateTimeOffset.UtcNow, projectId, ownerUsername);
        submission.SetAnalysis(
            JsonSerializer.Serialize(businessResult, JsonOptions),
            JsonSerializer.Serialize(developerResult, JsonOptions),
            JsonSerializer.Serialize(qaResult, JsonOptions));

        await submissions.AddAsync(submission, cancellationToken);
        return submission;
    }

    private static string ValidateAndCleanRequirement(string requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement))
        {
            throw new ArgumentException("Enter a business requirement before submitting.", nameof(requirement));
        }

        return requirement.Trim();
    }

    private static Task ReportProgressAsync(
        Func<AnalysisProgress, Task>? reportProgress,
        AnalysisProgress progress) =>
        reportProgress is null ? Task.CompletedTask : reportProgress(progress);

    public Task<IReadOnlyList<RequirementSubmission>> GetRecentAsync(CancellationToken cancellationToken) =>
        submissions.GetRecentAsync(20, cancellationToken);

    public Task<IReadOnlyList<RequirementSubmission>> GetRecentForOwnerAsync(string ownerUsername, Guid? projectId, CancellationToken cancellationToken) =>
        submissions.GetRecentByOwnerAsync(ownerUsername, projectId, 30, cancellationToken);

    public Task<RequirementSubmission?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        submissions.GetByIdAsync(id, cancellationToken);

    public Task<RequirementSubmission?> GetForOwnerAsync(Guid id, string ownerUsername, CancellationToken cancellationToken) =>
        submissions.GetByIdForOwnerAsync(id, ownerUsername, cancellationToken);

    public async Task SaveReviewAsync(Guid id, string? notes, bool approve, CancellationToken cancellationToken, string? ownerUsername = null)
    {
        var submission = (ownerUsername is null
            ? await submissions.GetByIdAsync(id, cancellationToken)
            : await submissions.GetByIdForOwnerAsync(id, ownerUsername, cancellationToken))
            ?? throw new KeyNotFoundException("The requirement analysis was not found.");

        submission.SaveReviewerNotes(notes);
        if (approve)
            submission.Approve();
        await submissions.SaveChangesAsync(cancellationToken);
    }
}
