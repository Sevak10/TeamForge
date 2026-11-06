namespace TeamForge.Domain;

public sealed class RequirementSubmission
{
    private RequirementSubmission() { }

    public RequirementSubmission(string requirement, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Requirement = requirement;
        CreatedAt = createdAt;
        Status = SubmissionStatus.AwaitingReview;
    }

    public Guid Id { get; private set; }
    public string Requirement { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string BusinessAnalysisJson { get; private set; } = string.Empty;
    public string DeveloperAnalysisJson { get; private set; } = string.Empty;
    public string QaAnalysisJson { get; private set; } = string.Empty;
    public SubmissionStatus Status { get; private set; }
    public string? ReviewerNotes { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public void SetAnalysis(string businessAnalysisJson, string developerAnalysisJson, string qaAnalysisJson)
    {
        BusinessAnalysisJson = businessAnalysisJson;
        DeveloperAnalysisJson = developerAnalysisJson;
        QaAnalysisJson = qaAnalysisJson;
    }

    public void SaveReviewerNotes(string? notes)
    {
        ReviewerNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ReviewedAt = DateTimeOffset.UtcNow;
    }

    public void Approve()
    {
        Status = SubmissionStatus.Approved;
        ReviewedAt = DateTimeOffset.UtcNow;
    }
}

public enum SubmissionStatus
{
    AwaitingReview = 0,
    Approved = 1
}
