using Microsoft.EntityFrameworkCore;
using TeamForge.Domain;

namespace TeamForge.Infrastructure;

public sealed class TeamForgeDbContext(DbContextOptions<TeamForgeDbContext> options) : DbContext(options)
{
    public DbSet<RequirementSubmission> Submissions => Set<RequirementSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var submission = modelBuilder.Entity<RequirementSubmission>();
        submission.HasKey(item => item.Id);
        submission.Property(item => item.Requirement).HasMaxLength(12000).IsRequired();
        submission.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        submission.Property(item => item.BusinessAnalysisJson).IsRequired();
        submission.Property(item => item.DeveloperAnalysisJson).IsRequired();
        submission.Property(item => item.QaAnalysisJson).IsRequired();
        submission.Property(item => item.ReviewerNotes).HasMaxLength(4000);
        submission.HasIndex(item => item.CreatedAt);
    }
}
