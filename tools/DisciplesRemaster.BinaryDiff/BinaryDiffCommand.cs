using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.BinaryDiff;

public static class BinaryDiffCommand
{
    public const int SuccessExitCode = 0;
    public const int InputErrorExitCode = 2;
    public const int PartialSuccessExitCode = 3;
    public const int UsageErrorExitCode = 64;
    public const int SoftwareErrorExitCode = 70;

    public static int Run(
        IReadOnlyList<string> arguments,
        IOriginalGameLocationProvider locationProvider,
        TextWriter output,
        TextWriter error) =>
        Run(
            arguments,
            locationProvider,
            new BinaryComparisonService(),
            new BinaryDiffReportWriter(),
            Environment.CurrentDirectory,
            output,
            error);

    public static int Run(
        IReadOnlyList<string> arguments,
        IOriginalGameLocationProvider locationProvider,
        IBinaryComparisonService comparisonService,
        IBinaryDiffReportWriter reportWriter,
        string workingDirectory,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(locationProvider);
        ArgumentNullException.ThrowIfNull(comparisonService);
        ArgumentNullException.ThrowIfNull(reportWriter);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(arguments, workingDirectory, error, out ParsedCommand? parsed))
        {
            return UsageErrorExitCode;
        }

        try
        {
            OriginalGameLocationValidationResult locationResult = locationProvider.Validate();
            OriginalGameLocation? originalLocation = locationResult.IsSuccess ? locationResult.Location : null;
            BinaryDiffOutputValidationResult outputValidation = reportWriter.ValidateOutputPath(
                parsed!.OutputPath,
                parsed.FileAPath,
                parsed.FileBPath,
                originalLocation);
            if (!outputValidation.IsValid)
            {
                error.WriteLine("Binary diff output validation failed.");
                error.WriteLine($"Code: {outputValidation.ErrorCode}");
                error.WriteLine(outputValidation.Message);
                return UsageErrorExitCode;
            }

            BinaryDiffReport report = comparisonService.Compare(
                parsed.FileAPath,
                parsed.FileBPath,
                parsed.LabelA,
                parsed.LabelB,
                parsed.Options);
            BinaryDiffReportPaths paths = reportWriter.WriteReports(
                report,
                outputValidation.NormalizedOutputPath!,
                parsed.Format);

            output.WriteLine("Binary comparison completed.");
            output.WriteLine($"File A: {report.FileA.Label} ({report.FileA.FileName}, {report.FileA.SizeBytes} bytes, SHA-256 {report.FileA.Sha256})");
            output.WriteLine($"File B: {report.FileB.Label} ({report.FileB.FileName}, {report.FileB.SizeBytes} bytes, SHA-256 {report.FileB.Sha256})");
            output.WriteLine($"Equal: {report.Comparison.AreEqual}");
            output.WriteLine($"Changed ranges: {report.Comparison.DetectedChangedRangeCount}");
            output.WriteLine($"Different bytes at same offsets: {report.Comparison.DifferentByteCountAtSameOffsets}");
            if (paths.JsonPath is not null)
            {
                output.WriteLine($"JSON report: {DisplayOutputPath(paths.JsonPath, workingDirectory)}");
            }

            if (paths.TextPath is not null)
            {
                output.WriteLine($"Text report: {DisplayOutputPath(paths.TextPath, workingDirectory)}");
            }

            if (report.IsPartial)
            {
                output.WriteLine($"Warnings: {report.Warnings.Count}; comparison is partial.");
                return PartialSuccessExitCode;
            }

            return SuccessExitCode;
        }
        catch (BinaryInputException exception)
        {
            error.WriteLine("Binary comparison input validation failed.");
            error.WriteLine("Code: FileInaccessible");
            error.WriteLine(exception.Message);
            return InputErrorExitCode;
        }
        catch (Exception)
        {
            error.WriteLine("Binary comparison failed unexpectedly.");
            error.WriteLine("Code: ComparisonFailed");
            return SoftwareErrorExitCode;
        }
    }

    private static bool TryParse(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TextWriter error,
        out ParsedCommand? command)
    {
        command = null;
        if (arguments.Count < 3 || !string.Equals(arguments[0], "compare", StringComparison.Ordinal))
        {
            return Invalid("Unknown command or missing input files.");
        }

        string fileAPath;
        string fileBPath;
        try
        {
            fileAPath = Path.GetFullPath(arguments[1], workingDirectory);
            fileBPath = Path.GetFullPath(arguments[2], workingDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Invalid("An input path is invalid.");
        }

        string outputPath = Path.Combine(workingDirectory, "artifacts", "research", "binary-diff");
        BinaryDiffOutputFormat format = BinaryDiffOutputFormat.Both;
        int context = BinaryDiffOptions.DefaultContext;
        int maxRanges = BinaryDiffOptions.DefaultMaxRanges;
        string labelA = "File A";
        string labelB = "File B";
        var numericSearches = new List<BinarySearchRequest>();
        var stringSearches = new List<string>();
        string encoding = "all";
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 3; index < arguments.Count; index++)
        {
            string option = arguments[index];
            switch (option)
            {
                case "--output" when TakeValue(arguments, ref index, out string? outputValue) && seen.Add(option):
                    try
                    {
                        outputPath = Path.GetFullPath(outputValue!, workingDirectory);
                    }
                    catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                        return Invalid("--output is not a valid path.");
                    }

                    break;
                case "--format" when TakeValue(arguments, ref index, out string? formatValue) && seen.Add(option):
                    if (!TryParseFormat(formatValue!, out format)) return Invalid("--format must be text, json, or both.");
                    break;
                case "--context" when TakeValue(arguments, ref index, out string? contextValue) && seen.Add(option):
                    if (!int.TryParse(contextValue, NumberStyles.None, CultureInfo.InvariantCulture, out context) || context < 0 || context > BinaryDiffOptions.MaximumContext)
                        return Invalid($"--context must be between 0 and {BinaryDiffOptions.MaximumContext}.");
                    break;
                case "--max-ranges" when TakeValue(arguments, ref index, out string? rangesValue) && seen.Add(option):
                    if (!int.TryParse(rangesValue, NumberStyles.None, CultureInfo.InvariantCulture, out maxRanges) || maxRanges <= 0 || maxRanges > BinaryDiffOptions.MaximumRanges)
                        return Invalid($"--max-ranges must be between 1 and {BinaryDiffOptions.MaximumRanges}.");
                    break;
                case "--label-a" when TakeValue(arguments, ref index, out string? labelAValue) && seen.Add(option):
                    if (!IsSafeLabel(labelAValue!)) return Invalid("--label-a is not a safe label.");
                    labelA = labelAValue!;
                    break;
                case "--label-b" when TakeValue(arguments, ref index, out string? labelBValue) && seen.Add(option):
                    if (!IsSafeLabel(labelBValue!)) return Invalid("--label-b is not a safe label.");
                    labelB = labelBValue!;
                    break;
                case "--encoding" when TakeValue(arguments, ref index, out string? encodingValue) && seen.Add(option):
                    if (encodingValue is not ("ascii" or "utf8" or "utf16le" or "all")) return Invalid("--encoding must be ascii, utf8, utf16le, or all.");
                    encoding = encodingValue!;
                    break;
                case "--search-string" when TakeValue(arguments, ref index, out string? stringValue):
                    if (stringValue!.Length == 0 || stringValue.Length > BinaryDiffOptions.MaximumSearchStringLength || stringValue.Any(char.IsControl))
                        return Invalid($"--search-string must contain 1-{BinaryDiffOptions.MaximumSearchStringLength} printable characters.");
                    stringSearches.Add(stringValue);
                    break;
                case "--search-int16" when TakeValue(arguments, ref index, out string? int16Value):
                    if (!short.TryParse(int16Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out short int16)) return Invalid("--search-int16 requires an Int16 value.");
                    numericSearches.Add(CreateNumeric(BinarySearchValueType.Int16, int16Value!, 2, span => BinaryPrimitives.WriteInt16LittleEndian(span, int16)));
                    break;
                case "--search-uint16" when TakeValue(arguments, ref index, out string? uint16Value):
                    if (!ushort.TryParse(uint16Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort uint16)) return Invalid("--search-uint16 requires a UInt16 value.");
                    numericSearches.Add(CreateNumeric(BinarySearchValueType.UInt16, uint16Value!, 2, span => BinaryPrimitives.WriteUInt16LittleEndian(span, uint16)));
                    break;
                case "--search-int32" when TakeValue(arguments, ref index, out string? int32Value):
                    if (!int.TryParse(int32Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int int32)) return Invalid("--search-int32 requires an Int32 value.");
                    numericSearches.Add(CreateNumeric(BinarySearchValueType.Int32, int32Value!, 4, span => BinaryPrimitives.WriteInt32LittleEndian(span, int32)));
                    break;
                case "--search-uint32" when TakeValue(arguments, ref index, out string? uint32Value):
                    if (!uint.TryParse(uint32Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint uint32)) return Invalid("--search-uint32 requires a UInt32 value.");
                    numericSearches.Add(CreateNumeric(BinarySearchValueType.UInt32, uint32Value!, 4, span => BinaryPrimitives.WriteUInt32LittleEndian(span, uint32)));
                    break;
                default:
                    return Invalid($"Unknown, duplicate, or incomplete option: {option}");
            }
        }

        var searches = new List<BinarySearchRequest>(numericSearches);
        foreach (string value in stringSearches)
        {
            foreach (BinaryStringEncoding selectedEncoding in ExpandEncodings(encoding, value))
            {
                searches.Add(new BinarySearchRequest(
                    BinarySearchValueType.String,
                    value,
                    selectedEncoding,
                    GetBytes(value, selectedEncoding)));
            }
        }

        command = new ParsedCommand(
            fileAPath,
            fileBPath,
            outputPath,
            format,
            labelA,
            labelB,
            new BinaryDiffOptions(context, maxRanges, searches));
        return true;

        bool Invalid(string message)
        {
            error.WriteLine(message);
            WriteUsage(error);
            return false;
        }
    }

    private static BinarySearchRequest CreateNumeric(
        BinarySearchValueType type,
        string value,
        int length,
        SpanWriter writer)
    {
        byte[] pattern = new byte[length];
        writer(pattern);
        return new BinarySearchRequest(type, value, null, pattern);
    }

    private static IEnumerable<BinaryStringEncoding> ExpandEncodings(string encoding, string value)
    {
        if ((encoding is "ascii" or "all") && value.All(character => character <= 0x7F)) yield return BinaryStringEncoding.Ascii;
        if (encoding is "utf8" or "all") yield return BinaryStringEncoding.Utf8;
        if (encoding is "utf16le" or "all") yield return BinaryStringEncoding.Utf16Le;
    }

    private static byte[] GetBytes(string value, BinaryStringEncoding encoding) => encoding switch
    {
        BinaryStringEncoding.Ascii => Encoding.ASCII.GetBytes(value),
        BinaryStringEncoding.Utf8 => Encoding.UTF8.GetBytes(value),
        BinaryStringEncoding.Utf16Le => Encoding.Unicode.GetBytes(value),
        _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
    };

    private static bool TakeValue(IReadOnlyList<string> arguments, ref int index, out string? value)
    {
        if (index + 1 >= arguments.Count)
        {
            value = null;
            return false;
        }

        value = arguments[++index];
        return true;
    }

    private static bool TryParseFormat(string value, out BinaryDiffOutputFormat format)
    {
        format = value switch
        {
            "text" => BinaryDiffOutputFormat.Text,
            "json" => BinaryDiffOutputFormat.Json,
            "both" => BinaryDiffOutputFormat.Both,
            _ => (BinaryDiffOutputFormat)(-1),
        };
        return Enum.IsDefined(format);
    }

    private static bool IsSafeLabel(string value) =>
        value.Length is > 0 and <= 64 && value.All(character => !char.IsControl(character));

    private static string DisplayOutputPath(string path, string workingDirectory)
    {
        string relative = Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');
        return relative == "." || relative.StartsWith("../", StringComparison.Ordinal) ? path : relative;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project tools/DisciplesRemaster.BinaryDiff -- compare <file-a> <file-b> [options]");
        writer.WriteLine("Options: --output <directory> --format text|json|both --context <0-256> --max-ranges <1-1000>");
        writer.WriteLine("         --search-int16 <n> --search-uint16 <n> --search-int32 <n> --search-uint32 <n>");
        writer.WriteLine("         --search-string <text> --encoding ascii|utf8|utf16le|all --label-a <label> --label-b <label>");
    }

    private delegate void SpanWriter(Span<byte> span);

    private sealed record ParsedCommand(
        string FileAPath,
        string FileBPath,
        string OutputPath,
        BinaryDiffOutputFormat Format,
        string LabelA,
        string LabelB,
        BinaryDiffOptions Options);
}
