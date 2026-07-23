using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DisciplesRemaster.BinaryDiff;

namespace DisciplesRemaster.Tools.Tests;

public sealed class BinaryComparisonServiceTests
{
    private readonly BinaryComparisonService service = new();

    [Fact]
    public void Compare_IdenticalEmptyFiles_AreEqual()
    {
        using var fixture = new BinaryFixture();
        string fileA = fixture.Write("empty-a.bin", []);
        string fileB = fixture.Write("empty-b.bin", []);

        BinaryDiffReport report = Compare(fileA, fileB);

        Assert.True(report.Comparison.AreEqual);
        Assert.Equal(0, report.Comparison.ComparedByteCount);
        Assert.Empty(report.ChangedRanges);
    }

    [Fact]
    public void Compare_IdenticalNonEmptyFilesWithDifferentNames_AreEqual()
    {
        using var fixture = new BinaryFixture();
        byte[] content = [1, 2, 3, 4];

        BinaryDiffReport report = Compare(fixture.Write("first.bin", content), fixture.Write("second.dat", content));

        Assert.True(report.Comparison.AreEqual);
        Assert.Equal(report.FileA.Sha256, report.FileB.Sha256);
        Assert.NotEqual(report.FileA.FileName, report.FileB.FileName);
    }

    [Fact]
    public void Compare_OneDifferentByte_CreatesOneRange()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", [0, 1, 2, 3, 4]),
            fixture.Write("b.bin", [0, 1, 9, 3, 4]));

        BinaryChangedRange range = Assert.Single(report.ChangedRanges);
        Assert.Equal(2, range.StartOffset);
        Assert.Equal(2, range.EndOffset);
        Assert.Equal(1, report.Comparison.DifferentByteCountAtSameOffsets);
    }

    [Fact]
    public void Compare_SeparateDifferences_CreateSeparateRanges()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", [0, 1, 2, 3, 4, 5]),
            fixture.Write("b.bin", [9, 1, 2, 8, 4, 5]));

        Assert.Equal(2, report.ChangedRanges.Count);
        Assert.Equal([0L, 3L], report.ChangedRanges.Select(range => range.StartOffset));
    }

    [Fact]
    public void Compare_AdjacentDifferences_AreMerged()
    {
        using var fixture = new BinaryFixture();

        BinaryChangedRange range = Assert.Single(Compare(
            fixture.Write("a.bin", [0, 1, 2, 3, 4]),
            fixture.Write("b.bin", [0, 9, 8, 3, 4])).ChangedRanges);

        Assert.Equal(1, range.StartOffset);
        Assert.Equal(2, range.EndOffset);
        Assert.Equal(2, range.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Compare_DifferenceAtBoundary_IsReported(int offset)
    {
        using var fixture = new BinaryFixture();
        byte[] a = [1, 1, 1, 1, 1];
        byte[] b = (byte[])a.Clone();
        b[offset] = 2;

        BinaryChangedRange range = Assert.Single(Compare(fixture.Write("a.bin", a), fixture.Write("b.bin", b)).ChangedRanges);

        Assert.Equal(offset, range.StartOffset);
    }

    [Fact]
    public void Compare_AppendedTail_ReportsSizeDifferenceAndTailRange()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", [1, 2, 3]),
            fixture.Write("b.bin", [1, 2, 3, 4, 5]));

        Assert.False(report.Comparison.SameSize);
        Assert.Equal(2, report.Comparison.SizeDifference);
        Assert.Equal(3, report.Comparison.LongestCommonPrefix);
        Assert.Contains(report.ChangedRanges, range => range.StartOffset == 3 && range.EndOffset == 4);
        Assert.Contains(report.Hypotheses, hypothesis => hypothesis.Contains("hypothesis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Compare_TruncatedFile_ReportsNegativeSizeDifference()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", [1, 2, 3, 4, 5]),
            fixture.Write("b.bin", [1, 2, 3]));

        Assert.Equal(-2, report.Comparison.SizeDifference);
        Assert.Contains(report.ChangedRanges, range => range.StartOffset == 3);
    }

    [Fact]
    public void Compare_ComputesCommonPrefixAndSuffix()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", [1, 2, 3, 4, 5, 6]),
            fixture.Write("b.bin", [1, 2, 9, 8, 5, 6]));

        Assert.Equal(2, report.Comparison.LongestCommonPrefix);
        Assert.Equal(2, report.Comparison.LongestCommonSuffix);
    }

    [Fact]
    public void Compare_CompletelyDifferentFiles_HaveNoCommonPrefixOrSuffix()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", [1, 1, 1, 1]),
            fixture.Write("b.bin", [2, 2, 2, 2]));

        Assert.Equal(0, report.Comparison.LongestCommonPrefix);
        Assert.Equal(0, report.Comparison.LongestCommonSuffix);
        Assert.Equal(4, report.Comparison.DifferentByteCountAtSameOffsets);
    }

    [Fact]
    public void Compare_ComputesCorrectLowercaseSha256()
    {
        using var fixture = new BinaryFixture();
        byte[] content = [4, 3, 2, 1];
        string expected = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        BinaryDiffReport report = Compare(fixture.Write("a.bin", content), fixture.Write("b.bin", [4, 3, 2, 0]));

        Assert.Equal(expected, report.FileA.Sha256);
    }

    [Fact]
    public void Compare_LargeFiles_UsesCompleteContent()
    {
        using var fixture = new BinaryFixture();
        byte[] a = new byte[2 * 1024 * 1024];
        byte[] b = new byte[a.Length];
        Random.Shared.NextBytes(a);
        Array.Copy(a, b, a.Length);
        b[^1] ^= 0xFF;

        BinaryDiffReport report = Compare(fixture.Write("a.bin", a), fixture.Write("b.bin", b));

        Assert.Equal(a.Length, report.Comparison.ComparedByteCount);
        Assert.Equal(a.Length - 1, report.Comparison.LongestCommonPrefix);
        Assert.Equal(1, report.Comparison.DifferentByteCountAtSameOffsets);
    }

    [Theory]
    [InlineData(BinarySearchValueType.Int16, "-123", 2)]
    [InlineData(BinarySearchValueType.UInt16, "65000", 2)]
    [InlineData(BinarySearchValueType.Int32, "-1234567", 4)]
    [InlineData(BinarySearchValueType.UInt32, "4000000000", 4)]
    public void Compare_NumericSearchesFindLittleEndianValues(BinarySearchValueType type, string value, int length)
    {
        using var fixture = new BinaryFixture();
        byte[] pattern = NumericPattern(type, value);
        byte[] content = [0xAA, 0xBB, .. pattern, 0xCC];
        var search = new BinarySearchRequest(type, value, null, pattern);

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", content),
            fixture.Write("b.bin", content),
            new BinaryDiffOptions(4, 200, [search]));

        Assert.Contains(report.SearchResults, result => result.Offset == 2 && result.Length == length && result.Type == type.ToString());
    }

    [Theory]
    [InlineData(BinaryStringEncoding.Ascii)]
    [InlineData(BinaryStringEncoding.Utf8)]
    [InlineData(BinaryStringEncoding.Utf16Le)]
    public void Compare_StringSearchFindsRequestedEncoding(BinaryStringEncoding encoding)
    {
        using var fixture = new BinaryFixture();
        byte[] pattern = encoding switch
        {
            BinaryStringEncoding.Ascii => Encoding.ASCII.GetBytes("Test"),
            BinaryStringEncoding.Utf8 => Encoding.UTF8.GetBytes("Тест"),
            BinaryStringEncoding.Utf16Le => Encoding.Unicode.GetBytes("Test"),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding)),
        };
        byte[] content = [0, 0, .. pattern, 0];
        var search = new BinarySearchRequest(BinarySearchValueType.String, "Test", encoding, pattern);

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", content),
            fixture.Write("b.bin", content),
            new BinaryDiffOptions(4, 200, [search]));

        Assert.Contains(report.SearchResults, result => result.Offset == 2 && result.Encoding == encoding.ToString());
    }

    [Fact]
    public void Compare_SearchReportsMultipleMatches()
    {
        using var fixture = new BinaryFixture();
        var search = new BinarySearchRequest(BinarySearchValueType.String, "AB", BinaryStringEncoding.Ascii, Encoding.ASCII.GetBytes("AB"));
        byte[] content = Encoding.ASCII.GetBytes("ABxxAByyAB");

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", content),
            fixture.Write("b.bin", content),
            new BinaryDiffOptions(2, 200, [search]));

        Assert.Equal([0L, 0L, 4L, 4L, 8L, 8L], report.SearchResults.Select(result => result.Offset));
    }

    [Fact]
    public void Compare_SearchResultsAreHardLimited()
    {
        using var fixture = new BinaryFixture();
        byte[] content = Enumerable.Repeat((byte)0x41, 1200).ToArray();
        var search = new BinarySearchRequest(BinarySearchValueType.String, "A", BinaryStringEncoding.Ascii, [0x41]);

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", content),
            fixture.Write("b.bin", content),
            new BinaryDiffOptions(0, 200, [search]));

        Assert.Equal(BinaryDiffOptions.MaximumSearchResults, report.SearchResults.Count);
        Assert.Contains(report.Warnings, warning => warning.Code == BinaryDiffWarningCode.SearchResultsTruncated);
    }

    [Fact]
    public void Compare_ChangedRangeCountIsLimitedButFullyCounted()
    {
        using var fixture = new BinaryFixture();
        byte[] a = new byte[20];
        byte[] b = new byte[20];
        for (int index = 0; index < b.Length; index += 2) b[index] = 1;

        BinaryDiffReport report = Compare(
            fixture.Write("a.bin", a),
            fixture.Write("b.bin", b),
            new BinaryDiffOptions(0, 2, []));

        Assert.Equal(10, report.Comparison.DetectedChangedRangeCount);
        Assert.Equal(2, report.ChangedRanges.Count);
        Assert.Contains(report.Warnings, warning => warning.Code == BinaryDiffWarningCode.ReportTruncated);
    }

    [Fact]
    public void Compare_LargeChangedRangeHasBoundedDump()
    {
        using var fixture = new BinaryFixture();
        byte[] a = new byte[200];
        byte[] b = Enumerable.Repeat((byte)1, 200).ToArray();

        BinaryChangedRange range = Assert.Single(Compare(fixture.Write("a.bin", a), fixture.Write("b.bin", b)).ChangedRanges);

        Assert.True(range.IsTruncated);
        Assert.Equal(136, range.OmittedByteCount);
        Assert.Contains("bytes omitted", range.FileABytes, StringComparison.Ordinal);
        Assert.True(range.FileABytes.Length < 300);
    }

    [Fact]
    public void Compare_DifferentFilesSameSize_AreNotEqual()
    {
        using var fixture = new BinaryFixture();

        BinaryDiffReport report = Compare(fixture.Write("a.bin", [1, 2]), fixture.Write("b.bin", [2, 1]));

        Assert.True(report.Comparison.SameSize);
        Assert.False(report.Comparison.AreEqual);
    }

    [Fact]
    public void Compare_DoesNotModifyInputFiles()
    {
        using var fixture = new BinaryFixture();
        string fileA = fixture.Write("a.bin", [1, 2, 3]);
        string fileB = fixture.Write("b.bin", [1, 2, 4]);
        FileState beforeA = State(fileA);
        FileState beforeB = State(fileB);

        _ = Compare(fileA, fileB);

        Assert.Equal(beforeA, State(fileA));
        Assert.Equal(beforeB, State(fileB));
    }

    [Fact]
    public void Compare_MissingInputThrowsSafeInputException()
    {
        using var fixture = new BinaryFixture();

        BinaryInputException exception = Assert.Throws<BinaryInputException>(() =>
            Compare(fixture.GetPath("missing.bin"), fixture.Write("b.bin", [1])));

        Assert.DoesNotContain(fixture.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private BinaryDiffReport Compare(string fileA, string fileB, BinaryDiffOptions? options = null) =>
        service.Compare(fileA, fileB, "File A", "File B", options ?? new BinaryDiffOptions(16, 200, []));

    private static byte[] NumericPattern(BinarySearchValueType type, string value)
    {
        byte[] pattern = new byte[type is BinarySearchValueType.Int16 or BinarySearchValueType.UInt16 ? 2 : 4];
        switch (type)
        {
            case BinarySearchValueType.Int16:
                BinaryPrimitives.WriteInt16LittleEndian(pattern, short.Parse(value));
                break;
            case BinarySearchValueType.UInt16:
                BinaryPrimitives.WriteUInt16LittleEndian(pattern, ushort.Parse(value));
                break;
            case BinarySearchValueType.Int32:
                BinaryPrimitives.WriteInt32LittleEndian(pattern, int.Parse(value));
                break;
            case BinarySearchValueType.UInt32:
                BinaryPrimitives.WriteUInt32LittleEndian(pattern, uint.Parse(value));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        return pattern;
    }

    private static FileState State(string path) =>
        new(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            File.GetLastWriteTimeUtc(path),
            File.GetAttributes(path));

    private sealed record FileState(string ContentHash, DateTime LastWriteUtc, FileAttributes Attributes);

    private sealed class BinaryFixture : IDisposable
    {
        public BinaryFixture()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DisciplesRemaster.BinaryDiff.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string GetPath(string name) => System.IO.Path.Combine(Path, name);

        public string Write(string name, byte[] content)
        {
            string path = GetPath(name);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
