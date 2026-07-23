using System.Buffers.Binary;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace DisciplesRemaster.BinaryDiff;

public sealed class BinaryComparisonService : IBinaryComparisonService
{
    private const int BufferSize = 128 * 1024;

    public BinaryDiffReport Compare(
        string fileAPath,
        string fileBPath,
        string labelA,
        string labelB,
        BinaryDiffOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileAPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileBPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelA);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelB);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            return CompareCore(fileAPath, fileBPath, labelA, labelB, options);
        }
        catch (BinaryInputException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedIoException(exception))
        {
            throw new BinaryInputException("An input file became unavailable during read-only analysis.", exception);
        }
    }

    private static BinaryDiffReport CompareCore(
        string fileAPath,
        string fileBPath,
        string labelA,
        string labelB,
        BinaryDiffOptions options)
    {

        FileSnapshot beforeA = ReadSnapshot(fileAPath);
        FileSnapshot beforeB = ReadSnapshot(fileBPath);
        string hashA = ComputeHash(fileAPath);
        string hashB = ComputeHash(fileBPath);
        bool areEqual = beforeA.Size == beforeB.Size && string.Equals(hashA, hashB, StringComparison.Ordinal);
        var warnings = new List<BinaryDiffWarning>();
        var rawRanges = new List<RawRange>();
        long differentBytes = 0;
        int detectedRangeCount = 0;
        long commonLength = Math.Min(beforeA.Size, beforeB.Size);
        long prefix;
        long suffix;

        if (areEqual)
        {
            prefix = beforeA.Size;
            suffix = beforeA.Size;
        }
        else
        {
            prefix = ComputeCommonPrefix(fileAPath, fileBPath, commonLength);
            suffix = ComputeCommonSuffix(fileAPath, fileBPath, beforeA.Size, beforeB.Size, commonLength);
            CompareSameOffsets(
                fileAPath,
                fileBPath,
                commonLength,
                options.MaxRanges,
                rawRanges,
                ref differentBytes,
                ref detectedRangeCount);

            if (beforeA.Size != beforeB.Size)
            {
                long tailStart = commonLength;
                long tailEnd = Math.Max(beforeA.Size, beforeB.Size) - 1;
                detectedRangeCount++;
                if (rawRanges.Count < options.MaxRanges)
                {
                    rawRanges.Add(new RawRange(tailStart, tailEnd, tailEnd - tailStart + 1));
                }
            }
        }

        bool rangeLimitReached = detectedRangeCount > rawRanges.Count;
        if (rangeLimitReached)
        {
            warnings.Add(new BinaryDiffWarning(
                BinaryDiffWarningCode.ReportTruncated,
                "Comparison",
                "Changed range output reached the configured limit."));
        }

        IReadOnlyList<BinaryChangedRange> ranges = rawRanges
            .Select(range => MaterializeRange(fileAPath, fileBPath, beforeA.Size, beforeB.Size, range, options.Context, warnings))
            .ToArray();
        IReadOnlyList<BinarySearchResult> searchResults = RunSearches(
            fileAPath,
            fileBPath,
            labelA,
            labelB,
            beforeA.Size,
            beforeB.Size,
            options,
            warnings);

        FileSnapshot afterA = ReadSnapshot(fileAPath);
        FileSnapshot afterB = ReadSnapshot(fileBPath);
        bool changedA = beforeA != afterA;
        bool changedB = beforeB != afterB;
        if (changedA)
        {
            warnings.Add(new BinaryDiffWarning(
                BinaryDiffWarningCode.FileChangedDuringRead,
                labelA,
                "File metadata changed during analysis; results may not represent a stable state."));
        }

        if (changedB)
        {
            warnings.Add(new BinaryDiffWarning(
                BinaryDiffWarningCode.FileChangedDuringRead,
                labelB,
                "File metadata changed during analysis; results may not represent a stable state."));
        }

        warnings.Sort((left, right) =>
        {
            int code = left.Code.CompareTo(right.Code);
            return code != 0 ? code : StringComparer.Ordinal.Compare(left.FileLabel, right.FileLabel);
        });

        bool isTruncated = rangeLimitReached || ranges.Any(range => range.IsTruncated) ||
            warnings.Any(warning => warning.Code == BinaryDiffWarningCode.SearchResultsTruncated);
        var summary = new BinaryComparisonSummary(
            areEqual,
            beforeA.Size == beforeB.Size,
            beforeB.Size - beforeA.Size,
            commonLength,
            differentBytes,
            detectedRangeCount,
            prefix,
            suffix,
            !changedA && !changedB,
            isTruncated);
        var hypotheses = new List<string>();
        if (beforeA.Size != beforeB.Size)
        {
            hypotheses.Add("Different file sizes may indicate an insertion, deletion, or tail change; this is a hypothesis, not a confirmed format structure.");
        }

        hypotheses.Add("Numeric and text interpretations are low-confidence byte-level hypotheses, not confirmed file-format fields.");

        return new BinaryDiffReport(
            BinaryDiffReport.CurrentReportFormatVersion,
            new BinaryFileMetadata(labelA, Path.GetFileName(fileAPath), beforeA.Size, hashA, beforeA.LastWriteUtc, true),
            new BinaryFileMetadata(labelB, Path.GetFileName(fileBPath), beforeB.Size, hashB, beforeB.LastWriteUtc, true),
            options,
            summary,
            ranges,
            searchResults,
            hypotheses,
            warnings);
    }

    private static FileSnapshot ReadSnapshot(string path)
    {
        try
        {
            var info = new FileInfo(path);
            info.Refresh();
            if (!info.Exists || (info.Attributes & FileAttributes.Directory) != 0)
            {
                throw new BinaryInputException("An input file is missing or is not a regular file.");
            }

            return new FileSnapshot(info.Length, info.LastWriteTimeUtc);
        }
        catch (BinaryInputException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedIoException(exception))
        {
            throw new BinaryInputException("Input file metadata is unavailable.", exception);
        }
    }

    private static string ComputeHash(string path)
    {
        try
        {
            using FileStream stream = OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (IsExpectedIoException(exception))
        {
            throw new BinaryInputException("An input file could not be hashed with read-only access.", exception);
        }
    }

    private static long ComputeCommonPrefix(string pathA, string pathB, long commonLength)
    {
        using FileStream streamA = OpenRead(pathA);
        using FileStream streamB = OpenRead(pathB);
        byte[] bufferA = new byte[BufferSize];
        byte[] bufferB = new byte[BufferSize];
        long offset = 0;
        while (offset < commonLength)
        {
            int requested = (int)Math.Min(BufferSize, commonLength - offset);
            ReadExactly(streamA, bufferA, requested);
            ReadExactly(streamB, bufferB, requested);
            for (int index = 0; index < requested; index++)
            {
                if (bufferA[index] != bufferB[index])
                {
                    return offset + index;
                }
            }

            offset += requested;
        }

        return commonLength;
    }

    private static long ComputeCommonSuffix(
        string pathA,
        string pathB,
        long sizeA,
        long sizeB,
        long maximumLength)
    {
        using FileStream streamA = OpenRead(pathA);
        using FileStream streamB = OpenRead(pathB);
        byte[] bufferA = new byte[BufferSize];
        byte[] bufferB = new byte[BufferSize];
        long matched = 0;
        while (matched < maximumLength)
        {
            int requested = (int)Math.Min(BufferSize, maximumLength - matched);
            streamA.Position = sizeA - matched - requested;
            streamB.Position = sizeB - matched - requested;
            ReadExactly(streamA, bufferA, requested);
            ReadExactly(streamB, bufferB, requested);
            for (int index = requested - 1; index >= 0; index--)
            {
                if (bufferA[index] != bufferB[index])
                {
                    return matched + (requested - 1 - index);
                }
            }

            matched += requested;
        }

        return maximumLength;
    }

    private static void CompareSameOffsets(
        string pathA,
        string pathB,
        long length,
        int maxRanges,
        List<RawRange> ranges,
        ref long differentBytes,
        ref int detectedRanges)
    {
        int detected = detectedRanges;
        using FileStream streamA = OpenRead(pathA);
        using FileStream streamB = OpenRead(pathB);
        byte[] bufferA = new byte[BufferSize];
        byte[] bufferB = new byte[BufferSize];
        long offset = 0;
        long? rangeStart = null;
        long rangeDifferences = 0;
        while (offset < length)
        {
            int requested = (int)Math.Min(BufferSize, length - offset);
            ReadExactly(streamA, bufferA, requested);
            ReadExactly(streamB, bufferB, requested);
            for (int index = 0; index < requested; index++)
            {
                bool different = bufferA[index] != bufferB[index];
                long absoluteOffset = offset + index;
                if (different)
                {
                    rangeStart ??= absoluteOffset;
                    rangeDifferences++;
                    differentBytes++;
                }
                else if (rangeStart is not null)
                {
                    AddRange(rangeStart.Value, absoluteOffset - 1, rangeDifferences);
                    rangeStart = null;
                    rangeDifferences = 0;
                }
            }

            offset += requested;
        }

        if (rangeStart is not null)
        {
            AddRange(rangeStart.Value, length - 1, rangeDifferences);
        }

        detectedRanges = detected;

        void AddRange(long start, long end, long count)
        {
            detected++;
            if (ranges.Count < maxRanges)
            {
                ranges.Add(new RawRange(start, end, count));
            }
        }
    }

    private static BinaryChangedRange MaterializeRange(
        string pathA,
        string pathB,
        long sizeA,
        long sizeB,
        RawRange range,
        int context,
        List<BinaryDiffWarning> warnings)
    {
        long length = range.End - range.Start + 1;
        bool truncated = length > BinaryDiffOptions.MaximumDumpBytes;
        long omitted = truncated ? length - BinaryDiffOptions.MaximumDumpBytes : 0;
        if (truncated)
        {
            warnings.Add(new BinaryDiffWarning(
                BinaryDiffWarningCode.ChangedRangeTruncated,
                "Comparison",
                $"Changed range at {FormatOffset(range.Start)} was truncated to bounded byte dumps."));
        }

        string bytesA = ReadRangeDump(pathA, sizeA, range.Start, length);
        string bytesB = ReadRangeDump(pathB, sizeB, range.Start, length);
        long beforeStart = Math.Max(0, range.Start - context);
        int beforeLength = (int)(range.Start - beforeStart);
        long afterStart = range.End + 1;
        int afterLengthA = (int)Math.Min(context, Math.Max(0, sizeA - afterStart));
        int afterLengthB = (int)Math.Min(context, Math.Max(0, sizeB - afterStart));
        byte[] interpretationA = ReadBounded(pathA, sizeA, range.Start, 8);
        byte[] interpretationB = ReadBounded(pathB, sizeB, range.Start, 8);

        return new BinaryChangedRange(
            range.Start,
            FormatOffset(range.Start),
            range.End,
            FormatOffset(range.End),
            length,
            range.DifferentCount,
            ToHex(ReadBounded(pathA, sizeA, beforeStart, beforeLength)),
            ToHex(ReadBounded(pathB, sizeB, beforeStart, beforeLength)),
            bytesA,
            bytesB,
            ToHex(ReadBounded(pathA, sizeA, afterStart, afterLengthA)),
            ToHex(ReadBounded(pathB, sizeB, afterStart, afterLengthB)),
            InterpretNumbers(interpretationA, interpretationB),
            InterpretText(interpretationA, interpretationB),
            "Low",
            truncated,
            omitted);
    }

    private static string ReadRangeDump(string path, long size, long start, long length)
    {
        long available = Math.Max(0, Math.Min(length, size - start));
        if (available == 0)
        {
            return "<no-bytes>";
        }

        if (available <= BinaryDiffOptions.MaximumDumpBytes)
        {
            return ToHex(ReadBounded(path, size, start, (int)available));
        }

        int half = BinaryDiffOptions.MaximumDumpBytes / 2;
        byte[] first = ReadBounded(path, size, start, half);
        byte[] last = ReadBounded(path, size, start + available - half, half);
        return $"{ToHex(first)} ... [{available - (half * 2)} bytes omitted] ... {ToHex(last)}";
    }

    private static byte[] ReadBounded(string path, long size, long offset, int count)
    {
        if (count <= 0 || offset >= size)
        {
            return [];
        }

        int actual = (int)Math.Min(count, size - offset);
        byte[] bytes = new byte[actual];
        using FileStream stream = OpenRead(path);
        stream.Position = offset;
        ReadExactly(stream, bytes, actual);
        return bytes;
    }

    private static IReadOnlyList<BinaryNumericInterpretation> InterpretNumbers(
        ReadOnlySpan<byte> bytesA,
        ReadOnlySpan<byte> bytesB)
    {
        var results = new List<BinaryNumericInterpretation>();
        if (bytesA.Length >= 2 && bytesB.Length >= 2)
        {
            results.Add(new("Int16 little-endian", BinaryPrimitives.ReadInt16LittleEndian(bytesA).ToString(), BinaryPrimitives.ReadInt16LittleEndian(bytesB).ToString(), "Low"));
            results.Add(new("UInt16 little-endian", BinaryPrimitives.ReadUInt16LittleEndian(bytesA).ToString(), BinaryPrimitives.ReadUInt16LittleEndian(bytesB).ToString(), "Low"));
        }

        if (bytesA.Length >= 4 && bytesB.Length >= 4)
        {
            results.Add(new("Int32 little-endian", BinaryPrimitives.ReadInt32LittleEndian(bytesA).ToString(), BinaryPrimitives.ReadInt32LittleEndian(bytesB).ToString(), "Low"));
            results.Add(new("UInt32 little-endian", BinaryPrimitives.ReadUInt32LittleEndian(bytesA).ToString(), BinaryPrimitives.ReadUInt32LittleEndian(bytesB).ToString(), "Low"));
        }

        if (bytesA.Length >= 8 && bytesB.Length >= 8)
        {
            results.Add(new("Int64 little-endian", BinaryPrimitives.ReadInt64LittleEndian(bytesA).ToString(), BinaryPrimitives.ReadInt64LittleEndian(bytesB).ToString(), "Low"));
            results.Add(new("UInt64 little-endian", BinaryPrimitives.ReadUInt64LittleEndian(bytesA).ToString(), BinaryPrimitives.ReadUInt64LittleEndian(bytesB).ToString(), "Low"));
        }

        return results;
    }

    private static IReadOnlyList<BinaryTextInterpretation> InterpretText(byte[] bytesA, byte[] bytesB)
    {
        if (!IsPrintableAscii(bytesA) || !IsPrintableAscii(bytesB) || bytesA.Length == 0 || bytesB.Length == 0)
        {
            return [];
        }

        return [new BinaryTextInterpretation("ASCII", Encoding.ASCII.GetString(bytesA), Encoding.ASCII.GetString(bytesB), "Low")];
    }

    private static IReadOnlyList<BinarySearchResult> RunSearches(
        string pathA,
        string pathB,
        string labelA,
        string labelB,
        long sizeA,
        long sizeB,
        BinaryDiffOptions options,
        List<BinaryDiffWarning> warnings)
    {
        var results = new List<BinarySearchResult>();
        bool truncated = false;
        foreach ((string path, string label, long size) in new[] { (pathA, labelA, sizeA), (pathB, labelB, sizeB) })
        {
            foreach (BinarySearchRequest request in options.Searches)
            {
                if (results.Count >= BinaryDiffOptions.MaximumSearchResults)
                {
                    truncated = true;
                    break;
                }

                SearchFile(path, label, size, request, options.Context, results, ref truncated);
            }

            if (truncated)
            {
                break;
            }
        }

        if (truncated)
        {
            warnings.Add(new BinaryDiffWarning(
                BinaryDiffWarningCode.SearchResultsTruncated,
                "Search",
                "Search results reached the hard safety limit."));
        }

        results.Sort((left, right) =>
        {
            int offset = left.Offset.CompareTo(right.Offset);
            if (offset != 0) return offset;
            int encoding = StringComparer.Ordinal.Compare(left.Encoding, right.Encoding);
            if (encoding != 0) return encoding;
            return StringComparer.Ordinal.Compare(left.FileLabel, right.FileLabel);
        });
        return results;
    }

    private static void SearchFile(
        string path,
        string label,
        long size,
        BinarySearchRequest request,
        int context,
        List<BinarySearchResult> results,
        ref bool truncated)
    {
        byte[] pattern = request.Pattern;
        if (pattern.Length == 0 || size < pattern.Length)
        {
            return;
        }

        using FileStream stream = OpenRead(path);
        byte[] buffer = new byte[BufferSize + pattern.Length - 1];
        int carried = 0;
        long bufferBaseOffset = 0;
        while (true)
        {
            int read = stream.Read(buffer, carried, BufferSize);
            int total = carried + read;
            if (total < pattern.Length && read == 0)
            {
                break;
            }

            int searchable = read == 0 ? total - pattern.Length + 1 : total - pattern.Length + 1;
            for (int index = 0; index < searchable; index++)
            {
                if (buffer.AsSpan(index, pattern.Length).SequenceEqual(pattern))
                {
                    long offset = bufferBaseOffset + index;
                    long contextStart = Math.Max(0, offset - context);
                    int contextLength = (int)Math.Min((long)(context * 2) + pattern.Length, size - contextStart);
                    results.Add(new BinarySearchResult(
                        label,
                        offset,
                        FormatOffset(offset),
                        request.Type.ToString(),
                        request.Encoding?.ToString() ?? "LittleEndian",
                        request.Value,
                        pattern.Length,
                        ToHex(ReadBounded(path, size, contextStart, contextLength))));
                    if (results.Count >= BinaryDiffOptions.MaximumSearchResults)
                    {
                        truncated = true;
                        return;
                    }
                }
            }

            if (read == 0)
            {
                break;
            }

            carried = Math.Min(pattern.Length - 1, total);
            Buffer.BlockCopy(buffer, total - carried, buffer, 0, carried);
            bufferBaseOffset += total - carried;
        }
    }

    private static FileStream OpenRead(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        BufferSize,
        FileOptions.SequentialScan);

    private static void ReadExactly(Stream stream, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(buffer, offset, count - offset);
            if (read == 0)
            {
                throw new EndOfStreamException("Input file changed or ended during read.");
            }

            offset += read;
        }
    }

    private static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).Replace("-", string.Empty, StringComparison.Ordinal)
        .Chunk(2)
        .Select(chunk => new string(chunk))
        .Aggregate(string.Empty, (current, next) => current.Length == 0 ? next : current + " " + next);

    private static string FormatOffset(long offset) => $"0x{offset:X16}";

    private static bool IsPrintableAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value is < 0x20 or > 0x7E)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsExpectedIoException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;

    private sealed record FileSnapshot(long Size, DateTime LastWriteUtc);

    private sealed record RawRange(long Start, long End, long DifferentCount);
}
