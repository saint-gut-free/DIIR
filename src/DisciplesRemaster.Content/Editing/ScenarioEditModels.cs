using DisciplesRemaster.Content.Scenarios;

namespace DisciplesRemaster.Content.Editing;

public static class ScenarioEditRules
{
    public const int DefaultHistoryCapacity = 100;
    public const int MaximumHistoryCapacity = 1_000;
}

public enum ScenarioEditStatus
{
    Applied,
    NoChange,
    Undone,
    Redone,
    InvalidPosition,
    InvalidDimensions,
    DuplicateObjectId,
    ObjectNotFound,
    ValidationFailed,
    UndoUnavailable,
    RedoUnavailable,
}

public sealed record ScenarioEditResult(
    ScenarioEditStatus Status,
    ScenarioDefinition Scenario,
    IReadOnlyList<ScenarioValidationIssue> ValidationIssues,
    string? Message)
{
    public bool IsSuccess => Status is
        ScenarioEditStatus.Applied or
        ScenarioEditStatus.NoChange or
        ScenarioEditStatus.Undone or
        ScenarioEditStatus.Redone;
}

public sealed record ScenarioEditSessionCreationResult(
    ScenarioEditSession? Session,
    IReadOnlyList<ScenarioValidationIssue> ValidationIssues,
    string? Message)
{
    public bool IsSuccess => Session is not null && ValidationIssues.All(issue =>
        issue.Severity != ScenarioValidationSeverity.Error);
}
