namespace DisciplesRemaster.OriginalGame;

public enum OriginalGameFileCategory
{
    Executable,
    Library,
    MapOrScenarioCandidate,
    SaveCandidate,
    Image,
    Audio,
    Archive,
    Text,
    Configuration,
    Data,
    Unknown,
}

public enum OriginalGameFileSignature
{
    Unknown,
    WindowsPortableExecutable,
    Png,
    Jpeg,
    Gif,
    Bmp,
    Wave,
    Ogg,
    Zip,
    Gzip,
    SevenZip,
    Rar,
    Utf8Bom,
    Utf16LittleEndianBom,
    Utf16BigEndianBom,
    PlainText,
}

public enum OriginalGameInventoryWarningCode
{
    FileInaccessible,
    DirectoryInaccessible,
    ReparsePointSkipped,
    MaximumDepthExceeded,
    HiddenItemSkipped,
    FileChangedDuringRead,
    UnsupportedFileMetadata,
    OutputInsideOriginalGameDirectory,
    InvalidOutputPath,
    HashingFailed,
    SignatureDetectionFailed,
}

public sealed record OriginalGameInventoryOptions(bool IncludeHidden = false, int MaxDepth = 64)
{
    public const int DefaultMaxDepth = 64;
}

public sealed record OriginalGameInventoryWarning(
    OriginalGameInventoryWarningCode Code,
    string RelativePath,
    string Message);

public sealed record OriginalGameInventoryEntry(
    string RelativePath,
    string FileName,
    string Extension,
    long SizeBytes,
    string? Sha256,
    DateTime LastWriteTimeUtc,
    int Depth,
    FileAttributes Attributes,
    OriginalGameFileCategory Category,
    OriginalGameFileSignature Signature,
    bool IsHidden,
    bool IsReparsePoint,
    bool IsReadSuccessful,
    string? Warning);

public sealed record OriginalGameInventoryCount(string Name, long Count);

public sealed record OriginalGameInventoryDuplicateGroup(
    string Sha256,
    long SizeBytes,
    IReadOnlyList<string> RelativePaths);

public sealed record OriginalGameInventorySummary(
    int FilesProcessed,
    int FilesSkipped,
    long TotalSizeBytes,
    IReadOnlyList<OriginalGameInventoryCount> Extensions,
    IReadOnlyList<OriginalGameInventoryCount> Categories,
    IReadOnlyList<OriginalGameInventoryDuplicateGroup> DuplicateGroups);

public sealed record OriginalGameInventory(
    string ReportFormatVersion,
    string SourceLocation,
    OriginalGameInventoryOptions Options,
    IReadOnlyList<OriginalGameInventoryEntry> Files,
    IReadOnlyList<OriginalGameInventoryWarning> Warnings,
    OriginalGameInventorySummary Summary)
{
    public const string CurrentReportFormatVersion = "1.0";

    public bool IsPartial => Warnings.Count > 0;
}
