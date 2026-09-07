namespace DisciplesRemaster.Persistence.Sessions;

public sealed class GameSessionActionLogFileStore : IGameSessionActionLogFileStore
{
    private readonly IGameSessionActionLogSerializer serializer;

    public GameSessionActionLogFileStore(IGameSessionActionLogSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public GameSessionActionLogLoadResult Load(string path)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return Failure(GameSessionActionLogErrorCode.InvalidPath, "Action log path is invalid.");
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return Failure(GameSessionActionLogErrorCode.FileNotFound, "Action log was not found.");
            }

            if (info.Length > GameSessionActionLogFormatV1.MaximumDocumentBytes)
            {
                return Failure(GameSessionActionLogErrorCode.DocumentTooLarge, "Action log exceeds the safety limit.");
            }

            using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                32 * 1024,
                FileOptions.SequentialScan);
            using var memory = new MemoryStream(info.Length > 0 ? checked((int)info.Length) : 0);
            stream.CopyTo(memory);
            GameSessionActionLogDeserializationResult result = serializer.Deserialize(
                memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
            return new GameSessionActionLogLoadResult(
                result.IsSuccess,
                result.Log,
                result.ErrorCode,
                result.Issues,
                result.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(GameSessionActionLogErrorCode.FileInaccessible, "Action log is inaccessible.");
        }
        catch (IOException)
        {
            return Failure(GameSessionActionLogErrorCode.FileInaccessible, "Action log could not be read.");
        }
        catch (Exception)
        {
            return Failure(GameSessionActionLogErrorCode.UnexpectedError, "Action log loading failed unexpectedly.");
        }
    }

    private static bool TryNormalizePath(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path.Trim());
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static GameSessionActionLogLoadResult Failure(
        GameSessionActionLogErrorCode code,
        string message) =>
        new(false, null, code, [], message);
}
