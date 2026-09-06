using DisciplesRemaster.Content.Catalog;

namespace DisciplesRemaster.Persistence.Content;

public enum ContentPackagePersistenceErrorCode
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

public sealed record ContentPackageSerializationResult(
    bool IsSuccess,
    byte[]? Data,
    ContentPackagePersistenceErrorCode ErrorCode,
    IReadOnlyList<ContentPackageValidationIssue> ValidationIssues,
    string? Message);

public sealed record ContentPackageDeserializationResult(
    bool IsSuccess,
    ContentPackageDefinition? Package,
    ContentPackagePersistenceErrorCode ErrorCode,
    IReadOnlyList<ContentPackageValidationIssue> ValidationIssues,
    string? Message);

public sealed record ContentPackageLoadResult(
    bool IsSuccess,
    ContentPackageDefinition? Package,
    ContentPackagePersistenceErrorCode ErrorCode,
    IReadOnlyList<ContentPackageValidationIssue> ValidationIssues,
    string? Message);

public sealed record ContentPackageSaveResult(
    bool IsSuccess,
    ContentPackagePersistenceErrorCode ErrorCode,
    IReadOnlyList<ContentPackageValidationIssue> ValidationIssues,
    string? Message);

public interface IContentPackageSerializer
{
    ContentPackageSerializationResult Serialize(ContentPackageDefinition? package);

    ContentPackageDeserializationResult Deserialize(ReadOnlySpan<byte> data);
}

public interface IContentPackageFileStore
{
    ContentPackageLoadResult Load(string path);

    ContentPackageSaveResult Save(string path, ContentPackageDefinition? package);
}
