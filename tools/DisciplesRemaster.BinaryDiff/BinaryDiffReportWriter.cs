using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.BinaryDiff;

public sealed class BinaryDiffReportWriter : IBinaryDiffReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public BinaryDiffOutputValidationResult ValidateOutputPath(
        string outputPath,
        string fileAPath,
        string fileBPath,
        OriginalGameLocation? originalGameLocation)
    {
        try
        {
            string output = Normalize(outputPath);
            string fileA = Normalize(fileAPath);
            string fileB = Normalize(fileBPath);
            if (PathEquals(output, fileA) || PathEquals(output, fileB))
            {
                return Invalid(
                    BinaryDiffWarningCode.OutputConflictsWithInput,
                    "The output directory conflicts with an input file.");
            }

            if (originalGameLocation is not null)
            {
                string source = Normalize(originalGameLocation.FullPath);
                string resolvedOutput = ResolveExistingLinks(output);
                string resolvedSource = ResolveExistingLinks(source);
                if (IsSameOrDescendant(output, source) || IsSameOrDescendant(resolvedOutput, resolvedSource))
                {
                    return Invalid(
                        BinaryDiffWarningCode.OutputInsideOriginalGameDirectory,
                        "The output directory must be outside the configured original-game directory.");
                }
            }

            return new BinaryDiffOutputValidationResult(true, output, null, "Output path is safe.");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return Invalid(BinaryDiffWarningCode.InvalidOutputPath, "The output path is invalid or cannot be safely resolved.");
        }

        static BinaryDiffOutputValidationResult Invalid(BinaryDiffWarningCode code, string message) =>
            new(false, null, code, message);
    }

    public BinaryDiffReportPaths WriteReports(
        BinaryDiffReport report,
        string outputPath,
        BinaryDiffOutputFormat format)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(outputPath);
        string? jsonPath = null;
        string? textPath = null;
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (format is BinaryDiffOutputFormat.Json or BinaryDiffOutputFormat.Both)
        {
            jsonPath = Path.Combine(outputPath, "binary-diff.json");
            File.WriteAllText(jsonPath, SerializeJson(report), utf8);
        }

        if (format is BinaryDiffOutputFormat.Text or BinaryDiffOutputFormat.Both)
        {
            textPath = Path.Combine(outputPath, "binary-diff.txt");
            File.WriteAllText(textPath, CreateTextReport(report), utf8);
        }

        return new BinaryDiffReportPaths(jsonPath, textPath);
    }

    public static string SerializeJson(BinaryDiffReport report) =>
        JsonSerializer.Serialize(report, JsonOptions) + "\n";

    public static string CreateTextReport(BinaryDiffReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Binary comparison summary");
        builder.AppendLine("=========================");
        builder.AppendLine();
        builder.AppendLine("Input files");
        AppendFile(builder, report.FileA);
        AppendFile(builder, report.FileB);
        builder.AppendLine();
        builder.AppendLine("Equality and sizes");
        builder.AppendLine($"Equal: {report.Comparison.AreEqual}");
        builder.AppendLine($"Same size: {report.Comparison.SameSize}");
        builder.AppendLine($"Size difference (B - A): {report.Comparison.SizeDifference}");
        builder.AppendLine($"Compared bytes: {report.Comparison.ComparedByteCount}");
        builder.AppendLine($"Different bytes at same offsets: {report.Comparison.DifferentByteCountAtSameOffsets}");
        builder.AppendLine($"Changed ranges detected: {report.Comparison.DetectedChangedRangeCount}");
        builder.AppendLine($"Truncated: {report.Comparison.IsTruncated}");
        builder.AppendLine();
        builder.AppendLine("Common prefix and suffix");
        builder.AppendLine($"Longest common prefix: {report.Comparison.LongestCommonPrefix} bytes");
        builder.AppendLine($"Longest common suffix: {report.Comparison.LongestCommonSuffix} bytes");
        builder.AppendLine();
        builder.AppendLine("Changed ranges");
        if (report.ChangedRanges.Count == 0)
        {
            builder.AppendLine("None.");
        }
        else
        {
            for (int index = 0; index < report.ChangedRanges.Count; index++)
            {
                BinaryChangedRange range = report.ChangedRanges[index];
                builder.AppendLine();
                builder.AppendLine($"Range {index + 1}");
                builder.AppendLine($"Offsets: {range.StartOffset}-{range.EndOffset}");
                builder.AppendLine($"Hex: {range.StartOffsetHex}-{range.EndOffsetHex}");
                builder.AppendLine($"Length: {range.Length} bytes");
                builder.AppendLine($"Different bytes: {range.DifferentByteCount}");
                builder.AppendLine($"File A: {range.FileABytes}");
                builder.AppendLine($"File B: {range.FileBBytes}");
                builder.AppendLine($"Before context A: {range.BeforeContextA}");
                builder.AppendLine($"Before context B: {range.BeforeContextB}");
                builder.AppendLine($"After context A: {range.AfterContextA}");
                builder.AppendLine($"After context B: {range.AfterContextB}");
                if (range.IsTruncated)
                {
                    builder.AppendLine($"Omitted bytes: {range.OmittedByteCount}");
                }

                foreach (BinaryNumericInterpretation interpretation in range.NumericInterpretations)
                {
                    builder.AppendLine($"Possible {interpretation.Type}: {interpretation.FileAValue} -> {interpretation.FileBValue} (confidence: {interpretation.Confidence})");
                }

                foreach (BinaryTextInterpretation interpretation in range.TextInterpretations)
                {
                    builder.AppendLine($"Possible {interpretation.Encoding} text: '{SafeText(interpretation.FileAValue)}' -> '{SafeText(interpretation.FileBValue)}' (confidence: {interpretation.Confidence})");
                }

                builder.AppendLine("This is a byte-level observation, not a confirmed file-format field.");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Explicit numeric and string searches");
        if (report.SearchResults.Count == 0)
        {
            builder.AppendLine("None.");
        }
        else
        {
            foreach (BinarySearchResult result in report.SearchResults)
            {
                builder.AppendLine($"- {result.FileLabel}: {result.Offset} ({result.OffsetHex}), {result.Type}/{result.Encoding}, value '{SafeText(result.Value)}', length {result.Length}, context {result.ContextHex}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Hypotheses");
        foreach (string hypothesis in report.Hypotheses)
        {
            builder.AppendLine($"- {hypothesis}");
        }

        builder.AppendLine();
        builder.AppendLine("Warnings");
        if (report.Warnings.Count == 0)
        {
            builder.AppendLine("None.");
        }
        else
        {
            foreach (BinaryDiffWarning warning in report.Warnings)
            {
                builder.AppendLine($"- {warning.Code} [{warning.FileLabel}]: {warning.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Limitations");
        builder.AppendLine("Ranges, numeric values, and text are bounded byte-level observations. They do not identify fields, records, coordinates, IDs, dimensions, counts, or proprietary structures without separate repeatable evidence.");
        return builder.ToString();
    }

    private static void AppendFile(StringBuilder builder, BinaryFileMetadata file)
    {
        builder.AppendLine($"- {file.Label}: {file.FileName}, {file.SizeBytes} bytes, SHA-256 {file.Sha256}, last-write UTC {file.LastWriteTimeUtc:O}, read success {file.IsReadSuccessful}");
    }

    private static string SafeText(string value)
    {
        string result = new(value.Where(character => !char.IsControl(character)).Take(256).ToArray());
        return result.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool PathEquals(string left, string right) =>
        string.Equals(left, right, PathComparison);

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        if (PathEquals(candidate, root))
        {
            return true;
        }

        string prefix = root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, PathComparison);
    }

    private static string ResolveExistingLinks(string fullPath)
    {
        string root = Path.GetPathRoot(fullPath) ?? throw new ArgumentException("Path has no root.", nameof(fullPath));
        string current = root;
        foreach (string segment in fullPath[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                continue;
            }

            var info = new DirectoryInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    current = target.FullName;
                }
            }
        }

        return Normalize(current);
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
