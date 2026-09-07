using DisciplesRemaster.Content.Catalog;

namespace DisciplesRemaster.Content.Editing;

/// <summary>
/// Maintains bounded immutable edit history for project-owned content metadata.
/// Binary assets and runtime behavior remain outside this model.
/// </summary>
public sealed class ContentPackageEditSession
{
    private readonly IContentPackageValidationService validationService;
    private readonly int historyCapacity;
    private readonly List<ContentPackageDefinition> undoHistory = [];
    private readonly List<ContentPackageDefinition> redoHistory = [];

    private ContentPackageEditSession(
        ContentPackageDefinition package,
        IContentPackageValidationService validationService,
        int historyCapacity)
    {
        Current = Clone(package);
        this.validationService = validationService;
        this.historyCapacity = historyCapacity;
    }

    public ContentPackageDefinition Current { get; private set; }

    public bool CanUndo => undoHistory.Count > 0;

    public bool CanRedo => redoHistory.Count > 0;

    public int UndoCount => undoHistory.Count;

    public int RedoCount => redoHistory.Count;

    public static ContentPackageEditSessionCreationResult Create(
        ContentPackageDefinition? package,
        IContentPackageValidationService validationService,
        int historyCapacity = ContentPackageEditRules.DefaultHistoryCapacity)
    {
        ArgumentNullException.ThrowIfNull(validationService);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(historyCapacity);
        if (historyCapacity > ContentPackageEditRules.MaximumHistoryCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        }

        ContentPackageValidationResult validation = validationService.Validate(package);
        if (!validation.IsValid || package is null)
        {
            return new ContentPackageEditSessionCreationResult(
                null,
                validation.Issues,
                "A content edit session requires a valid native content package.");
        }

        return new ContentPackageEditSessionCreationResult(
            new ContentPackageEditSession(package, validationService, historyCapacity),
            validation.Issues,
            null);
    }

    public ContentPackageEditResult SetDisplayName(string displayName)
    {
        if (string.Equals(Current.DisplayName, displayName, StringComparison.Ordinal))
        {
            return NoChange();
        }

        return Apply(Current with { DisplayName = displayName });
    }

    public ContentPackageEditResult AddTerrain(TerrainContentDefinition terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if (Current.Terrains.Any(item => string.Equals(item.Id, terrain.Id, StringComparison.Ordinal)))
        {
            return Failure(ContentPackageEditStatus.DuplicateEntryId, "A terrain with this ID already exists.");
        }

        return Apply(Current with { Terrains = [.. Current.Terrains, terrain] });
    }

    public ContentPackageEditResult SetTerrainDisplayName(string terrainId, string displayName)
    {
        TerrainContentDefinition? existing = Current.Terrains.SingleOrDefault(
            item => string.Equals(item.Id, terrainId, StringComparison.Ordinal));
        if (existing is null)
        {
            return Failure(ContentPackageEditStatus.EntryNotFound, "Terrain entry was not found.");
        }

        if (string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
        {
            return NoChange();
        }

        TerrainContentDefinition[] terrains = Current.Terrains
            .Select(item => ReferenceEquals(item, existing) ? item with { DisplayName = displayName } : item)
            .ToArray();
        return Apply(Current with { Terrains = terrains });
    }

    public ContentPackageEditResult RemoveTerrain(string terrainId)
    {
        TerrainContentDefinition[] terrains = Current.Terrains
            .Where(item => !string.Equals(item.Id, terrainId, StringComparison.Ordinal))
            .ToArray();
        if (terrains.Length == Current.Terrains.Count)
        {
            return Failure(ContentPackageEditStatus.EntryNotFound, "Terrain entry was not found.");
        }

        return Apply(Current with { Terrains = terrains });
    }

    public ContentPackageEditResult AddObjectArchetype(ObjectArchetypeDefinition archetype)
    {
        ArgumentNullException.ThrowIfNull(archetype);
        if (Current.ObjectArchetypes.Any(item => string.Equals(item.Id, archetype.Id, StringComparison.Ordinal)))
        {
            return Failure(ContentPackageEditStatus.DuplicateEntryId, "An object archetype with this ID already exists.");
        }

        return Apply(Current with { ObjectArchetypes = [.. Current.ObjectArchetypes, archetype] });
    }

    public ContentPackageEditResult SetObjectArchetypeDisplayName(string archetypeId, string displayName)
    {
        ObjectArchetypeDefinition? existing = Current.ObjectArchetypes.SingleOrDefault(
            item => string.Equals(item.Id, archetypeId, StringComparison.Ordinal));
        if (existing is null)
        {
            return Failure(ContentPackageEditStatus.EntryNotFound, "Object archetype entry was not found.");
        }

        if (string.Equals(existing.DisplayName, displayName, StringComparison.Ordinal))
        {
            return NoChange();
        }

        ObjectArchetypeDefinition[] archetypes = Current.ObjectArchetypes
            .Select(item => ReferenceEquals(item, existing) ? item with { DisplayName = displayName } : item)
            .ToArray();
        return Apply(Current with { ObjectArchetypes = archetypes });
    }

    public ContentPackageEditResult RemoveObjectArchetype(string archetypeId)
    {
        ObjectArchetypeDefinition[] archetypes = Current.ObjectArchetypes
            .Where(item => !string.Equals(item.Id, archetypeId, StringComparison.Ordinal))
            .ToArray();
        if (archetypes.Length == Current.ObjectArchetypes.Count)
        {
            return Failure(ContentPackageEditStatus.EntryNotFound, "Object archetype entry was not found.");
        }

        return Apply(Current with { ObjectArchetypes = archetypes });
    }

    public ContentPackageEditResult Undo()
    {
        if (!CanUndo)
        {
            return Failure(ContentPackageEditStatus.UndoUnavailable, "No content edit is available to undo.");
        }

        PushBounded(redoHistory, Current);
        Current = Pop(undoHistory);
        return Success(ContentPackageEditStatus.Undone);
    }

    public ContentPackageEditResult Redo()
    {
        if (!CanRedo)
        {
            return Failure(ContentPackageEditStatus.RedoUnavailable, "No content edit is available to redo.");
        }

        PushBounded(undoHistory, Current);
        Current = Pop(redoHistory);
        return Success(ContentPackageEditStatus.Redone);
    }

    private ContentPackageEditResult Apply(ContentPackageDefinition candidate)
    {
        ContentPackageDefinition snapshot = Clone(candidate);
        ContentPackageValidationResult validation = validationService.Validate(snapshot);
        if (!validation.IsValid)
        {
            return new ContentPackageEditResult(
                ContentPackageEditStatus.ValidationFailed,
                Current,
                validation.Issues,
                "The edit would produce an invalid content package.");
        }

        PushBounded(undoHistory, Current);
        redoHistory.Clear();
        Current = snapshot;
        return new ContentPackageEditResult(
            ContentPackageEditStatus.Applied,
            Current,
            validation.Issues,
            null);
    }

    private void PushBounded(IList<ContentPackageDefinition> history, ContentPackageDefinition package)
    {
        if (history.Count == historyCapacity)
        {
            history.RemoveAt(0);
        }

        history.Add(package);
    }

    private static ContentPackageDefinition Pop(IList<ContentPackageDefinition> history)
    {
        int index = history.Count - 1;
        ContentPackageDefinition package = history[index];
        history.RemoveAt(index);
        return package;
    }

    private ContentPackageEditResult NoChange() => Success(ContentPackageEditStatus.NoChange);

    private ContentPackageEditResult Success(ContentPackageEditStatus status) =>
        new(status, Current, [], null);

    private ContentPackageEditResult Failure(ContentPackageEditStatus status, string message) =>
        new(status, Current, [], message);

    private static ContentPackageDefinition Clone(ContentPackageDefinition package) =>
        package with
        {
            Terrains = package.Terrains.ToList().AsReadOnly(),
            ObjectArchetypes = package.ObjectArchetypes.ToList().AsReadOnly(),
        };
}
