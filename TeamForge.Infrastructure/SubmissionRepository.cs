using Microsoft.EntityFrameworkCore;
using TeamForge.Application;
using TeamForge.Domain;

namespace TeamForge.Infrastructure;

public sealed class SubmissionRepository(TeamForgeDbContext dbContext) : ISubmissionRepository
{
    public async Task AddAsync(RequirementSubmission submission, CancellationToken cancellationToken)
    {
        await dbContext.Submissions.AddAsync(submission, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<RequirementSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Submissions.Include(item => item.Project).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<RequirementSubmission?> GetByIdForOwnerAsync(Guid id, string ownerUsername, CancellationToken cancellationToken) =>
        dbContext.Submissions
            .Include(item => item.Project)
            .SingleOrDefaultAsync(item => item.Id == id && item.OwnerUsername == ownerUsername, cancellationToken);

    public async Task<IReadOnlyList<RequirementSubmission>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
        await dbContext.Submissions.OrderByDescending(item => item.CreatedAt).Take(count).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RequirementSubmission>> GetRecentByOwnerAsync(string ownerUsername, Guid? projectId, int count, CancellationToken cancellationToken)
    {
        var query = dbContext.Submissions.Where(item => item.OwnerUsername == ownerUsername);
        if (projectId.HasValue)
            query = query.Where(item => item.ProjectId == projectId.Value);

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
