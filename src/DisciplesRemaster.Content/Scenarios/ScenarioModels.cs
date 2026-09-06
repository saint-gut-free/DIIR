using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Scenarios;

/// <summary>
/// A project-owned, engine-neutral scenario definition.
/// </summary>
public sealed record ScenarioDefinition(
    int FormatVersion,
    string Id,
    string Title,
    string? Description,
    ScenarioMapDefinition Map);

/// <summary>
/// A rectangular map with a default terrain and sparse terrain overrides.
/// </summary>
public sealed record ScenarioMapDefinition(
    int Width,
    int Height,
    string DefaultTerrain,
    IReadOnlyList<TerrainPlacement> Terrain);

/// <summary>
/// Places project-owned terrain content at one grid position.
/// </summary>
public sealed record TerrainPlacement(GridPosition Position, string Terrain);
