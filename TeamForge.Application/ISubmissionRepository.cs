using TeamForge.Domain;

namespace TeamForge.Application;

public interface ISubmissionRepository
{
    Task AddAsync(RequirementSubmission submission, CancellationToken cancellationToken);
    Task<RequirementSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<RequirementSubmission>> GetRecentAsync(int count, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
