using DisciplesRemaster.Content.Catalog;

namespace DisciplesRemaster.Persistence.Content;

public sealed class ContentPackageFileStore : IContentPackageFileStore
{
    public const long MaximumDocumentBytes = 16L * 1024 * 1024;

    private readonly IContentPackageSerializer serializer;

    public ContentPackageFileStore(IContentPackageSerializer serializer)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    public ContentPackageLoadResult Load(string path)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return LoadFailure(ContentPackagePersistenceErrorCode.InvalidPath, "Content package path is invalid.");
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return LoadFailure(ContentPackagePersistenceErrorCode.FileNotFound, "Content package file was not found.");
            }

            if (info.Length > MaximumDocumentBytes)
            {
                return LoadFailure(ContentPackagePersistenceErrorCode.DocumentTooLarge, "Content package exceeds the safety limit.");
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
            ContentPackageDeserializationResult result = serializer.Deserialize(
                memory.GetBuffer().AsSpan(0, checked((int)memory.Length)));
            return new ContentPackageLoadResult(
                result.IsSuccess,
                result.Package,
                result.ErrorCode,
                result.ValidationIssues,
                result.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return LoadFailure(ContentPackagePersistenceErrorCode.FileInaccessible, "Content package is inaccessible.");
        }
        catch (IOException)
        {
            return LoadFailure(ContentPackagePersistenceErrorCode.FileInaccessible, "Content package could not be read.");
        }
        catch (Exception)
        {
            return LoadFailure(ContentPackagePersistenceErrorCode.UnexpectedError, "Content package loading failed unexpectedly.");
        }
    }

    public ContentPackageSaveResult Save(string path, ContentPackageDefinition? package)
    {
        if (!TryNormalizePath(path, out string fullPath))
        {
            return SaveFailure(ContentPackagePersistenceErrorCode.InvalidPath, [], "Content package path is invalid.");
        }

        ContentPackageSerializationResult serialization = serializer.Serialize(package);
        if (!serialization.IsSuccess || serialization.Data is null)
        {
            return SaveFailure(serialization.ErrorCode, serialization.ValidationIssues, serialization.Message);
        }

        if (serialization.Data.LongLength > MaximumDocumentBytes)
        {
            return SaveFailure(ContentPackagePersistenceErrorCode.DocumentTooLarge, [], "Content package exceeds the safety limit.");
        }

        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return SaveFailure(ContentPackagePersistenceErrorCode.InvalidPath, [], "Content package output directory does not exist.");
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
            return new ContentPackageSaveResult(true, ContentPackagePersistenceErrorCode.None, [], null);
        }
        catch (UnauthorizedAccessException)
        {
            return SaveFailure(ContentPackagePersistenceErrorCode.WriteFailed, [], "Content package is inaccessible for writing.");
        }
        catch (IOException)
        {
            return SaveFailure(ContentPackagePersistenceErrorCode.WriteFailed, [], "Content package could not be written.");
        }
        catch (Exception)
        {
            return SaveFailure(ContentPackagePersistenceErrorCode.UnexpectedError, [], "Content package saving failed unexpectedly.");
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
            // A cleanup failure must not hide the primary persistence result.
        }
    }

    private static ContentPackageLoadResult LoadFailure(ContentPackagePersistenceErrorCode code, string message) =>
        new(false, null, code, [], message);

    private static ContentPackageSaveResult SaveFailure(
        ContentPackagePersistenceErrorCode code,
        IReadOnlyList<ContentPackageValidationIssue> issues,
        string? message) =>
        new(false, code, issues, message);
}
