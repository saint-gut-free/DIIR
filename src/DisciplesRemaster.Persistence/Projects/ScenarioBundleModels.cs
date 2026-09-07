using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;

namespace DisciplesRemaster.Persistence.Projects;

public enum ScenarioBundleLoadIssueCode
{
    ScenarioLoadFailed,
    ContentPackageLoadFailed,
    ContentCatalogInvalid,
    ScenarioContentInvalid,
}

public sealed record ScenarioBundleLoadIssue(
    ScenarioBundleLoadIssueCode Code,
    string InputLabel,
    string PropertyPath,
    string DetailCode,
    string Message);

/// <summary>
/// A validated project-owned scenario together with the content catalog that
/// resolves all of its references.
/// </summary>
public sealed record ScenarioBundle(
    ScenarioDefinition Scenario,
    ContentCatalog Content,
    IReadOnlyList<string> ContentPackageIds);

public sealed record ScenarioBundleLoadResult(
    ScenarioBundle? Bundle,
    IReadOnlyList<ScenarioBundleLoadIssue> Issues)
{
    public bool IsSuccess => Bundle is not null && Issues.Count == 0;
}

public interface IScenarioBundleLoader
{
    ScenarioBundleLoadResult Load(string scenarioPath, IReadOnlyList<string> contentPackagePaths);
}
