using System.Collections.ObjectModel;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Scenarios;

/// <summary>
/// Provides indexed, read-only access to a validated native scenario without
/// introducing gameplay or rendering dependencies.
/// </summary>
public sealed class ScenarioRuntimeView
{
    private readonly IReadOnlyDictionary<string, ScenarioObjectPlacement> objectsById;

    private ScenarioRuntimeView(
        string scenarioId,
        string title,
        SparseGrid<string> terrain,
        IReadOnlyDictionary<string, ScenarioObjectPlacement> objectsById)
    {
        ScenarioId = scenarioId;
        Title = title;
        Terrain = terrain;
        this.objectsById = objectsById;
    }

    public string ScenarioId { get; }

    public string Title { get; }

    public SparseGrid<string> Terrain { get; }

    public IReadOnlyDictionary<string, ScenarioObjectPlacement> ObjectsById => objectsById;

    public static ScenarioRuntimeView Create(
        ScenarioDefinition scenario,
        IScenarioValidationService validationService)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(validationService);

        ScenarioValidationResult validation = validationService.Validate(scenario);
        if (!validation.IsValid)
        {
            throw new ArgumentException("A runtime view can only be created from a valid scenario.", nameof(scenario));
        }

        var size = new GridSize(scenario.Map.Width, scenario.Map.Height);
        var terrain = new SparseGrid<string>(
            size,
            scenario.Map.DefaultTerrain,
            scenario.Map.Terrain.Select(placement =>
                new KeyValuePair<GridPosition, string>(placement.Position, placement.Terrain)));
        var objects = new ReadOnlyDictionary<string, ScenarioObjectPlacement>(
            scenario.Map.Objects.ToDictionary(placement => placement.Id, StringComparer.Ordinal));

        return new ScenarioRuntimeView(scenario.Id, scenario.Title, terrain, objects);
    }
}
