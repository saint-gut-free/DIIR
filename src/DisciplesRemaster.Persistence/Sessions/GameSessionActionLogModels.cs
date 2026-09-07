using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Persistence.Sessions;

public static class GameSessionActionLogFormatV1
{
    public const int Version = 1;
    public const long MaximumDocumentBytes = 16L * 1024 * 1024;
}

public sealed record GameSessionActionLog(
    int FormatVersion,
    IReadOnlyList<GameSessionAction> Actions);

public enum GameSessionActionLogIssueCode
{
    DocumentMissing,
    UnsupportedFormatVersion,
    ActionsMissing,
    ActionMissing,
    UnsupportedActionKind,
    ActionValidationFailed,
}

public sealed record GameSessionActionLogIssue(
    GameSessionActionLogIssueCode Code,
    string PropertyPath,
    string DetailCode,
    string Message);

public enum GameSessionActionLogErrorCode
{
    None,
    InvalidPath,
    FileNotFound,
    FileInaccessible,
    DocumentTooLarge,
    InvalidJson,
    ValidationFailed,
    UnexpectedError,
}

public sealed record GameSessionActionLogSerializationResult(
    bool IsSuccess,
    byte[]? Data,
    GameSessionActionLogErrorCode ErrorCode,
    IReadOnlyList<GameSessionActionLogIssue> Issues,
    string? Message);

public sealed record GameSessionActionLogDeserializationResult(
    bool IsSuccess,
    GameSessionActionLog? Log,
    GameSessionActionLogErrorCode ErrorCode,
    IReadOnlyList<GameSessionActionLogIssue> Issues,
    string? Message);

public sealed record GameSessionActionLogLoadResult(
    bool IsSuccess,
    GameSessionActionLog? Log,
    GameSessionActionLogErrorCode ErrorCode,
    IReadOnlyList<GameSessionActionLogIssue> Issues,
    string? Message);

public interface IGameSessionActionLogSerializer
{
    GameSessionActionLogSerializationResult Serialize(GameSessionActionLog? log);

    GameSessionActionLogDeserializationResult Deserialize(ReadOnlySpan<byte> data);
}

public interface IGameSessionActionLogFileStore
{
    GameSessionActionLogLoadResult Load(string path);
}
