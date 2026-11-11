using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamForge.Application;

namespace TeamForge.Web.Controllers;

[Authorize]
[Route("projects")]
public sealed class ProjectsController(ProjectService projectService, ILogger<ProjectsController> logger) : Controller
{
    public sealed class CreateProjectInput
    {
        [Required(ErrorMessage = "Project name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Project name must be between 2 and 120 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CreateProjectInput input, CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Challenge();

        if (!ModelState.IsValid)
        {
            TempData["ProjectError"] = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Enter a valid project name.";
            return RedirectToPage("/Index");
        }

        try
        {
            var project = await projectService.CreateAsync(input.Name, username, input.Description, cancellationToken);
            return RedirectToPage("/Index", new { projectId = project.Id });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to create project for user {Username}", username);
            TempData["ProjectError"] = exception.Message;
            return RedirectToPage("/Index");
        }
    }
}
