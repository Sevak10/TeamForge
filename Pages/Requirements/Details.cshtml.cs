using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamForge.Agents;
using TeamForge.Application;
using TeamForge.Domain;

namespace TeamForge.Web.Pages.Requirements;

public sealed class DetailsModel(RequirementWorkflow workflow, ILogger<DetailsModel> logger) : PageModel
{
    public RequirementSubmission Submission { get; private set; } = default!;
    public BusinessAnalysisResult BusinessAnalysis { get; private set; } = default!;
    public DeveloperAnalysisResult DeveloperAnalysis { get; private set; } = default!;
    public QaAnalysisResult QaAnalysis { get; private set; } = default!;

    [BindProperty]
    public string? ReviewerNotes { get; set; }

    [TempData]
    public string? Notice { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name ?? string.Empty;
        var submission = await workflow.GetForOwnerAsync(id, username, cancellationToken);
        if (submission is null)
            return NotFound();

        Submission = submission;
        ViewData["ActiveProjectId"] = submission.ProjectId;
        ViewData["ActiveSubmissionId"] = submission.Id;
        ReviewerNotes = submission.ReviewerNotes;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        BusinessAnalysis = JsonSerializer.Deserialize<BusinessAnalysisResult>(submission.BusinessAnalysisJson, jsonOptions)!;
        DeveloperAnalysis = JsonSerializer.Deserialize<DeveloperAnalysisResult>(submission.DeveloperAnalysisJson, jsonOptions)!;
        QaAnalysis = JsonSerializer.Deserialize<QaAnalysisResult>(submission.QaAnalysisJson, jsonOptions)!;
        return Page();
    }

    public async Task<IActionResult> OnPostSaveNotesAsync(Guid id, CancellationToken cancellationToken) =>
        await UpdateReviewAsync(id, approve: false, cancellationToken);

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken cancellationToken) =>
        await UpdateReviewAsync(id, approve: true, cancellationToken);

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
}
