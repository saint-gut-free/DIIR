using DisciplesRemaster.FileInventory;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.Tools.Tests;

public sealed class OriginalGameInventoryReportWriterTests
{
    [Fact]
    public void SerializeJson_IsStableAndDoesNotContainAbsoluteSourcePath()
    {
        OriginalGameInventory inventory = CreateInventory();
        const string privatePath = "C:\\Users\\PrivateUser\\OriginalGame";

        string first = OriginalGameInventoryReportWriter.SerializeJson(inventory);
        string second = OriginalGameInventoryReportWriter.SerializeJson(inventory);

        Assert.Equal(first, second);
        Assert.DoesNotContain(privatePath, first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"reportFormatVersion\": \"1.0\"", first, StringComparison.Ordinal);
        Assert.DoesNotContain("fileContent", first, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateMarkdown_IsStableUsesRelativePathsAndIncludesDuplicateGroup()
    {
        OriginalGameInventory inventory = CreateInventory();
        const string privatePath = "C:\\Users\\PrivateUser\\OriginalGame";

        string first = OriginalGameInventoryReportWriter.CreateMarkdown(inventory);
        string second = OriginalGameInventoryReportWriter.CreateMarkdown(inventory);

        Assert.Equal(first, second);
        Assert.DoesNotContain(privatePath, first, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nested/a.bin", first, StringComparison.Ordinal);
        Assert.Contains("nested/b.bin", first, StringComparison.Ordinal);
        Assert.Contains("Duplicate groups", first, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateOutputPath_RejectsSourceRootAndDescendantWithoutCreatingThem()
    {
        using var source = new TemporaryDirectory();
        var writer = new OriginalGameInventoryReportWriter();
        var location = new OriginalGameLocation(source.Path, "<configured-original-game-directory>/synthetic");
        string descendant = Path.Combine(source.Path, "reports", "inventory");

        OriginalGameInventoryOutputValidationResult rootResult = writer.ValidateOutputPath(source.Path, location);
        OriginalGameInventoryOutputValidationResult descendantResult = writer.ValidateOutputPath(descendant, location);

        Assert.False(rootResult.IsValid);
        Assert.False(descendantResult.IsValid);
        Assert.Equal(OriginalGameInventoryWarningCode.OutputInsideOriginalGameDirectory, descendantResult.ErrorCode);
        Assert.False(Directory.Exists(descendant));
    }

    [Fact]
    public void ValidateOutputPath_RejectsExistingSymlinkThatResolvesInsideSource()
    {
        using var source = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        string link = Path.Combine(outside.Path, "source-link");
        try
        {
            Directory.CreateSymbolicLink(link, source.Path);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            return;
        }

        var writer = new OriginalGameInventoryReportWriter();
        var location = new OriginalGameLocation(source.Path, "<configured-original-game-directory>/synthetic");

        OriginalGameInventoryOutputValidationResult result = writer.ValidateOutputPath(Path.Combine(link, "reports"), location);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void WriteReports_CreatesOnlyTwoFilesInsideOutputDirectory()
    {
        using var source = new TemporaryDirectory();
        using var outputParent = new TemporaryDirectory();
        string output = Path.Combine(outputParent.Path, "reports");
        var writer = new OriginalGameInventoryReportWriter();

        OriginalGameInventoryReportPaths paths = writer.WriteReports(CreateInventory(), output);

        Assert.True(File.Exists(paths.JsonPath));
        Assert.True(File.Exists(paths.MarkdownPath));
        Assert.Equal(2, Directory.GetFiles(output).Length);
        Assert.Empty(Directory.GetFileSystemEntries(source.Path));
    }

    private static OriginalGameInventory CreateInventory()
    {
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        OriginalGameInventoryEntry[] entries =
        [
            Entry("nested/a.bin", hash),
            Entry("nested/b.bin", hash),
        ];
        var summary = new OriginalGameInventorySummary(
            2,
            0,
            8,
            [new OriginalGameInventoryCount(".bin", 2)],
            [new OriginalGameInventoryCount("Data", 2)],
            [new OriginalGameInventoryDuplicateGroup(hash, 4, ["nested/a.bin", "nested/b.bin"])]);
        return new OriginalGameInventory(
            OriginalGameInventory.CurrentReportFormatVersion,
            "<configured-original-game-directory>/OriginalGame",
            new OriginalGameInventoryOptions(),
            entries,
            [],
            summary);
    }

    private static OriginalGameInventoryEntry Entry(string path, string hash) =>
        new(
            path,
            Path.GetFileName(path),
            ".bin",
            4,
            hash,
            DateTime.UnixEpoch,
            2,
            FileAttributes.Normal,
            OriginalGameFileCategory.Data,
            OriginalGameFileSignature.Unknown,
            IsHidden: false,
            IsReparsePoint: false,
            IsReadSuccessful: true,
            Warning: null);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DisciplesRemaster.Report.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
