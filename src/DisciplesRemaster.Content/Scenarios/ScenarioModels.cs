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
    IReadOnlyList<TerrainPlacement> Terrain)
{
    public IReadOnlyList<ScenarioObjectPlacement> Objects { get; init; } = [];
}

/// <summary>
/// Places project-owned terrain content at one grid position.
/// </summary>
public sealed record TerrainPlacement(GridPosition Position, string Terrain);

/// <summary>
/// Places an inert project-owned object archetype on the map. Gameplay behavior
/// is deliberately outside the native scenario v1 placement contract.
/// </summary>
public sealed record ScenarioObjectPlacement(
    string Id,
    string Archetype,
    GridPosition Position);
