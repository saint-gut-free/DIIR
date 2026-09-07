using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Persistence.Scenarios;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Godot;

/// <summary>
/// Immutable data prepared for a future Godot scene adapter. This type contains
/// no Godot dependency and keeps engine APIs outside the domain libraries.
/// </summary>
public sealed record ScenarioSceneData(
    string ScenarioId,
    string Title,
    GridSize Size,
    string DefaultTerrain,
    IReadOnlyList<ScenarioSceneTerrain> TerrainOverrides,
    IReadOnlyList<ScenarioSceneObject> Objects);

public sealed record ScenarioSceneTerrain(GridPosition Position, string ContentReference);

public sealed record ScenarioSceneObject(
    string Id,
    string ArchetypeReference,
    GridPosition Position);

public sealed record ScenarioSceneLoadResult(
    bool IsSuccess,
    ScenarioSceneData? Scene,
    ScenarioPersistenceErrorCode ErrorCode,
    string? Message);

public sealed record ValidatedScenarioSceneLoadResult(
    bool IsSuccess,
    ScenarioSceneData? Scene,
    IReadOnlyList<string> ContentPackageIds,
    IReadOnlyList<ScenarioBundleLoadIssue> Issues);
