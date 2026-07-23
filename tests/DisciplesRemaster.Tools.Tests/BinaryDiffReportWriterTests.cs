using DisciplesRemaster.BinaryDiff;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.Tools.Tests;

public sealed class BinaryDiffReportWriterTests
{
    [Fact]
    public void SerializeJson_IsDeterministicAndContainsNoAbsolutePathsOrPatterns()
    {
        BinaryDiffReport report = BinaryDiffTestData.CreateReport();
        const string privatePath = "C:\\Users\\PrivateUser\\baseline.bin";

        string first = BinaryDiffReportWriter.SerializeJson(report);
        string second = BinaryDiffReportWriter.SerializeJson(report);

        Assert.Equal(first, second);
        Assert.DoesNotContain(privatePath, first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pattern", first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"reportFormatVersion\": \"1.0\"", first, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTextReport_IsDeterministicBoundedAndContainsObservationDisclaimer()
    {
        BinaryDiffReport report = BinaryDiffTestData.CreateReport();

        string first = BinaryDiffReportWriter.CreateTextReport(report);
        string second = BinaryDiffReportWriter.CreateTextReport(report);

        Assert.Equal(first, second);
        Assert.Contains("not a confirmed file-format field", first, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteReports_RespectsRequestedFormat()
    {
        using var fixture = new ReportDirectory();
        var writer = new BinaryDiffReportWriter();

        BinaryDiffReportPaths json = writer.WriteReports(BinaryDiffTestData.CreateReport(), fixture.JsonPath, BinaryDiffOutputFormat.Json);
        BinaryDiffReportPaths text = writer.WriteReports(BinaryDiffTestData.CreateReport(), fixture.TextPath, BinaryDiffOutputFormat.Text);

        Assert.NotNull(json.JsonPath);
        Assert.Null(json.TextPath);
        Assert.Null(text.JsonPath);
        Assert.NotNull(text.TextPath);
        Assert.Single(Directory.GetFiles(fixture.JsonPath));
        Assert.Single(Directory.GetFiles(fixture.TextPath));
    }

    [Fact]
    public void ValidateOutputPath_RejectsInputFilePath()
    {
        using var fixture = new ReportDirectory();
        string fileA = fixture.Write("a.bin", [1]);
        string fileB = fixture.Write("b.bin", [2]);
        var writer = new BinaryDiffReportWriter();

        BinaryDiffOutputValidationResult result = writer.ValidateOutputPath(fileA, fileA, fileB, null);

        Assert.False(result.IsValid);
        Assert.Equal(BinaryDiffWarningCode.OutputConflictsWithInput, result.ErrorCode);
    }

    [Fact]
    public void ValidateOutputPath_RejectsOutputInsideConfiguredOriginalRoot()
    {
        using var fixture = new ReportDirectory();
        string source = Directory.CreateDirectory(Path.Combine(fixture.Root, "source")).FullName;
        string fileA = fixture.Write("a.bin", [1]);
        string fileB = fixture.Write("b.bin", [2]);
        var location = new OriginalGameLocation(source, "<configured-original-game-directory>/source");
        var writer = new BinaryDiffReportWriter();

        BinaryDiffOutputValidationResult result = writer.ValidateOutputPath(Path.Combine(source, "reports"), fileA, fileB, location);

        Assert.False(result.IsValid);
        Assert.Equal(BinaryDiffWarningCode.OutputInsideOriginalGameDirectory, result.ErrorCode);
        Assert.False(Directory.Exists(Path.Combine(source, "reports")));
    }

    [Fact]
    public void ValidateOutputPath_RejectsSymlinkResolvingInsideOriginalRoot()
    {
        using var fixture = new ReportDirectory();
        string source = Directory.CreateDirectory(Path.Combine(fixture.Root, "source")).FullName;
        string link = Path.Combine(fixture.Root, "source-link");
        try
        {
            Directory.CreateSymbolicLink(link, source);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            return;
        }

        string fileA = fixture.Write("a.bin", [1]);
        string fileB = fixture.Write("b.bin", [2]);
        var writer = new BinaryDiffReportWriter();

        BinaryDiffOutputValidationResult result = writer.ValidateOutputPath(
            Path.Combine(link, "reports"),
            fileA,
            fileB,
            new OriginalGameLocation(source, "<configured-original-game-directory>/source"));

        Assert.False(result.IsValid);
    }

    private sealed class ReportDirectory : IDisposable
    {
        public ReportDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), "DisciplesRemaster.BinaryDiff.Report.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string JsonPath => Path.Combine(Root, "json-output");

        public string TextPath => Path.Combine(Root, "text-output");

        public string Write(string name, byte[] content)
        {
            string path = Path.Combine(Root, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
