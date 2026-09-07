using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Persistence.Projects;

public static class NativeProjectManifestFormatV1
{
    public const int Version = 1;
    public const int MaximumProjectIdLength = 96;
    public const int MaximumRelativePathLength = 512;
    public const int MaximumContentPackages = 256;
    public const long MaximumDocumentBytes = 1024 * 1024;
}

/// <summary>
/// Portable entry point for project-owned scenario, content, and optional
/// runtime checkpoint documents. Every document reference is relative to the
/// manifest directory.
/// </summary>
public sealed record NativeProjectManifest(
    int FormatVersion,
    string Id,
    string Scenario,
    IReadOnlyList<string> ContentPackages,
    string? Session);

public enum NativeProjectManifestValidationCode
{
    UnsupportedFormatVersion,
    ProjectIdMissing,
    ProjectIdTooLong,
    ScenarioPathMissing,
    ContentPackagesMissing,
    TooManyContentPackages,
    ContentPackagePathMissing,
    DuplicateContentPackagePath,
    PathTooLong,
    PathMustBeRelative,
    PathEscapesProjectDirectory,
}

public sealed record NativeProjectManifestValidationIssue(
    NativeProjectManifestValidationCode Code,
    string PropertyPath,
    string Message);

public sealed record NativeProjectManifestValidationResult(
    IReadOnlyList<NativeProjectManifestValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public enum NativeProjectPersistenceErrorCode
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

public sealed record NativeProjectManifestSerializationResult(
    bool IsSuccess,
    byte[]? Data,
    NativeProjectPersistenceErrorCode ErrorCode,
    IReadOnlyList<NativeProjectManifestValidationIssue> Issues,
    string? Message);

public sealed record NativeProjectManifestDeserializationResult(
    bool IsSuccess,
    NativeProjectManifest? Manifest,
    NativeProjectPersistenceErrorCode ErrorCode,
    IReadOnlyList<NativeProjectManifestValidationIssue> Issues,
    string? Message);

public sealed record NativeProjectManifestLoadResult(
    bool IsSuccess,
    NativeProjectManifest? Manifest,
    NativeProjectPersistenceErrorCode ErrorCode,
    IReadOnlyList<NativeProjectManifestValidationIssue> Issues,
    string? Message);

public sealed record NativeProjectManifestSaveResult(
    bool IsSuccess,
    NativeProjectPersistenceErrorCode ErrorCode,
    IReadOnlyList<NativeProjectManifestValidationIssue> Issues,
    string? Message);

public interface INativeProjectManifestValidationService
{
    NativeProjectManifestValidationResult Validate(NativeProjectManifest? manifest);
}

public interface INativeProjectManifestSerializer
{
    NativeProjectManifestSerializationResult Serialize(NativeProjectManifest? manifest);

    NativeProjectManifestDeserializationResult Deserialize(ReadOnlySpan<byte> data);
}

public interface INativeProjectManifestFileStore
{
    NativeProjectManifestLoadResult Load(string path);

    NativeProjectManifestSaveResult Save(string path, NativeProjectManifest? manifest);
}

public enum NativeProjectLoadIssueCode
{
    ManifestLoadFailed,
    ReferencedPathInvalid,
    ScenarioBundleInvalid,
    SessionLoadFailed,
    SessionMapSizeMismatch,
}

public sealed record NativeProjectLoadIssue(
    NativeProjectLoadIssueCode Code,
    string InputLabel,
    string PropertyPath,
    string DetailCode,
    string Message);

public sealed record NativeProject(
    NativeProjectManifest Manifest,
    ScenarioBundle ScenarioBundle,
    GameSessionState? Session);

public sealed record NativeProjectLoadResult(
    NativeProject? Project,
    IReadOnlyList<NativeProjectLoadIssue> Issues)
{
    public bool IsSuccess => Project is not null && Issues.Count == 0;
}

public interface INativeProjectLoader
{
    NativeProjectLoadResult Load(string manifestPath);
}
