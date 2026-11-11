namespace TeamForge.Domain;

public sealed class Project
{
    private Project() { }

    public Project(string name, string ownerUsername, DateTimeOffset createdAt, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(ownerUsername))
            throw new ArgumentException("Project owner is required.", nameof(ownerUsername));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        OwnerUsername = ownerUsername.Trim();
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string OwnerUsername { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public List<RequirementSubmission> Submissions { get; private set; } = [];
}
