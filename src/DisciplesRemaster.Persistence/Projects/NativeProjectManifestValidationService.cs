namespace DisciplesRemaster.Persistence.Projects;

public sealed class NativeProjectManifestValidationService : INativeProjectManifestValidationService
{
    public NativeProjectManifestValidationResult Validate(NativeProjectManifest? manifest)
    {
        if (manifest is null)
        {
            return Result(Issue(
                NativeProjectManifestValidationCode.UnsupportedFormatVersion,
                "$",
                "Project manifest is missing."));
        }

        List<NativeProjectManifestValidationIssue> issues = [];
        if (manifest.FormatVersion != NativeProjectManifestFormatV1.Version)
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.UnsupportedFormatVersion,
                "formatVersion",
                "Only native project manifest format version 1 is supported."));
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.ProjectIdMissing,
                "id",
                "Project ID is required."));
        }
        else if (manifest.Id.Length > NativeProjectManifestFormatV1.MaximumProjectIdLength)
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.ProjectIdTooLong,
                "id",
                "Project ID exceeds the safety limit."));
        }

        ValidatePath(manifest.Scenario, "scenario", NativeProjectManifestValidationCode.ScenarioPathMissing, issues);

        if (manifest.ContentPackages is null || manifest.ContentPackages.Count == 0)
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.ContentPackagesMissing,
                "contentPackages",
                "At least one content package is required."));
        }
        else
        {
            if (manifest.ContentPackages.Count > NativeProjectManifestFormatV1.MaximumContentPackages)
            {
                issues.Add(Issue(
                    NativeProjectManifestValidationCode.TooManyContentPackages,
                    "contentPackages",
                    "The project references too many content packages."));
            }

            HashSet<string> paths = new(StringComparer.Ordinal);
            for (int index = 0; index < manifest.ContentPackages.Count; index++)
            {
                string path = manifest.ContentPackages[index];
                string propertyPath = $"contentPackages[{index}]";
                ValidatePath(
                    path,
                    propertyPath,
                    NativeProjectManifestValidationCode.ContentPackagePathMissing,
                    issues);
                if (!string.IsNullOrWhiteSpace(path) && !paths.Add(NormalizeSeparators(path)))
                {
                    issues.Add(Issue(
                        NativeProjectManifestValidationCode.DuplicateContentPackagePath,
                        propertyPath,
                        "Content package paths must be unique."));
                }
            }
        }

        if (manifest.Session is not null)
        {
            ValidatePath(
                manifest.Session,
                "session",
                NativeProjectManifestValidationCode.PathMustBeRelative,
                issues);
        }

        return Result(issues);
    }

    private static void ValidatePath(
        string? path,
        string propertyPath,
        NativeProjectManifestValidationCode missingCode,
        ICollection<NativeProjectManifestValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            issues.Add(Issue(missingCode, propertyPath, "A relative document path is required."));
            return;
        }

        if (path.Length > NativeProjectManifestFormatV1.MaximumRelativePathLength)
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.PathTooLong,
                propertyPath,
                "The relative document path exceeds the safety limit."));
        }

        string normalized = NormalizeSeparators(path);
        if (Path.IsPathFullyQualified(path) ||
            normalized.StartsWith("/", StringComparison.Ordinal) ||
            (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':'))
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.PathMustBeRelative,
                propertyPath,
                "Project document paths must be relative."));
        }

        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            issues.Add(Issue(
                NativeProjectManifestValidationCode.PathEscapesProjectDirectory,
                propertyPath,
                "Project document paths cannot contain current- or parent-directory segments."));
        }
    }

    internal static string NormalizeSeparators(string path) =>
        path.Trim().Replace('\\', '/');

    private static NativeProjectManifestValidationResult Result(
        NativeProjectManifestValidationIssue issue) =>
        Result([issue]);

    private static NativeProjectManifestValidationResult Result(
        IEnumerable<NativeProjectManifestValidationIssue> issues) =>
        new(issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ToArray());

    private static NativeProjectManifestValidationIssue Issue(
        NativeProjectManifestValidationCode code,
        string propertyPath,
        string message) =>
        new(code, propertyPath, message);
}
