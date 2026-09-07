namespace DisciplesRemaster.Persistence.Sessions;

public sealed class GameSessionFileStore : IGameSessionFileStore
{
    private readonly IGameSessionSerializer serializer;

    public GameSessionFileStore(IGameSessionSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public GameSessionLoadResult Load(string path)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return LoadFailure(GameSessionPersistenceErrorCode.InvalidPath, "Game session path is invalid.");
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return LoadFailure(GameSessionPersistenceErrorCode.FileNotFound, "Game session file was not found.");
            }

            if (info.Length > GameSessionCheckpointFormatV1.MaximumDocumentBytes)
            {
                return LoadFailure(GameSessionPersistenceErrorCode.DocumentTooLarge, "Game session document exceeds the safety limit.");
            }

            using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.SequentialScan);
            using var memory = new MemoryStream(info.Length > 0 ? checked((int)info.Length) : 0);
            stream.CopyTo(memory);
            GameSessionDeserializationResult result = serializer.Deserialize(
                memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
            return new GameSessionLoadResult(
                result.IsSuccess,
                result.Session,
                result.ErrorCode,
                result.Issues,
                result.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return LoadFailure(GameSessionPersistenceErrorCode.FileInaccessible, "Game session file is inaccessible.");
        }
        catch (IOException)
        {
            return LoadFailure(GameSessionPersistenceErrorCode.FileInaccessible, "Game session file could not be read.");
        }
        catch (Exception)
        {
            return LoadFailure(GameSessionPersistenceErrorCode.UnexpectedError, "Game session loading failed unexpectedly.");
        }
    }

    public GameSessionSaveResult Save(string path, Core.Sessions.GameSessionState? session)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return SaveFailure(GameSessionPersistenceErrorCode.InvalidPath, [], "Game session path is invalid.");
        }

        GameSessionSerializationResult serialization = serializer.Serialize(session);
        if (!serialization.IsSuccess || serialization.Data is null)
        {
            return SaveFailure(serialization.ErrorCode, serialization.Issues, serialization.Message);
        }

        if (serialization.Data.LongLength > GameSessionCheckpointFormatV1.MaximumDocumentBytes)
        {
            return SaveFailure(GameSessionPersistenceErrorCode.DocumentTooLarge, [], "Game session document exceeds the safety limit.");
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return SaveFailure(GameSessionPersistenceErrorCode.InvalidPath, [], "Game session output directory does not exist.");
        }

        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(serialization.Data);
                stream.Flush(true);
            }

            File.Move(temporaryPath, fullPath, true);
            return new GameSessionSaveResult(true, GameSessionPersistenceErrorCode.None, [], null);
        }
        catch (UnauthorizedAccessException)
        {
            return SaveFailure(GameSessionPersistenceErrorCode.WriteFailed, [], "Game session file is inaccessible for writing.");
        }
        catch (IOException)
        {
            return SaveFailure(GameSessionPersistenceErrorCode.WriteFailed, [], "Game session file could not be written.");
        }
        catch (Exception)
        {
            return SaveFailure(GameSessionPersistenceErrorCode.UnexpectedError, [], "Game session saving failed unexpectedly.");
        }
        finally
        {
            TryDelete(temporaryPath);
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

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup failure must not replace the primary persistence result.
        }
    }

    private static GameSessionLoadResult LoadFailure(GameSessionPersistenceErrorCode code, string message) =>
        new(false, null, code, [], message);

    private static GameSessionSaveResult SaveFailure(
        GameSessionPersistenceErrorCode code,
        IReadOnlyList<GameSessionPersistenceIssue> issues,
        string? message) =>
        new(false, code, issues, message);
}
