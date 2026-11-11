using System.ComponentModel.DataAnnotations;
using System.ClientModel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TeamForge.Agents;
using TeamForge.Application;
using TeamForge.Domain;

namespace TeamForge.Web.Pages;

public sealed class IndexModel(
    RequirementWorkflow workflow,
    ProjectService projectService,
    ILogger<IndexModel> logger,
    IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid? ProjectId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Enter a requirement to get started.")]
    [StringLength(12000, MinimumLength = 10, ErrorMessage = "Requirements must be between 10 and 12,000 characters.")]
    public string Requirement { get; set; } = string.Empty;

    [BindProperty, EnumDataType(typeof(AgentSeniority))]
    public AgentSeniority BusinessAnalystLevel { get; set; } = AgentSeniority.Mid;

    [BindProperty, StringLength(2000)]
    public string? BusinessAnalystGuidance { get; set; }

    [BindProperty, EnumDataType(typeof(AgentSeniority))]
    public AgentSeniority DeveloperLevel { get; set; } = AgentSeniority.Mid;

    [BindProperty, StringLength(2000)]
    public string? DeveloperGuidance { get; set; }

    [BindProperty, EnumDataType(typeof(AgentSeniority))]
    public AgentSeniority QaLevel { get; set; } = AgentSeniority.Mid;

    [BindProperty, StringLength(2000)]
    public string? QaGuidance { get; set; }

    [BindProperty, Required, StringLength(30)]
    public string ModelProvider { get; set; } = "Ollama";

    [BindProperty, Required, StringLength(120)]
    public string ModelName { get; set; } = "qwen3.5:9b";

    public Project? SelectedProject { get; private set; }
    public IReadOnlyList<Project> UserProjects { get; private set; } = [];
    public IReadOnlyList<RequirementSubmission> RecentSubmissions { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ModelProvider = configuration["Ai:Provider"] ?? "Ollama";
        ModelName = configuration["Ai:Model"] ?? "qwen3.5:9b";
        await LoadWorkspaceDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var username = CurrentUsername;
        if (!ModelState.IsValid)
        {
            await LoadWorkspaceDataAsync(cancellationToken);
            return Page();
        }

        try
        {
            var validProjectId = await ResolveValidProjectIdAsync(username, cancellationToken);
            var analysis = await workflow.AnalyzeAsync(
                Requirement,
                BuildAgentOptions(),
                cancellationToken,
                projectId: validProjectId,
                ownerUsername: username);
            return RedirectToPage("/Requirements/Details", new { id = analysis.Submission.Id });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "TeamForge could not analyze the submitted requirement using {Provider} model {Model}", ModelProvider, ModelName);
            ErrorMessage = GetAnalysisFailureMessage(exception, ModelProvider, ModelName, "analysis");
            await LoadWorkspaceDataAsync(cancellationToken);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAnalyzeAsync(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        await Response.StartAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            var message = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error))
                ?? "Check the requirement and role guidance fields, then try again.";
            await WriteProgressEventAsync(new { type = "error", message }, cancellationToken);
            return new EmptyResult();
        }

        var username = CurrentUsername;
        var currentRole = "Business Analyst";
        try
        {
            var validProjectId = await ResolveValidProjectIdAsync(username, cancellationToken);
            var analysis = await workflow.AnalyzeAsync(
                Requirement,
                BuildAgentOptions(),
                cancellationToken,
                progress =>
                {
                    currentRole = progress.Role;
                    return WriteProgressEventAsync(new
                    {
                        type = progress.Result is null ? "stage" : "result",
                        progress.Step,
                        progress.Role,
                        progress.Message,
                        progress.Result
                    }, cancellationToken);
                },
                projectId: validProjectId,
                ownerUsername: username);

            await WriteProgressEventAsync(new
            {
                type = "complete",
                redirectUrl = Url.Page("/Requirements/Details", pageHandler: null, values: new { id = analysis.Submission.Id })
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "TeamForge could not analyze the submitted requirement using {Provider} model {Model}", ModelProvider, ModelName);
            await WriteProgressEventAsync(new
            {
                type = "error",
                message = GetAnalysisFailureMessage(exception, ModelProvider, ModelName, currentRole)
            }, cancellationToken);
        }

        return new EmptyResult();
    }

    private string CurrentUsername => User.Identity?.Name ?? string.Empty;

    private async Task<Guid?> ResolveValidProjectIdAsync(string username, CancellationToken cancellationToken)
    {
        if (!ProjectId.HasValue)
            return null;

        var project = await projectService.GetAsync(ProjectId.Value, username, cancellationToken);
        return project?.Id;
    }

    private async Task LoadWorkspaceDataAsync(CancellationToken cancellationToken)
    {
        var username = CurrentUsername;
        UserProjects = await projectService.GetByOwnerAsync(username, cancellationToken);

        if (ProjectId.HasValue)
        {
            SelectedProject = UserProjects.FirstOrDefault(p => p.Id == ProjectId.Value);
            if (SelectedProject is null)
            {
                ProjectId = null;
            }
        }

        ViewData["ActiveProjectId"] = ProjectId;
        RecentSubmissions = await workflow.GetRecentForOwnerAsync(username, ProjectId, cancellationToken);
    }

    private RequirementAgentOptions BuildAgentOptions() => new(
        new(BusinessAnalystLevel, BusinessAnalystGuidance),
        new(DeveloperLevel, DeveloperGuidance),
        new(QaLevel, QaGuidance),
        new(ModelProvider, ModelName));

    private static string GetAnalysisFailureMessage(Exception exception, string provider, string model, string role) => exception switch
    {
        TaskCanceledException => "The AI model took longer than its configured timeout. Increase Ai:CloudRequestTimeoutSeconds for cloud providers or choose a faster model.",
        HttpRequestException => "TeamForge could not connect to the selected AI provider. Check its endpoint and availability.",
        ClientResultException { Status: 401 or 403 } => "The cloud AI provider rejected the API key. Check the provider's user-secret setting and create a replacement key if needed.",
        ClientResultException { Status: 404 } => $"The cloud AI provider could not find model '{model}'. Check the model ID in the workspace.",
        ClientResultException { Status: 429 } => "The cloud AI provider rate limit or quota was reached. Check the provider account's quota and try again later.",
        ClientResultException { Status: >= 500 } providerError => $"{provider} returned HTTP {providerError.Status} for model '{model}' during {role}. The provider is temporarily unavailable. Try again shortly or choose another model.",
        ClientResultException providerError => $"{provider} returned HTTP {providerError.Status} for model '{model}' during {role}. Check the model ID and server log for the provider's error details.",
        JsonException => $"{role} returned a response that was not valid JSON. Try again or use a different model.",
        InvalidOperationException when exception.Message.StartsWith($"{role} returned", StringComparison.Ordinal) => exception.Message,
        InvalidOperationException when exception.Message.StartsWith("QA output", StringComparison.Ordinal) => $"QA Engineer response did not meet TeamForge's test coverage checks: {exception.Message}",
        InvalidOperationException when exception.Message.StartsWith("Set Ai:", StringComparison.Ordinal) => exception.Message,
        _ => $"The {role} step could not be completed. Check the server log for details, then try again."
    };

    private async Task WriteProgressEventAsync(object progressEvent, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(progressEvent, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
