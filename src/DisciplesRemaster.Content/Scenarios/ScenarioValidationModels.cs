namespace DisciplesRemaster.Content.Scenarios;

public enum ScenarioValidationSeverity
{
    Warning,
    Error,
}

public enum ScenarioValidationCode
{
    ScenarioMissing,
    UnsupportedFormatVersion,
    ScenarioIdMissing,
    ScenarioIdInvalid,
    ScenarioTitleMissing,
    ScenarioTitleTooLong,
    ScenarioDescriptionTooLong,
    MapMissing,
    MapDimensionsInvalid,
    MapDimensionsTooLarge,
    MapCellCountTooLarge,
    DefaultTerrainMissing,
    ContentReferenceInvalid,
    TerrainPlacementOutsideMap,
    DuplicateTerrainPlacement,
    RedundantTerrainPlacement,
    TooManyObjects,
    ObjectIdMissing,
    ObjectIdInvalid,
    DuplicateObjectId,
    ObjectPlacementOutsideMap,
}

public sealed record ScenarioValidationIssue(
    ScenarioValidationCode Code,
    ScenarioValidationSeverity Severity,
    string PropertyPath,
    string Message);

public sealed record ScenarioValidationResult(IReadOnlyList<ScenarioValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != ScenarioValidationSeverity.Error);
}

public interface IScenarioValidationService
{
    ScenarioValidationResult Validate(ScenarioDefinition? scenario);
}
