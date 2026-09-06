using System.Text.RegularExpressions;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Scenarios;

public sealed partial class ScenarioValidationService : IScenarioValidationService
{
    public ScenarioValidationResult Validate(ScenarioDefinition? scenario)
    {
        if (scenario is null)
        {
            return new ScenarioValidationResult(
            [
                Error(ScenarioValidationCode.ScenarioMissing, "$", "Scenario document is missing."),
            ]);
        }

        List<ScenarioValidationIssue> issues = [];

        if (scenario.FormatVersion != ScenarioFormatV1.Version)
        {
            issues.Add(Error(
                ScenarioValidationCode.UnsupportedFormatVersion,
                "formatVersion",
                $"Supported format version is {ScenarioFormatV1.Version}."));
        }

        ValidateId(scenario.Id, issues);
        ValidateText(scenario, issues);
        ValidateMap(scenario.Map, issues);

        return new ScenarioValidationResult(Sort(issues));
    }

    private static void ValidateId(string? id, ICollection<ScenarioValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            issues.Add(Error(ScenarioValidationCode.ScenarioIdMissing, "id", "Scenario ID is required."));
            return;
        }

        if (id.Length > ScenarioFormatV1.MaximumIdLength || !ScenarioIdPattern().IsMatch(id))
        {
            issues.Add(Error(
                ScenarioValidationCode.ScenarioIdInvalid,
                "id",
                "Scenario ID must use lowercase letters, digits, dots, underscores, or hyphens and start with a letter or digit."));
        }
    }

    private static void ValidateText(ScenarioDefinition scenario, ICollection<ScenarioValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(scenario.Title))
        {
            issues.Add(Error(ScenarioValidationCode.ScenarioTitleMissing, "title", "Scenario title is required."));
        }
        else if (scenario.Title.Length > ScenarioFormatV1.MaximumTitleLength)
        {
            issues.Add(Error(ScenarioValidationCode.ScenarioTitleTooLong, "title", "Scenario title is too long."));
        }

        if (scenario.Description?.Length > ScenarioFormatV1.MaximumDescriptionLength)
        {
            issues.Add(Error(ScenarioValidationCode.ScenarioDescriptionTooLong, "description", "Scenario description is too long."));
        }
    }

    private static void ValidateMap(ScenarioMapDefinition? map, ICollection<ScenarioValidationIssue> issues)
    {
        if (map is null)
        {
            issues.Add(Error(ScenarioValidationCode.MapMissing, "map", "Scenario map is required."));
            return;
        }

        bool dimensionsPositive = map.Width > 0 && map.Height > 0;
        if (!dimensionsPositive)
        {
            issues.Add(Error(ScenarioValidationCode.MapDimensionsInvalid, "map", "Map dimensions must be positive."));
        }
        else
        {
            if (map.Width > ScenarioFormatV1.MaximumDimension || map.Height > ScenarioFormatV1.MaximumDimension)
            {
                issues.Add(Error(ScenarioValidationCode.MapDimensionsTooLarge, "map", "A map dimension exceeds the native format safety limit."));
            }

            if ((long)map.Width * map.Height > ScenarioFormatV1.MaximumCellCount)
            {
                issues.Add(Error(ScenarioValidationCode.MapCellCountTooLarge, "map", "The map contains too many cells for format version 1."));
            }
        }

        ValidateContentReference(map.DefaultTerrain, "map.defaultTerrain", true, issues);

        IReadOnlyList<TerrainPlacement> terrain = map.Terrain ?? [];
        HashSet<GridPosition> occupied = [];
        GridSize? size = dimensionsPositive ? new GridSize(map.Width, map.Height) : null;

        for (int index = 0; index < terrain.Count; index++)
        {
            TerrainPlacement? placement = terrain[index];
            string path = $"map.terrain[{index}]";
            if (placement is null)
            {
                issues.Add(Error(ScenarioValidationCode.ContentReferenceInvalid, path, "Terrain placement is missing."));
                continue;
            }

            ValidateContentReference(placement.Terrain, $"{path}.terrain", false, issues);

            if (size is not null && !size.Value.Contains(placement.Position))
            {
                issues.Add(Error(ScenarioValidationCode.TerrainPlacementOutsideMap, $"{path}.position", "Terrain position is outside the map."));
            }

            if (!occupied.Add(placement.Position))
            {
                issues.Add(Error(ScenarioValidationCode.DuplicateTerrainPlacement, $"{path}.position", "Only one terrain override is allowed at a position."));
            }

            if (string.Equals(placement.Terrain, map.DefaultTerrain, StringComparison.Ordinal))
            {
                issues.Add(Warning(ScenarioValidationCode.RedundantTerrainPlacement, path, "Terrain override equals the map default."));
            }
        }
    }

    private static void ValidateContentReference(
        string? value,
        string path,
        bool isDefaultTerrain,
        ICollection<ScenarioValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Error(
                isDefaultTerrain ? ScenarioValidationCode.DefaultTerrainMissing : ScenarioValidationCode.ContentReferenceInvalid,
                path,
                "A content reference is required."));
            return;
        }

        if (value.Length > ScenarioFormatV1.MaximumContentReferenceLength || !ContentReferencePattern().IsMatch(value))
        {
            issues.Add(Error(
                ScenarioValidationCode.ContentReferenceInvalid,
                path,
                "Content references must use the project-owned namespace:name syntax."));
        }
    }

    private static IReadOnlyList<ScenarioValidationIssue> Sort(IEnumerable<ScenarioValidationIssue> issues) =>
        issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ToArray();

    private static ScenarioValidationIssue Error(ScenarioValidationCode code, string path, string message) =>
        new(code, ScenarioValidationSeverity.Error, path, message);

    private static ScenarioValidationIssue Warning(ScenarioValidationCode code, string path, string message) =>
        new(code, ScenarioValidationSeverity.Warning, path, message);

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ScenarioIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*:[a-z0-9][a-z0-9._/-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ContentReferencePattern();
}
