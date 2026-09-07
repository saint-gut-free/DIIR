namespace DisciplesRemaster.Persistence.Projects;

public sealed class NativeProjectManifestFileStore : INativeProjectManifestFileStore
{
    private readonly INativeProjectManifestSerializer serializer;

    public NativeProjectManifestFileStore(INativeProjectManifestSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public NativeProjectManifestLoadResult Load(string path)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return Failure(NativeProjectPersistenceErrorCode.InvalidPath, "Project manifest path is invalid.");
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return Failure(NativeProjectPersistenceErrorCode.FileNotFound, "Project manifest was not found.");
            }

            if (info.Length > NativeProjectManifestFormatV1.MaximumDocumentBytes)
            {
                return Failure(NativeProjectPersistenceErrorCode.DocumentTooLarge, "Project manifest exceeds the safety limit.");
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
            NativeProjectManifestDeserializationResult result = serializer.Deserialize(
                memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
            return new NativeProjectManifestLoadResult(
                result.IsSuccess,
                result.Manifest,
                result.ErrorCode,
                result.Issues,
                result.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(NativeProjectPersistenceErrorCode.FileInaccessible, "Project manifest is inaccessible.");
        }
        catch (IOException)
        {
            return Failure(NativeProjectPersistenceErrorCode.FileInaccessible, "Project manifest could not be read.");
        }
        catch (Exception)
        {
            return Failure(NativeProjectPersistenceErrorCode.UnexpectedError, "Project manifest loading failed unexpectedly.");
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

    private static NativeProjectManifestLoadResult Failure(
        NativeProjectPersistenceErrorCode code,
        string message) =>
        new(false, null, code, [], message);
}
