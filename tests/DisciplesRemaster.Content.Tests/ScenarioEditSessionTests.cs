using DisciplesRemaster.Content.Editing;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Content.Tests;

public sealed class ScenarioEditSessionTests
{
    private readonly ScenarioValidationService validation = new();

    [Fact]
    public void Create_ValidScenario_CopiesCallerOwnedCollections()
    {
        var terrain = new List<TerrainPlacement>();
        ScenarioDefinition scenario = CreateScenario() with { Map = CreateScenario().Map with { Terrain = terrain } };

        ScenarioEditSession session = ScenarioEditSession.Create(scenario, validation).Session!;
        terrain.Add(new TerrainPlacement(new GridPosition(1, 1), "synthetic:forest"));

        Assert.Empty(session.Current.Map.Terrain);
        Assert.False(session.Current.Map.Terrain is TerrainPlacement[]);
    }

    [Fact]
    public void Create_InvalidScenario_ReturnsValidationIssues()
    {
        ScenarioEditSessionCreationResult result = ScenarioEditSession.Create(
            CreateScenario() with { Id = string.Empty },
            validation);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == ScenarioValidationCode.ScenarioIdMissing);
    }

    [Fact]
    public void PaintTerrain_UndoAndRedo_RestoreSnapshots()
    {
        ScenarioEditSession session = CreateSession();

        ScenarioEditResult paint = session.PaintTerrain(new GridPosition(2, 3), "synthetic:forest");
        ScenarioEditResult undo = session.Undo();
        ScenarioEditResult redo = session.Redo();

        Assert.Equal(ScenarioEditStatus.Applied, paint.Status);
        Assert.Empty(undo.Scenario.Map.Terrain);
        TerrainPlacement placement = Assert.Single(redo.Scenario.Map.Terrain);
        Assert.Equal("synthetic:forest", placement.Terrain);
    }

    [Fact]
    public void MetadataAndMapEdits_AreValidatedAndUndoable()
    {
        ScenarioEditSession session = CreateSession();

        Assert.True(session.SetTitle("Updated title").IsSuccess);
        Assert.True(session.SetDefaultTerrain("synthetic:water").IsSuccess);
        Assert.True(session.ResizeMap(10, 9).IsSuccess);

        Assert.Equal("Updated title", session.Current.Title);
        Assert.Equal("synthetic:water", session.Current.Map.DefaultTerrain);
        Assert.Equal(10, session.Current.Map.Width);
        Assert.Equal(9, session.Current.Map.Height);
        Assert.Equal("synthetic:water", session.Undo().Scenario.Map.DefaultTerrain);
        Assert.Equal("synthetic:plain", session.Undo().Scenario.Map.DefaultTerrain);
        Assert.Equal("Synthetic", session.Undo().Scenario.Title);
    }

    [Fact]
    public void SetDefaultTerrain_RemovesNowRedundantOverrides()
    {
        ScenarioEditSession session = CreateSession();
        session.PaintTerrain(new GridPosition(1, 1), "synthetic:water");
        session.PaintTerrain(new GridPosition(2, 2), "synthetic:forest");

        ScenarioEditResult result = session.SetDefaultTerrain("synthetic:water");

        TerrainPlacement remaining = Assert.Single(result.Scenario.Map.Terrain);
        Assert.Equal("synthetic:forest", remaining.Terrain);
    }

    [Fact]
    public void SetDefaultTerrain_SameValue_CanonicalizesExistingRedundantOverride()
    {
        ScenarioDefinition scenario = CreateScenario() with
        {
            Map = CreateScenario().Map with
            {
                Terrain = [new TerrainPlacement(new GridPosition(1, 1), "synthetic:plain")],
            },
        };
        ScenarioEditSession session = ScenarioEditSession.Create(scenario, validation).Session!;

        ScenarioEditResult result = session.SetDefaultTerrain("synthetic:plain");

        Assert.Equal(ScenarioEditStatus.Applied, result.Status);
        Assert.Empty(result.Scenario.Map.Terrain);
    }

    [Fact]
    public void ResizeMap_ThatWouldExcludePlacements_IsRejectedWithoutHistory()
    {
        ScenarioEditSession session = CreateSession();
        session.PlaceObject(new ScenarioObjectPlacement("marker", "synthetic:marker", new GridPosition(7, 5)));
        int undoCount = session.UndoCount;

        ScenarioEditResult result = session.ResizeMap(4, 4);

        Assert.Equal(ScenarioEditStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == ScenarioValidationCode.ObjectPlacementOutsideMap);
        Assert.Equal(8, session.Current.Map.Width);
        Assert.Equal(undoCount, session.UndoCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    public void ResizeMap_InvalidDimensions_ReturnsStructuredFailure(int width, int height)
    {
        ScenarioEditSession session = CreateSession();

        ScenarioEditResult result = session.ResizeMap(width, height);

        Assert.Equal(ScenarioEditStatus.InvalidDimensions, result.Status);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void MetadataNoOps_DoNotCreateHistory()
    {
        ScenarioEditSession session = CreateSession();

        Assert.Equal(ScenarioEditStatus.NoChange, session.SetTitle("Synthetic").Status);
        Assert.Equal(ScenarioEditStatus.NoChange, session.SetDefaultTerrain("synthetic:plain").Status);
        Assert.Equal(ScenarioEditStatus.NoChange, session.ResizeMap(8, 6).Status);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void PaintTerrain_DefaultValue_RemovesOverrideAndCanUndo()
    {
        ScenarioEditSession session = CreateSession();
        Assert.True(session.PaintTerrain(new GridPosition(1, 1), "synthetic:forest").IsSuccess);

        ScenarioEditResult removal = session.PaintTerrain(new GridPosition(1, 1), "synthetic:plain");

        Assert.Empty(removal.Scenario.Map.Terrain);
        Assert.Single(session.Undo().Scenario.Map.Terrain);
    }

    [Fact]
    public void NoChange_DoesNotCreateHistory()
    {
        ScenarioEditSession session = CreateSession();

        ScenarioEditResult result = session.PaintTerrain(new GridPosition(1, 1), "synthetic:plain");

        Assert.Equal(ScenarioEditStatus.NoChange, result.Status);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void NewEditAfterUndo_ClearsRedoHistory()
    {
        ScenarioEditSession session = CreateSession();
        session.PaintTerrain(new GridPosition(1, 1), "synthetic:forest");
        session.Undo();

        session.PaintTerrain(new GridPosition(2, 2), "synthetic:water");

        Assert.False(session.CanRedo);
        Assert.Equal(ScenarioEditStatus.RedoUnavailable, session.Redo().Status);
    }

    [Fact]
    public void HistoryCapacity_DropsOldestSnapshot()
    {
        ScenarioEditSession session = ScenarioEditSession.Create(CreateScenario(), validation, historyCapacity: 2).Session!;
        session.PaintTerrain(new GridPosition(0, 0), "synthetic:forest");
        session.PaintTerrain(new GridPosition(1, 0), "synthetic:forest");
        session.PaintTerrain(new GridPosition(2, 0), "synthetic:forest");

        Assert.True(session.Undo().IsSuccess);
        Assert.True(session.Undo().IsSuccess);
        Assert.Equal(ScenarioEditStatus.UndoUnavailable, session.Undo().Status);
        Assert.Single(session.Current.Map.Terrain);
    }

    [Fact]
    public void ObjectEdits_AreUndoableAndPreserveStableIdentity()
    {
        ScenarioEditSession session = CreateSession();
        var placement = new ScenarioObjectPlacement("marker", "synthetic:marker", new GridPosition(1, 1));

        Assert.True(session.PlaceObject(placement).IsSuccess);
        Assert.True(session.MoveObject("marker", new GridPosition(3, 2)).IsSuccess);
        Assert.True(session.RemoveObject("marker").IsSuccess);
        Assert.Empty(session.Current.Map.Objects);

        Assert.Equal(new GridPosition(3, 2), Assert.Single(session.Undo().Scenario.Map.Objects).Position);
        Assert.Equal(new GridPosition(1, 1), Assert.Single(session.Undo().Scenario.Map.Objects).Position);
        Assert.Empty(session.Undo().Scenario.Map.Objects);
    }

    [Fact]
    public void InvalidEdits_DoNotChangeCurrentOrHistory()
    {
        ScenarioEditSession session = CreateSession();
        ScenarioEditResult invalidPosition = session.PaintTerrain(new GridPosition(8, 0), "synthetic:forest");
        ScenarioEditResult invalidReference = session.PaintTerrain(new GridPosition(1, 1), "not-a-reference");

        Assert.Equal(ScenarioEditStatus.InvalidPosition, invalidPosition.Status);
        Assert.Equal(ScenarioEditStatus.ValidationFailed, invalidReference.Status);
        Assert.Contains(invalidReference.ValidationIssues, issue => issue.Code == ScenarioValidationCode.ContentReferenceInvalid);
        Assert.Empty(session.Current.Map.Terrain);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void ObjectFailures_AreStructuredAndDoNotCreateHistory()
    {
        ScenarioEditSession session = CreateSession();
        var placement = new ScenarioObjectPlacement("marker", "synthetic:marker", new GridPosition(1, 1));
        Assert.True(session.PlaceObject(placement).IsSuccess);

        Assert.Equal(ScenarioEditStatus.DuplicateObjectId, session.PlaceObject(placement).Status);
        Assert.Equal(ScenarioEditStatus.ObjectNotFound, session.MoveObject("missing", new GridPosition(1, 1)).Status);
        Assert.Equal(ScenarioEditStatus.ObjectNotFound, session.RemoveObject("missing").Status);
        Assert.Equal(1, session.UndoCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ScenarioEditRules.MaximumHistoryCapacity + 1)]
    public void Create_InvalidHistoryCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ScenarioEditSession.Create(CreateScenario(), validation, capacity));
    }

    private ScenarioEditSession CreateSession() =>
        ScenarioEditSession.Create(CreateScenario(), validation).Session!;

    private static ScenarioDefinition CreateScenario() =>
        new(
            ScenarioFormatV1.Version,
            "synthetic",
            "Synthetic",
            null,
            new ScenarioMapDefinition(8, 6, "synthetic:plain", []));
}
