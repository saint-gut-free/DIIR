using System.Security.Cryptography;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.OriginalGame.Tests;

public sealed class OriginalGameInventoryServiceTests
{
    private readonly OriginalGameInventoryService service = new();

    [Fact]
    public void CreateInventory_EmptyDirectory_ReturnsEmptyInventory()
    {
        using var fixture = new InventoryDirectory();

        OriginalGameInventory inventory = CreateInventory(fixture);

        Assert.Empty(inventory.Files);
        Assert.Empty(inventory.Warnings);
        Assert.Equal(0, inventory.Summary.TotalSizeBytes);
    }

    [Fact]
    public void CreateInventory_OneFile_CollectsMetadata()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("one.txt", "hello");

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.Equal("one.txt", entry.RelativePath);
        Assert.Equal("one.txt", entry.FileName);
        Assert.Equal(".txt", entry.Extension);
        Assert.Equal(5, entry.SizeBytes);
        Assert.True(entry.IsReadSuccessful);
    }

    [Fact]
    public void CreateInventory_NestedDirectories_UsesNormalizedRelativePathsAndDepth()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile(Path.Combine("first", "second", "nested.bin"), [0x01]);

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.Equal("first/second/nested.bin", entry.RelativePath);
        Assert.Equal(3, entry.Depth);
        Assert.DoesNotContain('\\', entry.RelativePath);
    }

    [Fact]
    public void CreateInventory_AlwaysReturnsOrdinalPathOrder()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("z.txt", "z");
        fixture.WriteFile("A.txt", "a");
        fixture.WriteFile(Path.Combine("middle", "b.txt"), "b");

        string[] paths = CreateInventory(fixture).Files.Select(entry => entry.RelativePath).ToArray();

        Assert.Equal(["A.txt", "middle/b.txt", "z.txt"], paths);
    }

    [Fact]
    public void CreateInventory_FileWithoutExtension_HasEmptyNormalizedExtension()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("README", "text");

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.Equal(string.Empty, entry.Extension);
    }

    [Fact]
    public void CreateInventory_MixedCaseExtension_NormalizesToLowercase()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("IMAGE.PnG", [0x01]);

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.Equal(".png", entry.Extension);
        Assert.Equal(OriginalGameFileCategory.Image, entry.Category);
    }

    [Fact]
    public void CreateInventory_UnknownExtensionAndSignature_IsStillHashedAndInventoried()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("opaque.unrecognized", [0x00, 0x01, 0x02, 0x03]);

        OriginalGameInventory inventory = CreateInventory(fixture);
        OriginalGameInventoryEntry entry = Assert.Single(inventory.Files);

        Assert.True(entry.IsReadSuccessful);
        Assert.Equal(".unrecognized", entry.Extension);
        Assert.Equal(4, entry.SizeBytes);
        Assert.Equal(64, entry.Sha256!.Length);
        Assert.Equal(OriginalGameFileSignature.Unknown, entry.Signature);
        Assert.Equal(OriginalGameFileCategory.Unknown, entry.Category);
        Assert.Contains(inventory.Summary.Extensions, count => count.Name == ".unrecognized" && count.Count == 1);
    }

    [Fact]
    public void CreateInventory_IdenticalFiles_CreatesDuplicateGroup()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("copy-a.bin", [1, 2, 3, 4]);
        fixture.WriteFile("copy-b.bin", [1, 2, 3, 4]);

        OriginalGameInventoryDuplicateGroup group = Assert.Single(CreateInventory(fixture).Summary.DuplicateGroups);

        Assert.Equal(["copy-a.bin", "copy-b.bin"], group.RelativePaths);
        Assert.Equal(4, group.SizeBytes);
    }

    [Fact]
    public void CreateInventory_DifferentFilesWithSameSize_AreNotDuplicates()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("a.bin", [1, 2, 3]);
        fixture.WriteFile("b.bin", [3, 2, 1]);

        Assert.Empty(CreateInventory(fixture).Summary.DuplicateGroups);
    }

    [Fact]
    public void CreateInventory_ComputesLowercaseSha256()
    {
        using var fixture = new InventoryDirectory();
        byte[] content = [1, 3, 3, 7];
        fixture.WriteFile("hash.bin", content);
        string expected = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.Equal(expected, entry.Sha256);
        Assert.Equal(entry.Sha256, entry.Sha256!.ToLowerInvariant());
    }

    [Fact]
    public void CreateInventory_LargeFile_IsHashedSuccessfully()
    {
        using var fixture = new InventoryDirectory();
        byte[] block = Enumerable.Range(0, 8192).Select(index => (byte)(index % 251)).ToArray();
        string path = fixture.GetPath("large.bin");
        using (FileStream stream = File.Create(path))
        {
            for (int index = 0; index < 512; index++)
            {
                stream.Write(block);
            }
        }

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.True(entry.IsReadSuccessful);
        Assert.Equal(4L * 1024 * 1024, entry.SizeBytes);
        Assert.Equal(64, entry.Sha256!.Length);
    }

    [Theory]
    [MemberData(nameof(SignatureCases))]
    public void CreateInventory_DetectsSupportedSignatures(byte[] content, OriginalGameFileSignature expected)
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile("signature.bin", content);

        OriginalGameInventoryEntry entry = Assert.Single(CreateInventory(fixture).Files);

        Assert.Equal(expected, entry.Signature);
    }

    public static TheoryData<byte[], OriginalGameFileSignature> SignatureCases => new()
    {
        { [0x4D, 0x5A, 0x00], OriginalGameFileSignature.WindowsPortableExecutable },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], OriginalGameFileSignature.Png },
        { [0x50, 0x4B, 0x03, 0x04], OriginalGameFileSignature.Zip },
        { [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45], OriginalGameFileSignature.Wave },
        { [0x00, 0x01, 0x02, 0x03], OriginalGameFileSignature.Unknown },
    };

    [Fact]
    public void CreateInventory_HiddenFile_IsSkippedByDefault()
    {
        using var fixture = new InventoryDirectory();
        string path = fixture.WriteFile("hidden.txt", "hidden");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        if ((File.GetAttributes(path) & FileAttributes.Hidden) == 0)
        {
            return;
        }

        OriginalGameInventory inventory = CreateInventory(fixture);

        Assert.Empty(inventory.Files);
        Assert.Contains(inventory.Warnings, warning => warning.Code == OriginalGameInventoryWarningCode.HiddenItemSkipped);
    }

    [Fact]
    public void CreateInventory_IncludeHidden_IncludesHiddenFile()
    {
        using var fixture = new InventoryDirectory();
        string path = fixture.WriteFile("hidden.txt", "hidden");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        if ((File.GetAttributes(path) & FileAttributes.Hidden) == 0)
        {
            return;
        }

        OriginalGameInventory inventory = CreateInventory(fixture, new OriginalGameInventoryOptions(IncludeHidden: true));

        Assert.True(Assert.Single(inventory.Files).IsHidden);
    }

    [Fact]
    public void CreateInventory_MaximumDepth_SkipsDeeperElementsWithWarning()
    {
        using var fixture = new InventoryDirectory();
        fixture.WriteFile(Path.Combine("level-one", "too-deep.txt"), "deep");

        OriginalGameInventory inventory = CreateInventory(fixture, new OriginalGameInventoryOptions(MaxDepth: 1));

        Assert.Empty(inventory.Files);
        Assert.Contains(inventory.Warnings, warning => warning.Code == OriginalGameInventoryWarningCode.MaximumDepthExceeded);
    }

    [Fact]
    public void CreateInventory_SymbolicLink_IsSkippedWithoutFollowingTarget()
    {
        using var fixture = new InventoryDirectory();
        string target = fixture.WriteFile("target.txt", "target");
        string link = fixture.GetPath("link.txt");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            return;
        }

        OriginalGameInventory inventory = CreateInventory(fixture);

        Assert.DoesNotContain(inventory.Files, entry => entry.RelativePath == "link.txt");
        Assert.Contains(inventory.Warnings, warning =>
            warning.Code == OriginalGameInventoryWarningCode.ReparsePointSkipped && warning.RelativePath == "link.txt");
    }

    [Fact]
    public void CreateInventory_DoesNotModifySourceFiles()
    {
        using var fixture = new InventoryDirectory();
        byte[] content = [9, 8, 7, 6];
        string path = fixture.WriteFile("unchanged.bin", content);
        DateTime timestamp = File.GetLastWriteTimeUtc(path);
        FileAttributes attributes = File.GetAttributes(path);

        _ = CreateInventory(fixture);

        Assert.Equal(content, File.ReadAllBytes(path));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
        Assert.Equal(attributes, File.GetAttributes(path));
    }

    [Fact]
    public void CreateInventory_LockedFile_ReturnsWarningWhenPlatformDeniesSharedRead()
    {
        using var fixture = new InventoryDirectory();
        string path = fixture.WriteFile("locked.bin", [1, 2, 3]);
        using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        OriginalGameInventory inventory = CreateInventory(fixture);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        OriginalGameInventoryEntry entry = Assert.Single(inventory.Files);
        Assert.False(entry.IsReadSuccessful);
        Assert.Contains(inventory.Warnings, warning =>
            warning.Code is OriginalGameInventoryWarningCode.FileInaccessible or OriginalGameInventoryWarningCode.HashingFailed);
    }

    private OriginalGameInventory CreateInventory(
        InventoryDirectory fixture,
        OriginalGameInventoryOptions? options = null) =>
        service.CreateInventory(
            new OriginalGameLocation(fixture.Path, "<configured-original-game-directory>/synthetic"),
            options ?? new OriginalGameInventoryOptions());

    private sealed class InventoryDirectory : IDisposable
    {
        public InventoryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DisciplesRemaster.Inventory.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string GetPath(string relativePath)
        {
            string path = System.IO.Path.Combine(Path, relativePath);
            string? directory = System.IO.Path.GetDirectoryName(path);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            return path;
        }

        public string WriteFile(string relativePath, string content)
        {
            string path = GetPath(relativePath);
            File.WriteAllText(path, content);
            return path;
        }

        public string WriteFile(string relativePath, byte[] content)
        {
            string path = GetPath(relativePath);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(Path, recursive: true);
        }
    }
}
