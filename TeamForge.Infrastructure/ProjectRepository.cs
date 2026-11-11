using Microsoft.EntityFrameworkCore;
using TeamForge.Application;
using TeamForge.Domain;

namespace TeamForge.Infrastructure;

public sealed class ProjectRepository(TeamForgeDbContext dbContext) : IProjectRepository
{
    public async Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        await dbContext.Projects.AddAsync(project, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Project?> GetByIdAsync(Guid id, string ownerUsername, CancellationToken cancellationToken) =>
        dbContext.Projects
            .Include(project => project.Submissions.OrderByDescending(submission => submission.CreatedAt))
            .SingleOrDefaultAsync(project => project.Id == id && project.OwnerUsername == ownerUsername, cancellationToken);

    public async Task<IReadOnlyList<Project>> GetByOwnerWithSubmissionsAsync(string ownerUsername, CancellationToken cancellationToken) =>
        await dbContext.Projects
            .Where(project => project.OwnerUsername == ownerUsername)
            .OrderByDescending(project => project.CreatedAt)
            .Include(project => project.Submissions.OrderByDescending(submission => submission.CreatedAt))
            .ToListAsync(cancellationToken);
}
