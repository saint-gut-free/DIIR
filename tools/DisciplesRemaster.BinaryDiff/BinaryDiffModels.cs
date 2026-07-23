using System.Text.Json.Serialization;

namespace DisciplesRemaster.BinaryDiff;

public enum BinaryDiffWarningCode
{
    FileInaccessible,
    FileChangedDuringRead,
    ReportTruncated,
    ChangedRangeTruncated,
    SearchResultsTruncated,
    OutputConflictsWithInput,
    OutputInsideOriginalGameDirectory,
    InvalidOutputPath,
    HashingFailed,
    ComparisonFailed,
    UnsupportedMetadata,
}

public enum BinarySearchValueType
{
    Int16,
    UInt16,
    Int32,
    UInt32,
    String,
}

public enum BinaryStringEncoding
{
    Ascii,
    Utf8,
    Utf16Le,
}

public enum BinaryDiffOutputFormat
{
    Text,
    Json,
    Both,
}

public sealed record BinarySearchRequest(
    BinarySearchValueType Type,
    string Value,
    BinaryStringEncoding? Encoding,
    [property: JsonIgnore] byte[] Pattern);

public sealed record BinaryDiffOptions(
    int Context,
    int MaxRanges,
    IReadOnlyList<BinarySearchRequest> Searches)
{
    public const int DefaultContext = 16;
    public const int DefaultMaxRanges = 200;
    public const int MaximumContext = 256;
    public const int MaximumRanges = 1000;
    public const int MaximumDumpBytes = 64;
    public const int MaximumSearchResults = 1000;
    public const int MaximumSearchStringLength = 256;
}

public sealed record BinaryDiffWarning(
    BinaryDiffWarningCode Code,
    string FileLabel,
    string Message);

public sealed record BinaryFileMetadata(
    string Label,
    string FileName,
    long SizeBytes,
    string? Sha256,
    DateTime LastWriteTimeUtc,
    bool IsReadSuccessful);

public sealed record BinaryNumericInterpretation(
    string Type,
    string FileAValue,
    string FileBValue,
    string Confidence);

public sealed record BinaryTextInterpretation(
    string Encoding,
    string FileAValue,
    string FileBValue,
    string Confidence);

public sealed record BinaryChangedRange(
    long StartOffset,
    string StartOffsetHex,
    long EndOffset,
    string EndOffsetHex,
    long Length,
    long DifferentByteCount,
    string BeforeContextA,
    string BeforeContextB,
    string FileABytes,
    string FileBBytes,
    string AfterContextA,
    string AfterContextB,
    IReadOnlyList<BinaryNumericInterpretation> NumericInterpretations,
    IReadOnlyList<BinaryTextInterpretation> TextInterpretations,
    string Confidence,
    bool IsTruncated,
    long OmittedByteCount);

public sealed record BinarySearchResult(
    string FileLabel,
    long Offset,
    string OffsetHex,
    string Type,
    string Encoding,
    string Value,
    int Length,
    string ContextHex);

public sealed record BinaryComparisonSummary(
    bool AreEqual,
    bool SameSize,
    long SizeDifference,
    long ComparedByteCount,
    long DifferentByteCountAtSameOffsets,
    int DetectedChangedRangeCount,
    long LongestCommonPrefix,
    long LongestCommonSuffix,
    bool IsStable,
    bool IsTruncated);

public sealed record BinaryDiffReport(
    string ReportFormatVersion,
    BinaryFileMetadata FileA,
    BinaryFileMetadata FileB,
    BinaryDiffOptions Options,
    BinaryComparisonSummary Comparison,
    IReadOnlyList<BinaryChangedRange> ChangedRanges,
    IReadOnlyList<BinarySearchResult> SearchResults,
    IReadOnlyList<string> Hypotheses,
    IReadOnlyList<BinaryDiffWarning> Warnings)
{
    public const string CurrentReportFormatVersion = "1.0";

    public bool IsPartial => Warnings.Count > 0;
}

public sealed class BinaryInputException(string safeMessage, Exception? innerException = null)
    : IOException(safeMessage, innerException);

public interface IBinaryComparisonService
{
    BinaryDiffReport Compare(
        string fileAPath,
        string fileBPath,
        string labelA,
        string labelB,
        BinaryDiffOptions options);
}
