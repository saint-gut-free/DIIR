using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Editing;

namespace DisciplesRemaster.Content.Tests;

public sealed class ContentPackageEditSessionTests
{
    private readonly ContentPackageValidationService validation = new();

    [Fact]
    public void Create_ValidPackage_DefensivelyCopiesCollections()
    {
        List<TerrainContentDefinition> terrains = [new("plain", "Plain")];
        ContentPackageDefinition package = CreatePackage() with { Terrains = terrains };

        ContentPackageEditSessionCreationResult result = ContentPackageEditSession.Create(package, validation);
        terrains.Add(new TerrainContentDefinition("late", "Late mutation"));

        Assert.True(result.IsSuccess);
        Assert.Single(result.Session!.Current.Terrains);
    }

    [Fact]
    public void Create_InvalidPackage_ReturnsValidationWithoutSession()
    {
        ContentPackageDefinition invalid = CreatePackage() with { DisplayName = string.Empty };

        ContentPackageEditSessionCreationResult result = ContentPackageEditSession.Create(invalid, validation);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Contains(result.ValidationIssues, issue =>
            issue.Code == ContentPackageValidationCode.PackageDisplayNameMissing);
    }

    [Fact]
    public void SetDisplayName_ApplyNoChangeAndValidationAreAtomic()
    {
        ContentPackageEditSession session = CreateSession();

        ContentPackageEditResult applied = session.SetDisplayName("Updated package");
        ContentPackageEditResult noChange = session.SetDisplayName("Updated package");
        ContentPackageEditResult invalid = session.SetDisplayName(string.Empty);

        Assert.Equal(ContentPackageEditStatus.Applied, applied.Status);
        Assert.Equal(ContentPackageEditStatus.NoChange, noChange.Status);
        Assert.Equal(ContentPackageEditStatus.ValidationFailed, invalid.Status);
        Assert.Equal("Updated package", session.Current.DisplayName);
        Assert.Equal(1, session.UndoCount);
    }

    [Fact]
    public void TerrainOperations_AddRenameRemoveAndUndo()
    {
        ContentPackageEditSession session = CreateSession();

        Assert.True(session.AddTerrain(new TerrainContentDefinition("water", "Water")).IsSuccess);
        Assert.True(session.SetTerrainDisplayName("water", "Deep water").IsSuccess);
        Assert.True(session.RemoveTerrain("water").IsSuccess);
        Assert.DoesNotContain(session.Current.Terrains, item => item.Id == "water");

        Assert.True(session.Undo().IsSuccess);
        Assert.Equal("Deep water", session.Current.Terrains.Single(item => item.Id == "water").DisplayName);
        Assert.True(session.Undo().IsSuccess);
        Assert.Equal("Water", session.Current.Terrains.Single(item => item.Id == "water").DisplayName);
        Assert.True(session.Redo().IsSuccess);
        Assert.Equal("Deep water", session.Current.Terrains.Single(item => item.Id == "water").DisplayName);
    }

    [Fact]
    public void ObjectArchetypeOperations_AddRenameAndRemove()
    {
        ContentPackageEditSession session = CreateSession();

        Assert.True(session.AddObjectArchetype(new ObjectArchetypeDefinition("marker", "Marker")).IsSuccess);
        Assert.True(session.SetObjectArchetypeDisplayName("marker", "Map marker").IsSuccess);
        Assert.Equal(
            "Map marker",
            session.Current.ObjectArchetypes.Single(item => item.Id == "marker").DisplayName);
        Assert.True(session.RemoveObjectArchetype("marker").IsSuccess);
        Assert.DoesNotContain(session.Current.ObjectArchetypes, item => item.Id == "marker");
    }

    [Fact]
    public void Add_DuplicateIdWithinKind_IsRejectedWithoutHistory()
    {
        ContentPackageEditSession session = CreateSession();

        ContentPackageEditResult terrain = session.AddTerrain(new TerrainContentDefinition("plain", "Other"));
        ContentPackageEditResult archetype = session.AddObjectArchetype(
            new ObjectArchetypeDefinition("landmark", "Other"));

        Assert.Equal(ContentPackageEditStatus.DuplicateEntryId, terrain.Status);
        Assert.Equal(ContentPackageEditStatus.DuplicateEntryId, archetype.Status);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void SameLocalIdAcrossDifferentKinds_IsAllowed()
    {
        ContentPackageEditSession session = CreateSession();

        ContentPackageEditResult result = session.AddObjectArchetype(
            new ObjectArchetypeDefinition("plain", "Plain marker"));

        Assert.True(result.IsSuccess);
        Assert.Contains(session.Current.Terrains, item => item.Id == "plain");
        Assert.Contains(session.Current.ObjectArchetypes, item => item.Id == "plain");
    }

    [Theory]
    [InlineData("terrain")]
    [InlineData("object")]
    public void MissingEntryOperations_AreRejected(string kind)
    {
        ContentPackageEditSession session = CreateSession();

        ContentPackageEditResult rename = kind == "terrain"
            ? session.SetTerrainDisplayName("missing", "Missing")
            : session.SetObjectArchetypeDisplayName("missing", "Missing");
        ContentPackageEditResult remove = kind == "terrain"
            ? session.RemoveTerrain("missing")
            : session.RemoveObjectArchetype("missing");

        Assert.Equal(ContentPackageEditStatus.EntryNotFound, rename.Status);
        Assert.Equal(ContentPackageEditStatus.EntryNotFound, remove.Status);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void Rename_ToSameName_IsNoChange()
    {
        ContentPackageEditSession session = CreateSession();

        ContentPackageEditResult terrain = session.SetTerrainDisplayName("plain", "Plain");
        ContentPackageEditResult archetype = session.SetObjectArchetypeDisplayName("landmark", "Landmark");

        Assert.Equal(ContentPackageEditStatus.NoChange, terrain.Status);
        Assert.Equal(ContentPackageEditStatus.NoChange, archetype.Status);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void InvalidEntryName_DoesNotChangeCurrentOrHistory()
    {
        ContentPackageEditSession session = CreateSession();

        ContentPackageEditResult result = session.SetTerrainDisplayName("plain", string.Empty);

        Assert.Equal(ContentPackageEditStatus.ValidationFailed, result.Status);
        Assert.Equal("Plain", session.Current.Terrains.Single().DisplayName);
        Assert.Equal(0, session.UndoCount);
    }

    [Fact]
    public void EditAfterUndo_ClearsRedoBranch()
    {
        ContentPackageEditSession session = CreateSession();
        Assert.True(session.SetDisplayName("First").IsSuccess);
        Assert.True(session.SetDisplayName("Second").IsSuccess);
        Assert.True(session.Undo().IsSuccess);

        Assert.True(session.AddTerrain(new TerrainContentDefinition("water", "Water")).IsSuccess);

        Assert.False(session.CanRedo);
        Assert.Equal(ContentPackageEditStatus.RedoUnavailable, session.Redo().Status);
    }

    [Fact]
    public void HistoryCapacity_DropsOldestSnapshot()
    {
        ContentPackageEditSession session = ContentPackageEditSession.Create(
            CreatePackage(),
            validation,
            historyCapacity: 2).Session!;
        Assert.True(session.SetDisplayName("One").IsSuccess);
        Assert.True(session.SetDisplayName("Two").IsSuccess);
        Assert.True(session.SetDisplayName("Three").IsSuccess);

        Assert.True(session.Undo().IsSuccess);
        Assert.True(session.Undo().IsSuccess);

        Assert.Equal("One", session.Current.DisplayName);
        Assert.Equal(ContentPackageEditStatus.UndoUnavailable, session.Undo().Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(ContentPackageEditRules.MaximumHistoryCapacity + 1)]
    public void Create_InvalidHistoryCapacity_Throws(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ContentPackageEditSession.Create(CreatePackage(), validation, capacity));
    }

    private ContentPackageEditSession CreateSession() =>
        ContentPackageEditSession.Create(CreatePackage(), validation).Session!;

    private static ContentPackageDefinition CreatePackage() =>
        new(
            ContentPackageFormatV1.Version,
            "synthetic",
            "Synthetic package",
            [new TerrainContentDefinition("plain", "Plain")],
            [new ObjectArchetypeDefinition("landmark", "Landmark")]);
}
