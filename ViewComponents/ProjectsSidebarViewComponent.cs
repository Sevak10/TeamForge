using Microsoft.AspNetCore.Mvc;
using TeamForge.Application;
using TeamForge.Domain;

namespace TeamForge.Web.ViewComponents;

public sealed record ProjectsSidebarViewModel(
    string Username,
    IReadOnlyList<Project> Projects,
    Guid? ActiveProjectId,
    Guid? ActiveSubmissionId,
    string? ProjectError);

public sealed class ProjectsSidebarViewComponent(ProjectService projectService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (HttpContext.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(HttpContext.User.Identity.Name))
        {
            return Content(string.Empty);
        }

        var username = HttpContext.User.Identity.Name;
        var projects = await projectService.GetByOwnerAsync(username, HttpContext.RequestAborted);

        Guid? activeProjectId = ViewContext.ViewData["ActiveProjectId"] as Guid?;
        if (!activeProjectId.HasValue && Guid.TryParse(HttpContext.Request.Query["projectId"], out var queryProjectId))
        {
            activeProjectId = queryProjectId;
        }

        Guid? activeSubmissionId = ViewContext.ViewData["ActiveSubmissionId"] as Guid?;
        if (!activeSubmissionId.HasValue &&
            ViewContext.RouteData.Values.TryGetValue("id", out var routeIdObj) &&
            Guid.TryParse(routeIdObj?.ToString(), out var routeSubmissionId))
        {
            activeSubmissionId = routeSubmissionId;
        }

        string? projectError = ViewContext.TempData.Peek("ProjectError") as string;
        if (projectError is not null)
        {
            ViewContext.TempData.Remove("ProjectError");
        }

        return View(new ProjectsSidebarViewModel(username, projects, activeProjectId, activeSubmissionId, projectError));
    }
}
