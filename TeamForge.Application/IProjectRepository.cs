using TeamForge.Domain;

namespace TeamForge.Application;

public interface IProjectRepository
{
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetByIdAsync(Guid id, string ownerUsername, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> GetByOwnerWithSubmissionsAsync(string ownerUsername, CancellationToken cancellationToken);
}
