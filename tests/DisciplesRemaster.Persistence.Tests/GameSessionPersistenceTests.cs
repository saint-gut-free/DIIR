using System.Text;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class GameSessionPersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-session-{Guid.NewGuid():N}");
    private readonly GameSessionJsonSerializer serializer = new();
    private readonly GameSessionFileStore store;

    public GameSessionPersistenceTests()
    {
        Directory.CreateDirectory(directory);
        store = new GameSessionFileStore(serializer);
    }

    [Fact]
    public void Serialize_SameState_IsDeterministicAndSortsActors()
    {
        GameSessionState session = CreateSession();

        byte[] first = serializer.Serialize(session).Data!;
        byte[] second = serializer.Serialize(session).Data!;
        string json = Encoding.UTF8.GetString(first);

        Assert.Equal(first, second);
        Assert.True(json.IndexOf("blue-actor", StringComparison.Ordinal) < json.IndexOf("red-actor", StringComparison.Ordinal));
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsTurnPositionsAndMovement()
    {
        GameSessionState initial = CreateSession();
        var service = new GameSessionService(new MovementPlanner(new GridPathfinder()));
        GameSessionState redTurn = service.AdvanceTurn(initial);
        GameSessionState changed = service.MoveActor(
            redTurn,
            "red-actor",
            new GridPosition(2, 3),
            OrthogonalGridTopology.Instance,
            _ => true).Session!;

        GameSessionDeserializationResult result = serializer.Deserialize(serializer.Serialize(changed).Data!);

        Assert.True(result.IsSuccess);
        Assert.Equal("red", result.Session!.Turn.ActiveParticipantId);
        Assert.Equal(1, result.Session.Turn.RoundNumber);
        Assert.Equal(new GridPosition(2, 3), result.Session.Actors["red-actor"].Position);
        Assert.Equal(0, result.Session.Actors["red-actor"].RemainingMovement);
    }

    [Fact]
    public void Serialize_NullSession_ReturnsStructuredFailure()
    {
        GameSessionSerializationResult result = serializer.Serialize(null);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameSessionPersistenceErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal("SessionMissing", Assert.Single(result.Issues).DetailCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not-json")]
    [InlineData("{\"formatVersion\":1,\"unknown\":true}")]
    public void Deserialize_InvalidJson_ReturnsStructuredFailure(string json)
    {
        GameSessionDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(GameSessionPersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_ReturnsTypedIssue()
    {
        string json = ValidJson().Replace("\"formatVersion\": 1", "\"formatVersion\": 2", StringComparison.Ordinal);

        GameSessionDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.Equal(GameSessionPersistenceErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(GameSessionPersistenceIssueCode.UnsupportedFormatVersion, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Deserialize_InvalidDimensions_ReturnsTypedIssue()
    {
        string json = ValidJson().Replace("\"mapWidth\": 5", "\"mapWidth\": 0", StringComparison.Ordinal);

        GameSessionDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.Equal(GameSessionPersistenceIssueCode.MapDimensionsInvalid, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Deserialize_InvalidRuntimeState_MapsCoreIssue()
    {
        string json = ValidJson().Replace("\"remainingMovement\": 3", "\"remainingMovement\": 4", StringComparison.Ordinal);

        GameSessionDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        GameSessionPersistenceIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GameSessionPersistenceIssueCode.SessionValidationFailed, issue.Code);
        Assert.Equal(nameof(GameSessionValidationCode.InvalidRemainingMovement), issue.DetailCode);
    }

    [Fact]
    public void FileStore_SaveAndLoad_RoundTrips()
    {
        string path = Path.Combine(directory, "session.json");

        GameSessionSaveResult save = store.Save(path, CreateSession());
        GameSessionLoadResult load = store.Load(path);

        Assert.True(save.IsSuccess);
        Assert.True(load.IsSuccess);
        Assert.Equal("blue", load.Session!.Turn.ActiveParticipantId);
    }

    [Fact]
    public void FileStore_InvalidSession_DoesNotCreateFile()
    {
        string path = Path.Combine(directory, "invalid.json");

        GameSessionSaveResult result = store.Save(path, null);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void FileStore_MissingInput_ReturnsFileNotFound()
    {
        GameSessionLoadResult result = store.Load(Path.Combine(directory, "missing.json"));

        Assert.Equal(GameSessionPersistenceErrorCode.FileNotFound, result.ErrorCode);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private static GameSessionState CreateSession() =>
        GameSessionState.Create(
            new GridSize(5, 5),
            ["blue", "red"],
            [
                new GameActorDefinition("red-actor", "red", new GridPosition(4, 3), 2),
                new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 3),
            ]).Session!;

    private string ValidJson() =>
        Encoding.UTF8.GetString(serializer.Serialize(CreateSession()).Data!);
}
