using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Persistence.Sessions;

public static class GameSessionCheckpointFormatV1
{
    public const int Version = 1;
    public const long MaximumDocumentBytes = 16L * 1024 * 1024;
}

public enum GameSessionPersistenceErrorCode
{
    None,
    InvalidPath,
    FileNotFound,
    FileInaccessible,
    DocumentTooLarge,
    InvalidJson,
    ValidationFailed,
    WriteFailed,
    UnexpectedError,
}

public enum GameSessionPersistenceIssueCode
{
    UnsupportedFormatVersion,
    MapDimensionsInvalid,
    SessionValidationFailed,
}

public sealed record GameSessionPersistenceIssue(
    GameSessionPersistenceIssueCode Code,
    string PropertyPath,
    string DetailCode,
    string Message);

public sealed record GameSessionSerializationResult(
    bool IsSuccess,
    byte[]? Data,
    GameSessionPersistenceErrorCode ErrorCode,
    IReadOnlyList<GameSessionPersistenceIssue> Issues,
    string? Message);

public sealed record GameSessionDeserializationResult(
    bool IsSuccess,
    GameSessionState? Session,
    GameSessionPersistenceErrorCode ErrorCode,
    IReadOnlyList<GameSessionPersistenceIssue> Issues,
    string? Message);

public sealed record GameSessionLoadResult(
    bool IsSuccess,
    GameSessionState? Session,
    GameSessionPersistenceErrorCode ErrorCode,
    IReadOnlyList<GameSessionPersistenceIssue> Issues,
    string? Message);

public sealed record GameSessionSaveResult(
    bool IsSuccess,
    GameSessionPersistenceErrorCode ErrorCode,
    IReadOnlyList<GameSessionPersistenceIssue> Issues,
    string? Message);

public interface IGameSessionSerializer
{
    GameSessionSerializationResult Serialize(GameSessionState? session);

    GameSessionDeserializationResult Deserialize(ReadOnlySpan<byte> data);
}

public interface IGameSessionFileStore
{
    GameSessionLoadResult Load(string path);

    GameSessionSaveResult Save(string path, GameSessionState? session);
}
