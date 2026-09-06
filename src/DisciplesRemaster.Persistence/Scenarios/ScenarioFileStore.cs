using DisciplesRemaster.Content.Scenarios;

namespace DisciplesRemaster.Persistence.Scenarios;

public sealed class ScenarioFileStore : IScenarioFileStore
{
    public const long MaximumDocumentBytes = 64L * 1024 * 1024;

    private readonly IScenarioSerializer serializer;

    public ScenarioFileStore(IScenarioSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public ScenarioLoadResult Load(string path)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return LoadFailure(ScenarioPersistenceErrorCode.InvalidPath, "Scenario path is invalid.");
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return LoadFailure(ScenarioPersistenceErrorCode.FileNotFound, "Scenario file was not found.");
            }

            if (info.Length > MaximumDocumentBytes)
            {
                return LoadFailure(ScenarioPersistenceErrorCode.DocumentTooLarge, "Scenario document exceeds the safety limit.");
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

            ScenarioDeserializationResult result = serializer.Deserialize(memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
            return new ScenarioLoadResult(
                result.IsSuccess,
                result.Scenario,
                result.ErrorCode,
                result.ValidationIssues,
                result.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return LoadFailure(ScenarioPersistenceErrorCode.FileInaccessible, "Scenario file is inaccessible.");
        }
        catch (IOException)
        {
            return LoadFailure(ScenarioPersistenceErrorCode.FileInaccessible, "Scenario file could not be read.");
        }
        catch (Exception)
        {
            return LoadFailure(ScenarioPersistenceErrorCode.UnexpectedError, "Scenario loading failed unexpectedly.");
        }
    }

    public ScenarioSaveResult Save(string path, ScenarioDefinition? scenario)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return SaveFailure(ScenarioPersistenceErrorCode.InvalidPath, [], "Scenario path is invalid.");
        }

        ScenarioSerializationResult serialization = serializer.Serialize(scenario);
        if (!serialization.IsSuccess || serialization.Data is null)
        {
            return SaveFailure(serialization.ErrorCode, serialization.ValidationIssues, serialization.Message);
        }

        if (serialization.Data.LongLength > MaximumDocumentBytes)
        {
            return SaveFailure(ScenarioPersistenceErrorCode.DocumentTooLarge, [], "Scenario document exceeds the safety limit.");
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return SaveFailure(ScenarioPersistenceErrorCode.InvalidPath, [], "Scenario output directory does not exist.");
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
            return new ScenarioSaveResult(true, ScenarioPersistenceErrorCode.None, serialization.ValidationIssues, null);
        }
        catch (UnauthorizedAccessException)
        {
            return SaveFailure(ScenarioPersistenceErrorCode.WriteFailed, [], "Scenario file is inaccessible for writing.");
        }
        catch (IOException)
        {
            return SaveFailure(ScenarioPersistenceErrorCode.WriteFailed, [], "Scenario file could not be written.");
        }
        catch (Exception)
        {
            return SaveFailure(ScenarioPersistenceErrorCode.UnexpectedError, [], "Scenario saving failed unexpectedly.");
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
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

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A failed cleanup must not hide the primary save result.
        }
    }

    private static ScenarioLoadResult LoadFailure(ScenarioPersistenceErrorCode code, string message) =>
        new(false, null, code, [], message);

    private static ScenarioSaveResult SaveFailure(
        ScenarioPersistenceErrorCode code,
        IReadOnlyList<ScenarioValidationIssue> issues,
        string? message) =>
        new(false, code, issues, message);
}
