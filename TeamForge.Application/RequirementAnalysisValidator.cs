using TeamForge.Agents;

namespace TeamForge.Application;

/// <summary>
/// Checks that each agent returned the information required by the next workflow step.
/// </summary>
internal static class RequirementAnalysisValidator
{
    private static readonly string[] RequiredQaCategories = ["Positive", "Negative", "Boundary", "Edge"];

    public static void ValidateOptions(RequirementAgentOptions options)
    {
        if (options is null || options.BusinessAnalyst is null || options.Developer is null ||
            options.QaEngineer is null || options.Model is null ||
            !Enum.IsDefined(options.BusinessAnalyst.Level) ||
            !Enum.IsDefined(options.Developer.Level) ||
            !Enum.IsDefined(options.QaEngineer.Level))
        {
            throw new ArgumentException("Choose Junior, Mid, or Senior for each agent.", nameof(options));
        }

        var hasSupportedProvider = options.Model.Provider is "Ollama" or "Gemini" or "OpenAI";
        var hasValidModelName = !string.IsNullOrWhiteSpace(options.Model.ModelName) && options.Model.ModelName.Length <= 120;
        if (!hasSupportedProvider || !hasValidModelName)
        {
            throw new ArgumentException(
                "Choose Ollama, Gemini, or OpenAI and enter a model name (up to 120 characters).",
                nameof(options));
        }

        var guidanceValues = new[]
        {
            options.BusinessAnalyst.AdditionalGuidance,
            options.Developer.AdditionalGuidance,
            options.QaEngineer.AdditionalGuidance
        };

        if (guidanceValues.Any(guidance => guidance?.Length > 2000))
        {
            throw new ArgumentException(
                "Additional guidance must be 2,000 characters or fewer for each agent.",
                nameof(options));
        }
    }

    public static void ValidateBusinessResult(BusinessAnalysisResult result)
    {
        var hasNoStories = result.UserStories is null || result.UserStories.Count == 0;
        var hasIncompleteStory = result.UserStories?.Any(story =>
            story is null ||
            string.IsNullOrWhiteSpace(story.Id) ||
            string.IsNullOrWhiteSpace(story.Title) ||
            string.IsNullOrWhiteSpace(story.AsA) ||
            string.IsNullOrWhiteSpace(story.IWant) ||
            string.IsNullOrWhiteSpace(story.SoThat) ||
            story.AcceptanceCriteria is not { Count: > 0 } ||
            story.AcceptanceCriteria.Any(string.IsNullOrWhiteSpace)) == true;

        if (result.ClarificationQuestions is null || hasNoStories || hasIncompleteStory)
        {
            throw new InvalidOperationException(
                "Business Analyst output must include user stories with acceptance criteria.");
        }
    }

    public static void ValidateDeveloperResult(DeveloperAnalysisResult result, BusinessAnalysisResult business)
    {
        var hasRequiredPlanForEveryStory = business.UserStories.All(story =>
            result.UnitTestPlans?.Any(plan =>
                string.Equals(plan.UserStoryId, story.Id, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(plan.Target) &&
                plan.Tests is { Count: > 0 }) == true);

        if (result.ClarificationQuestions is null ||
            result.Dependencies is null ||
            string.IsNullOrWhiteSpace(result.ImplementationApproach) ||
            !hasRequiredPlanForEveryStory)
        {
            throw new InvalidOperationException("Developer output must include a unit test plan for every user story.");
        }
    }

    public static void ValidateQaResult(QaAnalysisResult result, BusinessAnalysisResult business)
    {
        if (result.ClarificationQuestions is null || result.TestCases is null)
        {
            throw new InvalidOperationException(
                "QA output must include clarification questions and acceptance test cases.");
        }

        foreach (var story in business.UserStories)
        {
            var storyTestCases = result.TestCases
                .Where(testCase => string.Equals(
                    testCase.UserStoryId,
                    story.Id,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

            EnsureTestCasesAreComplete(story.Id, storyTestCases);
            EnsureAllQaCategoriesAreCovered(story.Id, storyTestCases);
        }
    }

    private static void EnsureTestCasesAreComplete(string storyId, IReadOnlyCollection<QaTestCase> testCases)
    {
        var hasIncompleteCase = testCases.Any(testCase =>
            string.IsNullOrWhiteSpace(testCase.Scenario) ||
            string.IsNullOrWhiteSpace(testCase.ExpectedResult) ||
            testCase.Steps is not { Count: > 0 });

        if (hasIncompleteCase)
        {
            throw new InvalidOperationException(
                $"QA output contains an incomplete acceptance test case for {storyId}.");
        }
    }

    private static void EnsureAllQaCategoriesAreCovered(string storyId, IReadOnlyCollection<QaTestCase> testCases)
    {
        var hasMissingCategory = RequiredQaCategories.Any(requiredCategory =>
            !testCases.Any(testCase => string.Equals(
                testCase.Category,
                requiredCategory,
                StringComparison.OrdinalIgnoreCase)));

        if (hasMissingCategory)
        {
            throw new InvalidOperationException(
                $"QA output must cover positive, negative, boundary, and edge cases for {storyId}.");
        }
    }
}
