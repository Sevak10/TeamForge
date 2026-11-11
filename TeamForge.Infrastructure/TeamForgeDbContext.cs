using Microsoft.EntityFrameworkCore;
using TeamForge.Domain;

namespace TeamForge.Infrastructure;

public sealed class TeamForgeDbContext(DbContextOptions<TeamForgeDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<RequirementSubmission> Submissions => Set<RequirementSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var project = modelBuilder.Entity<Project>();
        project.HasKey(item => item.Id);
        project.Property(item => item.Name).HasMaxLength(120).IsRequired();
        project.Property(item => item.Description).HasMaxLength(500);
        project.Property(item => item.OwnerUsername).HasMaxLength(100).IsRequired();
        project.HasIndex(item => new { item.OwnerUsername, item.CreatedAt });

        var submission = modelBuilder.Entity<RequirementSubmission>();
        submission.HasKey(item => item.Id);
        submission.Property(item => item.OwnerUsername).HasMaxLength(100).IsRequired().HasDefaultValue(string.Empty);
        submission.Property(item => item.Requirement).HasMaxLength(12000).IsRequired();
        submission.Property(item => item.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        submission.Property(item => item.BusinessAnalysisJson).IsRequired();
        submission.Property(item => item.DeveloperAnalysisJson).IsRequired();
        submission.Property(item => item.QaAnalysisJson).IsRequired();
        submission.Property(item => item.ReviewerNotes).HasMaxLength(4000);
        submission.HasOne(item => item.Project)
            .WithMany(item => item.Submissions)
            .HasForeignKey(item => item.ProjectId)
            .OnDelete(DeleteBehavior.SetNull);
        submission.HasIndex(item => item.CreatedAt);
        submission.HasIndex(item => new { item.OwnerUsername, item.ProjectId, item.CreatedAt });
    }
}
