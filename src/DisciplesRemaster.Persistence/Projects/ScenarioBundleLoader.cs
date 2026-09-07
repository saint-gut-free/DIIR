using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Persistence.Projects;

/// <summary>
/// Composes the native scenario and content persistence boundaries. Results use
/// positional labels instead of local paths so callers can report failures safely.
/// </summary>
public sealed class ScenarioBundleLoader : IScenarioBundleLoader
{
    private readonly IScenarioFileStore scenarioStore;
    private readonly IContentPackageFileStore contentStore;
    private readonly IContentPackageValidationService packageValidation;
    private readonly ScenarioContentValidationService scenarioContentValidation;

    public ScenarioBundleLoader(
        IScenarioFileStore scenarioStore,
        IContentPackageFileStore contentStore,
        IContentPackageValidationService packageValidation,
        ScenarioContentValidationService scenarioContentValidation)
    {
        this.scenarioStore = scenarioStore ?? throw new ArgumentNullException(nameof(scenarioStore));
        this.contentStore = contentStore ?? throw new ArgumentNullException(nameof(contentStore));
        this.packageValidation = packageValidation ?? throw new ArgumentNullException(nameof(packageValidation));
        this.scenarioContentValidation = scenarioContentValidation ?? throw new ArgumentNullException(nameof(scenarioContentValidation));
    }

    public ScenarioBundleLoadResult Load(
        string scenarioPath,
        IReadOnlyList<string> contentPackagePaths)
    {
        ArgumentNullException.ThrowIfNull(contentPackagePaths);

        ScenarioLoadResult scenarioLoad = scenarioStore.Load(scenarioPath);
        if (!scenarioLoad.IsSuccess || scenarioLoad.Scenario is null)
        {
            return Failure(new ScenarioBundleLoadIssue(
                ScenarioBundleLoadIssueCode.ScenarioLoadFailed,
                "scenario",
                "$",
                scenarioLoad.ErrorCode.ToString(),
                scenarioLoad.Message ?? "Scenario loading failed."));
        }

        List<ContentPackageDefinition> packages = [];
        List<ScenarioBundleLoadIssue> issues = [];
        for (int index = 0; index < contentPackagePaths.Count; index++)
        {
            ContentPackageLoadResult packageLoad = contentStore.Load(contentPackagePaths[index]);
            if (packageLoad.IsSuccess && packageLoad.Package is not null)
            {
                packages.Add(packageLoad.Package);
                continue;
            }

            string label = $"content[{index}]";
            if (packageLoad.ValidationIssues.Count == 0)
            {
                issues.Add(new ScenarioBundleLoadIssue(
                    ScenarioBundleLoadIssueCode.ContentPackageLoadFailed,
                    label,
                    "$",
                    packageLoad.ErrorCode.ToString(),
                    packageLoad.Message ?? "Content package loading failed."));
                continue;
            }

            issues.AddRange(packageLoad.ValidationIssues.Select(issue => new ScenarioBundleLoadIssue(
                ScenarioBundleLoadIssueCode.ContentPackageLoadFailed,
                label,
                issue.PropertyPath,
                issue.Code.ToString(),
                issue.Message)));
        }

        if (issues.Count > 0)
        {
            return Failure(issues);
        }

        ContentCatalogBuildResult catalogBuild = ContentCatalog.Build(packages, packageValidation);
        if (!catalogBuild.IsSuccess || catalogBuild.Catalog is null)
        {
            return Failure(catalogBuild.Issues.Select(issue => new ScenarioBundleLoadIssue(
                ScenarioBundleLoadIssueCode.ContentCatalogInvalid,
                "content-catalog",
                issue.PropertyPath,
                issue.Code.ToString(),
                issue.Message)));
        }

        ScenarioContentValidationResult referenceValidation =
            scenarioContentValidation.ValidateReferences(scenarioLoad.Scenario, catalogBuild.Catalog);
        if (!referenceValidation.IsValid)
        {
            return Failure(referenceValidation.Issues.Select(issue => new ScenarioBundleLoadIssue(
                ScenarioBundleLoadIssueCode.ScenarioContentInvalid,
                "scenario",
                issue.PropertyPath,
                issue.Code.ToString(),
                issue.Message)));
        }

        string[] packageIds = packages
            .Select(package => package.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return new ScenarioBundleLoadResult(
            new ScenarioBundle(scenarioLoad.Scenario, catalogBuild.Catalog, packageIds),
            []);
    }

    private static ScenarioBundleLoadResult Failure(ScenarioBundleLoadIssue issue) =>
        Failure([issue]);

    private static ScenarioBundleLoadResult Failure(IEnumerable<ScenarioBundleLoadIssue> issues) =>
        new(
            null,
            issues
                .OrderBy(issue => issue.InputLabel, StringComparer.Ordinal)
                .ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
                .ToArray());
}
