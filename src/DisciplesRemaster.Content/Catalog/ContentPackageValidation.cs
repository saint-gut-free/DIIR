namespace DisciplesRemaster.Content.Catalog;

public enum ContentPackageValidationCode
{
    PackageMissing,
    UnsupportedFormatVersion,
    PackageIdMissing,
    PackageIdInvalid,
    PackageDisplayNameMissing,
    PackageDisplayNameTooLong,
    TooManyTerrains,
    TooManyObjectArchetypes,
    EntryMissing,
    EntryIdMissing,
    EntryIdInvalid,
    EntryDisplayNameMissing,
    EntryDisplayNameTooLong,
    DuplicateTerrainId,
    DuplicateObjectArchetypeId,
}

public sealed record ContentPackageValidationIssue(
    ContentPackageValidationCode Code,
    string PropertyPath,
    string Message);

public sealed record ContentPackageValidationResult(IReadOnlyList<ContentPackageValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public interface IContentPackageValidationService
{
    ContentPackageValidationResult Validate(ContentPackageDefinition? package);
}

public sealed class ContentPackageValidationService : IContentPackageValidationService
{
    public ContentPackageValidationResult Validate(ContentPackageDefinition? package)
    {
        if (package is null)
        {
            return new ContentPackageValidationResult(
            [
                Issue(ContentPackageValidationCode.PackageMissing, "$", "Content package is missing."),
            ]);
        }

        List<ContentPackageValidationIssue> issues = [];
        if (package.FormatVersion != ContentPackageFormatV1.Version)
        {
            issues.Add(Issue(ContentPackageValidationCode.UnsupportedFormatVersion, "formatVersion", "Content package version is unsupported."));
        }

        if (string.IsNullOrWhiteSpace(package.Id))
        {
            issues.Add(Issue(ContentPackageValidationCode.PackageIdMissing, "id", "Package ID is required."));
        }
        else if (!ContentReference.IsValidPackageId(package.Id))
        {
            issues.Add(Issue(ContentPackageValidationCode.PackageIdInvalid, "id", "Package ID is invalid."));
        }

        ValidateDisplayName(
            package.DisplayName,
            "displayName",
            ContentPackageValidationCode.PackageDisplayNameMissing,
            ContentPackageValidationCode.PackageDisplayNameTooLong,
            issues);
        ValidateEntries(
            package.Terrains,
            "terrains",
            ContentPackageValidationCode.TooManyTerrains,
            ContentPackageValidationCode.DuplicateTerrainId,
            definition => definition?.Id,
            definition => definition?.DisplayName,
            issues);
        ValidateEntries(
            package.ObjectArchetypes,
            "objectArchetypes",
            ContentPackageValidationCode.TooManyObjectArchetypes,
            ContentPackageValidationCode.DuplicateObjectArchetypeId,
            definition => definition?.Id,
            definition => definition?.DisplayName,
            issues);

        return new ContentPackageValidationResult(
            issues
                .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ToArray());
    }

    private static void ValidateEntries<T>(
        IReadOnlyList<T>? entries,
        string path,
        ContentPackageValidationCode tooManyCode,
        ContentPackageValidationCode duplicateCode,
        Func<T, string?> getId,
        Func<T, string?> getDisplayName,
        ICollection<ContentPackageValidationIssue> issues)
        where T : class
    {
        IReadOnlyList<T> values = entries ?? [];
        if (values.Count > ContentPackageFormatV1.MaximumEntriesPerKind)
        {
            issues.Add(Issue(tooManyCode, path, "Content package contains too many entries of this kind."));
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Count; index++)
        {
            T? entry = values[index];
            string entryPath = $"{path}[{index}]";
            if (entry is null)
            {
                issues.Add(Issue(ContentPackageValidationCode.EntryMissing, entryPath, "Content entry is missing."));
                continue;
            }

            string? id = getId(entry);
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(Issue(ContentPackageValidationCode.EntryIdMissing, $"{entryPath}.id", "Content entry ID is required."));
            }
            else
            {
                if (!ContentReference.IsValidLocalId(id))
                {
                    issues.Add(Issue(ContentPackageValidationCode.EntryIdInvalid, $"{entryPath}.id", "Content entry ID is invalid."));
                }

                if (!ids.Add(id))
                {
                    issues.Add(Issue(duplicateCode, $"{entryPath}.id", "Content entry IDs must be unique within their kind."));
                }
            }

            ValidateDisplayName(
                getDisplayName(entry),
                $"{entryPath}.displayName",
                ContentPackageValidationCode.EntryDisplayNameMissing,
                ContentPackageValidationCode.EntryDisplayNameTooLong,
                issues);
        }
    }

    private static void ValidateDisplayName(
        string? displayName,
        string path,
        ContentPackageValidationCode missingCode,
        ContentPackageValidationCode tooLongCode,
        ICollection<ContentPackageValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            issues.Add(Issue(missingCode, path, "Display name is required."));
        }
        else if (displayName.Length > ContentPackageFormatV1.MaximumDisplayNameLength)
        {
            issues.Add(Issue(tooLongCode, path, "Display name is too long."));
        }
    }

    private static ContentPackageValidationIssue Issue(
        ContentPackageValidationCode code,
        string path,
        string message) =>
        new(code, path, message);
}
