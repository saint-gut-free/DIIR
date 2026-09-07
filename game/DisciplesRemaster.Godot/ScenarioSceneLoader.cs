using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Godot;

/// <summary>
/// Loads the project-owned scenario format and projects it into deterministic
/// scene data. Creating Godot nodes remains the responsibility of the client adapter.
/// </summary>
public sealed class ScenarioSceneLoader
{
    private readonly IScenarioFileStore store;
    private readonly IScenarioValidationService validationService;

    public ScenarioSceneLoader(
        IScenarioFileStore store,
        IScenarioValidationService validationService)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public ScenarioSceneLoadResult Load(string path)
    {
        ScenarioLoadResult load = store.Load(path);
        if (!load.IsSuccess || load.Scenario is null)
        {
            return new ScenarioSceneLoadResult(false, null, load.ErrorCode, load.Message);
        }

        ScenarioRuntimeView runtime = ScenarioRuntimeView.Create(load.Scenario, validationService);
        return new ScenarioSceneLoadResult(
            true,
            ScenarioSceneProjection.Project(runtime),
            ScenarioPersistenceErrorCode.None,
            null);
    }
}

internal static class ScenarioSceneProjection
{
    public static ScenarioSceneData Project(ScenarioRuntimeView runtime) =>
        new(
            runtime.ScenarioId,
            runtime.Title,
            runtime.Terrain.Size,
            runtime.Terrain.DefaultValue,
            runtime.Terrain.Overrides
                .OrderBy(entry => entry.Key.Y)
                .ThenBy(entry => entry.Key.X)
                .ThenBy(entry => entry.Value, StringComparer.Ordinal)
                .Select(entry => new ScenarioSceneTerrain(entry.Key, entry.Value))
                .ToList()
                .AsReadOnly(),
            runtime.ObjectsById.Values
                .OrderBy(placement => placement.Id, StringComparer.Ordinal)
                .Select(placement => new ScenarioSceneObject(
                    placement.Id,
                    placement.Archetype,
                    placement.Position))
                .ToList()
                .AsReadOnly());
}
