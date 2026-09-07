using System.Text;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class GameSessionActionLogPersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-actions-{Guid.NewGuid():N}");
    private readonly GameSessionActionLogJsonSerializer serializer = new();

    public GameSessionActionLogPersistenceTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void SerializeAndDeserialize_ValidLog_RoundTripsActions()
    {
        GameSessionActionLog source = CreateLog();

        GameSessionActionLogSerializationResult serialization = serializer.Serialize(source);
        GameSessionActionLogDeserializationResult deserialization = serializer.Deserialize(serialization.Data!);

        Assert.True(serialization.IsSuccess);
        Assert.True(deserialization.IsSuccess);
        Assert.Equal(source.FormatVersion, deserialization.Log!.FormatVersion);
        Assert.Equal(source.Actions, deserialization.Log!.Actions);
    }

    [Fact]
    public void Serialize_IsDeterministicAndUsesStableKindNames()
    {
        GameSessionActionLog source = CreateLog();

        GameSessionActionLogSerializationResult first = serializer.Serialize(source);
        GameSessionActionLogSerializationResult second = serializer.Serialize(source);

        Assert.Equal(first.Data, second.Data);
        string json = Encoding.UTF8.GetString(first.Data!);
        Assert.Contains("\"kind\": \"moveActor\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"advanceTurn\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("timestamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_InvalidSequence_ReturnsCoreValidationDetail()
    {
        var log = new GameSessionActionLog(
            GameSessionActionLogFormatV1.Version,
            [GameSessionAction.AdvanceTurn(2)]);

        GameSessionActionLogSerializationResult result = serializer.Serialize(log);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameSessionActionLogErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains(result.Issues, issue => issue.DetailCode == nameof(GameSessionActionIssueCode.InvalidSequence));
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_ReturnsStructuredIssue()
    {
        byte[] json = Encoding.UTF8.GetBytes("""{"formatVersion":2,"actions":[]}""");

        GameSessionActionLogDeserializationResult result = serializer.Deserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionLogIssueCode.UnsupportedFormatVersion);
    }

    [Fact]
    public void Deserialize_UnknownActionKind_ReturnsStructuredIssue()
    {
        byte[] json = Encoding.UTF8.GetBytes(
            """{"formatVersion":1,"actions":[{"sequence":1,"kind":"unknown","actorId":null,"x":null,"y":null}]}""");

        GameSessionActionLogDeserializationResult result = serializer.Deserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionLogIssueCode.UnsupportedActionKind);
    }

    [Fact]
    public void Deserialize_IncompleteDestination_ReturnsValidationIssue()
    {
        byte[] json = Encoding.UTF8.GetBytes(
            """{"formatVersion":1,"actions":[{"sequence":1,"kind":"moveActor","actorId":"actor","x":2,"y":null}]}""");

        GameSessionActionLogDeserializationResult result = serializer.Deserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.DetailCode == nameof(GameSessionActionIssueCode.DestinationMissing));
    }

    [Fact]
    public void Deserialize_MissingActionsArray_IsRejected()
    {
        byte[] json = Encoding.UTF8.GetBytes("""{"formatVersion":1}""");

        GameSessionActionLogDeserializationResult result = serializer.Deserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionLogIssueCode.ActionsMissing);
    }

    [Fact]
    public void Deserialize_UnknownProperty_IsInvalidJson()
    {
        byte[] json = Encoding.UTF8.GetBytes("""{"formatVersion":1,"actions":[],"extra":true}""");

        GameSessionActionLogDeserializationResult result = serializer.Deserialize(json);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameSessionActionLogErrorCode.InvalidJson, result.ErrorCode);
    }

    [Fact]
    public void FileStore_LoadsReadOnlyWithoutChangingMetadata()
    {
        string path = Path.Combine(directory, "actions.json");
        byte[] data = serializer.Serialize(CreateLog()).Data!;
        File.WriteAllBytes(path, data);
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        byte[] before = File.ReadAllBytes(path);
        var store = new GameSessionActionLogFileStore(serializer);

        GameSessionActionLogLoadResult result = store.Load(path);

        Assert.True(result.IsSuccess);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void FileStore_MissingFile_ReturnsSafeFailure()
    {
        var store = new GameSessionActionLogFileStore(serializer);
        string path = Path.Combine(directory, "private", "missing.json");

        GameSessionActionLogLoadResult result = store.Load(path);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameSessionActionLogErrorCode.FileNotFound, result.ErrorCode);
        Assert.DoesNotContain(directory, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private static GameSessionActionLog CreateLog() =>
        new(
            GameSessionActionLogFormatV1.Version,
            [
                GameSessionAction.MoveActor(1, "blue-actor", new GridPosition(2, 1)),
                GameSessionAction.AdvanceTurn(2),
            ]);
}
