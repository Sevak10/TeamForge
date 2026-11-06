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
        dbContext.Submissions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RequirementSubmission>> GetRecentAsync(int count, CancellationToken cancellationToken) =>
        await dbContext.Submissions.OrderByDescending(item => item.CreatedAt).Take(count).ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
