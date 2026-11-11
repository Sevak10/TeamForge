using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamForge.Agents;
using TeamForge.Application;
using TeamForge.Domain;

namespace TeamForge.Web.Pages.Requirements;

public sealed class DetailsModel(
    RequirementWorkflow workflow,
    ILogger<DetailsModel> logger,
    IConfiguration configuration) : PageModel
{
    public RequirementSubmission Submission { get; private set; } = default!;
    public BusinessAnalysisResult BusinessAnalysis { get; private set; } = default!;
    public DeveloperAnalysisResult DeveloperAnalysis { get; private set; } = default!;
    public QaAnalysisResult QaAnalysis { get; private set; } = default!;

    [BindProperty]
    public string? ReviewerNotes { get; set; }

    [BindProperty]
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Describe what you want the team to change or add.")]
    [System.ComponentModel.DataAnnotations.StringLength(4000, MinimumLength = 5)]
    public string RefinementInstruction { get; set; } = string.Empty;

    [BindProperty]
    [System.ComponentModel.DataAnnotations.RegularExpression("^(Ollama|Gemini|OpenAI)$")]
    public string ModelProvider { get; set; } = "Ollama";

    [BindProperty]
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120)]
    public string ModelName { get; set; } = "qwen3.5:9b";

    [TempData]
    public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelProvider = configuration["Ai:Provider"] ?? "Ollama";
        ModelName = configuration["Ai:Model"] ?? "qwen3.5:9b";

        if (!await LoadSubmissionAsync(id, cancellationToken))
            return NotFound();
        ViewData["ActiveProjectId"] = Submission.ProjectId;
        ViewData["ActiveSubmissionId"] = Submission.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveNotesAsync(Guid id, CancellationToken cancellationToken) =>
        await UpdateReviewAsync(id, approve: false, cancellationToken);

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken cancellationToken) =>
        await UpdateReviewAsync(id, approve: true, cancellationToken);

    public async Task<IActionResult> OnPostRefineAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (!await LoadSubmissionAsync(id, cancellationToken))
                return NotFound();
            return Page();
        }

        try
        {
            var username = User.Identity?.Name ?? string.Empty;
            var submission = await workflow.GetForOwnerAsync(id, username, cancellationToken);
            if (submission is null)
                return NotFound();

            var refinedRequirement = $"{submission.Requirement}\n\nRequested refinement: {RefinementInstruction.Trim()}";
            var options = new RequirementAgentOptions(
                new(AgentSeniority.Mid, null),
                new(AgentSeniority.Mid, null),
                new(AgentSeniority.Mid, null),
                new(ModelProvider, ModelName));

            var refinedAnalysis = await workflow.AnalyzeAsync(
                refinedRequirement, options, cancellationToken,
                projectId: submission.ProjectId,
                ownerUsername: submission.OwnerUsername);
            Notice = "A refined analysis was saved as a new review. The earlier analysis is still available.";
            return RedirectToPage(new { id = refinedAnalysis.Submission.Id });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not refine requirement analysis {SubmissionId}", id);
            ModelState.AddModelError(string.Empty, "The refined analysis could not be completed. Check the model settings and try again.");
            if (!await LoadSubmissionAsync(id, cancellationToken))
                return NotFound();
            return Page();
        }
    }

    private async Task<IActionResult> UpdateReviewAsync(Guid id, bool approve, CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? string.Empty;
        try
        {
            await workflow.SaveReviewAsync(id, ReviewerNotes, approve, cancellationToken, ownerUsername: username);
            Notice = approve ? "The analysis was approved." : "Your review notes were saved.";
            return RedirectToPage(new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not save review for requirement {SubmissionId}", id);
            ModelState.AddModelError(string.Empty, "Could not save your review. Please try again.");
            return await OnGetAsync(id, cancellationToken);
        }
    }

    private async Task<bool> LoadSubmissionAsync(Guid id, CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? string.Empty;
        var submission = await workflow.GetForOwnerAsync(id, username, cancellationToken);
        if (submission is null)
            return false;

        Submission = submission;
        ReviewerNotes = submission.ReviewerNotes;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        BusinessAnalysis = JsonSerializer.Deserialize<BusinessAnalysisResult>(submission.BusinessAnalysisJson, jsonOptions)!;
        DeveloperAnalysis = JsonSerializer.Deserialize<DeveloperAnalysisResult>(submission.DeveloperAnalysisJson, jsonOptions)!;
        QaAnalysis = JsonSerializer.Deserialize<QaAnalysisResult>(submission.QaAnalysisJson, jsonOptions)!;
        return true;
    }
}
