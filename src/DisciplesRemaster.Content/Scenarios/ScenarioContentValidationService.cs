using DisciplesRemaster.Content.Catalog;

namespace DisciplesRemaster.Content.Scenarios;

public enum ScenarioContentValidationCode
{
    UnknownDefaultTerrain,
    UnknownTerrain,
    UnknownObjectArchetype,
}

public sealed record ScenarioContentValidationIssue(
    ScenarioContentValidationCode Code,
    string PropertyPath,
    string ContentReference,
    string Message);

public sealed record ScenarioContentValidationResult(IReadOnlyList<ScenarioContentValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class ScenarioContentValidationService
{
    public ScenarioContentValidationResult ValidateReferences(
        ScenarioDefinition scenario,
        ContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(catalog);

        List<ScenarioContentValidationIssue> issues = [];
        if (!catalog.ContainsTerrain(scenario.Map.DefaultTerrain))
        {
            issues.Add(Issue(
                ScenarioContentValidationCode.UnknownDefaultTerrain,
                "map.defaultTerrain",
                scenario.Map.DefaultTerrain,
                "Default terrain is not declared by the content catalog."));
        }

        for (int index = 0; index < scenario.Map.Terrain.Count; index++)
        {
            string reference = scenario.Map.Terrain[index].Terrain;
            if (!catalog.ContainsTerrain(reference))
            {
                issues.Add(Issue(
                    ScenarioContentValidationCode.UnknownTerrain,
                    $"map.terrain[{index}].terrain",
                    reference,
                    "Terrain is not declared by the content catalog."));
            }
        }

        for (int index = 0; index < scenario.Map.Objects.Count; index++)
        {
            string reference = scenario.Map.Objects[index].Archetype;
            if (!catalog.ContainsObjectArchetype(reference))
            {
                issues.Add(Issue(
                    ScenarioContentValidationCode.UnknownObjectArchetype,
                    $"map.objects[{index}].archetype",
                    reference,
                    "Object archetype is not declared by the content catalog."));
            }
        }

        return new ScenarioContentValidationResult(
            issues
                .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ToArray());
    }

    private static ScenarioContentValidationIssue Issue(
        ScenarioContentValidationCode code,
        string path,
        string reference,
        string message) =>
        new(code, path, reference, message);
}
