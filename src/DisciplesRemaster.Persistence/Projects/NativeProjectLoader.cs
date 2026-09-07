using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Persistence.Projects;

/// <summary>
/// Resolves a portable native project manifest and validates its complete
/// scenario/content/session boundary before exposing it to an outer adapter.
/// </summary>
public sealed class NativeProjectLoader : INativeProjectLoader
{
    private readonly INativeProjectManifestFileStore manifestStore;
    private readonly IScenarioBundleLoader bundleLoader;
    private readonly IGameSessionFileStore sessionStore;

    public NativeProjectLoader(
        INativeProjectManifestFileStore manifestStore,
        IScenarioBundleLoader bundleLoader,
        IGameSessionFileStore sessionStore)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.bundleLoader = bundleLoader ?? throw new ArgumentNullException(nameof(bundleLoader));
        this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    }

    public NativeProjectLoadResult Load(string manifestPath)
    {
        NativeProjectManifestLoadResult manifestLoad = manifestStore.Load(manifestPath);
        if (!manifestLoad.IsSuccess || manifestLoad.Manifest is null)
        {
            return Failure(new NativeProjectLoadIssue(
                NativeProjectLoadIssueCode.ManifestLoadFailed,
                "manifest",
                "$",
                manifestLoad.ErrorCode.ToString(),
                manifestLoad.Message ?? "Project manifest loading failed."));
        }

        if (!TryGetManifestDirectory(manifestPath, out string projectDirectory))
        {
            return InvalidReferencedPath("manifest", "$", "Project manifest location could not be resolved.");
        }

        NativeProjectManifest manifest = manifestLoad.Manifest;
        if (!TryResolve(projectDirectory, manifest.Scenario, out string scenarioPath))
        {
            return InvalidReferencedPath("scenario", "scenario", "Scenario path could not be resolved safely.");
        }

        List<string> contentPaths = [];
        for (int index = 0; index < manifest.ContentPackages.Count; index++)
        {
            if (!TryResolve(projectDirectory, manifest.ContentPackages[index], out string contentPath))
            {
                return InvalidReferencedPath(
                    $"content[{index}]",
                    $"contentPackages[{index}]",
                    "Content package path could not be resolved safely.");
            }

            contentPaths.Add(contentPath);
        }

        ScenarioBundleLoadResult bundleLoad = bundleLoader.Load(scenarioPath, contentPaths);
        if (!bundleLoad.IsSuccess || bundleLoad.Bundle is null)
        {
            return Failure(bundleLoad.Issues.Select(issue => new NativeProjectLoadIssue(
                NativeProjectLoadIssueCode.ScenarioBundleInvalid,
                issue.InputLabel,
                issue.PropertyPath,
                issue.DetailCode,
                issue.Message)));
        }

        DisciplesRemaster.Core.Sessions.GameSessionState? session = null;
        if (manifest.Session is not null)
        {
            if (!TryResolve(projectDirectory, manifest.Session, out string sessionPath))
            {
                return InvalidReferencedPath("session", "session", "Session checkpoint path could not be resolved safely.");
            }

            GameSessionLoadResult sessionLoad = sessionStore.Load(sessionPath);
            if (!sessionLoad.IsSuccess || sessionLoad.Session is null)
            {
                return Failure(new NativeProjectLoadIssue(
                    NativeProjectLoadIssueCode.SessionLoadFailed,
                    "session",
                    "$",
                    sessionLoad.ErrorCode.ToString(),
                    sessionLoad.Message ?? "Session checkpoint loading failed."));
            }

            session = sessionLoad.Session;
            if (session.MapSize.Width != bundleLoad.Bundle.Scenario.Map.Width ||
                session.MapSize.Height != bundleLoad.Bundle.Scenario.Map.Height)
            {
                return Failure(new NativeProjectLoadIssue(
                    NativeProjectLoadIssueCode.SessionMapSizeMismatch,
                    "session",
                    "mapSize",
                    nameof(NativeProjectLoadIssueCode.SessionMapSizeMismatch),
                    "Session checkpoint dimensions do not match the project scenario."));
            }
        }

        return new NativeProjectLoadResult(
            new NativeProject(manifest, bundleLoad.Bundle, session),
            []);
    }

    private static bool TryGetManifestDirectory(string path, out string directory)
    {
        directory = string.Empty;
        try
        {
            directory = Path.GetDirectoryName(Path.GetFullPath(path.Trim())) ?? string.Empty;
            return directory.Length > 0;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryResolve(string projectDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        try
        {
            string root = Path.GetFullPath(projectDirectory);
            string candidate = Path.GetFullPath(
                NativeProjectManifestValidationService.NormalizeSeparators(relativePath),
                root);
            string rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!candidate.StartsWith(rootPrefix, comparison))
            {
                return false;
            }

            fullPath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static NativeProjectLoadResult InvalidReferencedPath(
        string label,
        string propertyPath,
        string message) =>
        Failure(new NativeProjectLoadIssue(
            NativeProjectLoadIssueCode.ReferencedPathInvalid,
            label,
            propertyPath,
            nameof(NativeProjectLoadIssueCode.ReferencedPathInvalid),
            message));

    private static NativeProjectLoadResult Failure(NativeProjectLoadIssue issue) =>
        Failure([issue]);

    private static NativeProjectLoadResult Failure(IEnumerable<NativeProjectLoadIssue> issues) =>
        new(
            null,
            issues
                .OrderBy(issue => issue.InputLabel, StringComparer.Ordinal)
                .ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
                .ToArray());
}
