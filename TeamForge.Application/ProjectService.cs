using TeamForge.Domain;

namespace TeamForge.Application;

public sealed class ProjectService(IProjectRepository projects)
{
    public async Task<Project> CreateAsync(string name, string ownerUsername, string? description, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));
        if (name.Trim().Length > 120)
            throw new ArgumentException("Project name must be 120 characters or fewer.", nameof(name));
        if (description?.Trim().Length > 500)
            throw new ArgumentException("Project description must be 500 characters or fewer.", nameof(description));

        var project = new Project(name, ownerUsername, DateTimeOffset.UtcNow, description);
        await projects.AddAsync(project, cancellationToken);
        return project;
    }

    public Task<Project?> GetAsync(Guid id, string ownerUsername, CancellationToken cancellationToken) =>
        projects.GetByIdAsync(id, ownerUsername, cancellationToken);

    public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerUsername, CancellationToken cancellationToken) =>
        projects.GetByOwnerWithSubmissionsAsync(ownerUsername, cancellationToken);
}
