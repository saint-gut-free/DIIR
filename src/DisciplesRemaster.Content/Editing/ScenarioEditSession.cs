using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Editing;

/// <summary>
/// Maintains a bounded in-memory history for edits to the project-owned native
/// scenario model. Persistence and UI remain separate concerns.
/// </summary>
public sealed class ScenarioEditSession
{
    private readonly IScenarioValidationService validationService;
    private readonly int historyCapacity;
    private readonly List<ScenarioDefinition> undoHistory = [];
    private readonly List<ScenarioDefinition> redoHistory = [];

    private ScenarioEditSession(
        ScenarioDefinition scenario,
        IScenarioValidationService validationService,
        int historyCapacity)
    {
        Current = Clone(scenario);
        this.validationService = validationService;
        this.historyCapacity = historyCapacity;
    }

    public ScenarioDefinition Current { get; private set; }

    public bool CanUndo => undoHistory.Count > 0;

    public bool CanRedo => redoHistory.Count > 0;

    public int UndoCount => undoHistory.Count;

    public int RedoCount => redoHistory.Count;

    public static ScenarioEditSessionCreationResult Create(
        ScenarioDefinition? scenario,
        IScenarioValidationService validationService,
        int historyCapacity = ScenarioEditRules.DefaultHistoryCapacity)
    {
        ArgumentNullException.ThrowIfNull(validationService);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyCapacity);
        if (historyCapacity > ScenarioEditRules.MaximumHistoryCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        }

        ScenarioValidationResult validation = validationService.Validate(scenario);
        if (!validation.IsValid || scenario is null)
        {
            return new ScenarioEditSessionCreationResult(
                null,
                validation.Issues,
                "A scenario edit session requires a valid native scenario.");
        }

        return new ScenarioEditSessionCreationResult(
            new ScenarioEditSession(scenario, validationService, historyCapacity),
            validation.Issues,
            null);
    }

    public ScenarioEditResult PaintTerrain(GridPosition position, string terrainReference)
    {
        if (!Contains(position))
        {
            return Failure(ScenarioEditStatus.InvalidPosition, "Terrain position is outside the map.");
        }

        TerrainPlacement? existing = Current.Map.Terrain.SingleOrDefault(item => item.Position == position);
        bool removeOverride = string.Equals(terrainReference, Current.Map.DefaultTerrain, StringComparison.Ordinal);
        if ((removeOverride && existing is null) ||
            (!removeOverride && string.Equals(existing?.Terrain, terrainReference, StringComparison.Ordinal)))
        {
            return NoChange();
        }

        List<TerrainPlacement> terrain = Current.Map.Terrain
            .Where(item => item.Position != position)
            .ToList();
        if (!removeOverride)
        {
            terrain.Add(new TerrainPlacement(position, terrainReference));
        }

        return Apply(Current with { Map = Current.Map with { Terrain = terrain } });
    }

    public ScenarioEditResult PlaceObject(ScenarioObjectPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!Contains(placement.Position))
        {
            return Failure(ScenarioEditStatus.InvalidPosition, "Object position is outside the map.");
        }

        if (Current.Map.Objects.Any(item => string.Equals(item.Id, placement.Id, StringComparison.Ordinal)))
        {
            return Failure(ScenarioEditStatus.DuplicateObjectId, "An object with this ID already exists.");
        }

        List<ScenarioObjectPlacement> objects = Current.Map.Objects.ToList();
        objects.Add(placement);
        return Apply(Current with { Map = Current.Map with { Objects = objects } });
    }

    public ScenarioEditResult MoveObject(string objectId, GridPosition position)
    {
        if (!Contains(position))
        {
            return Failure(ScenarioEditStatus.InvalidPosition, "Object position is outside the map.");
        }

        ScenarioObjectPlacement? existing = Current.Map.Objects.SingleOrDefault(
            item => string.Equals(item.Id, objectId, StringComparison.Ordinal));
        if (existing is null)
        {
            return Failure(ScenarioEditStatus.ObjectNotFound, "Scenario object was not found.");
        }

        if (existing.Position == position)
        {
            return NoChange();
        }

        ScenarioObjectPlacement[] objects = Current.Map.Objects
            .Select(item => ReferenceEquals(item, existing) ? item with { Position = position } : item)
            .ToArray();
        return Apply(Current with { Map = Current.Map with { Objects = objects } });
    }

    public ScenarioEditResult RemoveObject(string objectId)
    {
        ScenarioObjectPlacement[] objects = Current.Map.Objects
            .Where(item => !string.Equals(item.Id, objectId, StringComparison.Ordinal))
            .ToArray();
        if (objects.Length == Current.Map.Objects.Count)
        {
            return Failure(ScenarioEditStatus.ObjectNotFound, "Scenario object was not found.");
        }

        return Apply(Current with { Map = Current.Map with { Objects = objects } });
    }

    public ScenarioEditResult Undo()
    {
        if (!CanUndo)
        {
            return Failure(ScenarioEditStatus.UndoUnavailable, "No edit is available to undo.");
        }

        PushBounded(redoHistory, Current);
        Current = Pop(undoHistory);
        return Success(ScenarioEditStatus.Undone);
    }

    public ScenarioEditResult Redo()
    {
        if (!CanRedo)
        {
            return Failure(ScenarioEditStatus.RedoUnavailable, "No edit is available to redo.");
        }

        PushBounded(undoHistory, Current);
        Current = Pop(redoHistory);
        return Success(ScenarioEditStatus.Redone);
    }

    private ScenarioEditResult Apply(ScenarioDefinition candidate)
    {
        ScenarioDefinition snapshot = Clone(candidate);
        ScenarioValidationResult validation = validationService.Validate(snapshot);
        if (!validation.IsValid)
        {
            return new ScenarioEditResult(
                ScenarioEditStatus.ValidationFailed,
                Current,
                validation.Issues,
                "The edit would produce an invalid scenario.");
        }

        PushBounded(undoHistory, Current);
        redoHistory.Clear();
        Current = snapshot;
        return new ScenarioEditResult(ScenarioEditStatus.Applied, Current, validation.Issues, null);
    }

    private void PushBounded(IList<ScenarioDefinition> history, ScenarioDefinition scenario)
    {
        if (history.Count == historyCapacity)
        {
            history.RemoveAt(0);
        }

        history.Add(scenario);
    }

    private static ScenarioDefinition Pop(IList<ScenarioDefinition> history)
    {
        int index = history.Count - 1;
        ScenarioDefinition scenario = history[index];
        history.RemoveAt(index);
        return scenario;
    }

    private bool Contains(GridPosition position) =>
        new GridSize(Current.Map.Width, Current.Map.Height).Contains(position);

    private ScenarioEditResult NoChange() =>
        Success(ScenarioEditStatus.NoChange);

    private ScenarioEditResult Success(ScenarioEditStatus status) =>
        new(status, Current, [], null);

    private ScenarioEditResult Failure(ScenarioEditStatus status, string message) =>
        new(status, Current, [], message);

    private static ScenarioDefinition Clone(ScenarioDefinition scenario) =>
        scenario with
        {
            Map = scenario.Map with
            {
                Terrain = scenario.Map.Terrain.ToList().AsReadOnly(),
                Objects = scenario.Map.Objects.ToList().AsReadOnly(),
            },
        };
}
