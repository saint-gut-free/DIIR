using DisciplesRemaster.Content.Catalog;

namespace DisciplesRemaster.Content.Editing;

public static class ContentPackageEditRules
{
    public const int DefaultHistoryCapacity = 100;
    public const int MaximumHistoryCapacity = 1_000;
}

public enum ContentPackageEditStatus
{
    Applied,
    NoChange,
    Undone,
    Redone,
    DuplicateEntryId,
    EntryNotFound,
    ValidationFailed,
    UndoUnavailable,
    RedoUnavailable,
}

public sealed record ContentPackageEditResult(
    ContentPackageEditStatus Status,
    ContentPackageDefinition Package,
    IReadOnlyList<ContentPackageValidationIssue> ValidationIssues,
    string? Message)
{
    public bool IsSuccess => Status is ContentPackageEditStatus.Applied or
        ContentPackageEditStatus.NoChange or
        ContentPackageEditStatus.Undone or
        ContentPackageEditStatus.Redone;
}

public sealed record ContentPackageEditSessionCreationResult(
    ContentPackageEditSession? Session,
    IReadOnlyList<ContentPackageValidationIssue> ValidationIssues,
    string? Message)
{
    public bool IsSuccess => Session is not null && ValidationIssues.Count == 0;
}
