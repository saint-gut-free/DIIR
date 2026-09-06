using System.Text;
using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Persistence.Content;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class ContentPackagePersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-content-{Guid.NewGuid():N}");
    private readonly ContentPackageJsonSerializer serializer;
    private readonly ContentPackageFileStore store;

    public ContentPackagePersistenceTests()
    {
        Directory.CreateDirectory(directory);
        serializer = new ContentPackageJsonSerializer(new ContentPackageValidationService());
        store = new ContentPackageFileStore(serializer);
    }

    [Fact]
    public void Serialize_ValidPackage_IsDeterministicAndSorted()
    {
        ContentPackageDefinition left = CreatePackage();
        ContentPackageDefinition right = left with
        {
            Terrains = left.Terrains.Reverse().ToArray(),
            ObjectArchetypes = left.ObjectArchetypes.Reverse().ToArray(),
        };

        byte[] leftData = serializer.Serialize(left).Data!;
        byte[] rightData = serializer.Serialize(right).Data!;
        string json = Encoding.UTF8.GetString(leftData);

        Assert.Equal(leftData, rightData);
        Assert.True(json.IndexOf("forest", StringComparison.Ordinal) < json.IndexOf("plain", StringComparison.Ordinal));
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_InvalidPackage_ReturnsValidationFailure()
    {
        ContentPackageSerializationResult result = serializer.Serialize(CreatePackage() with { Id = string.Empty });

        Assert.False(result.IsSuccess);
        Assert.Equal(ContentPackagePersistenceErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsValues()
    {
        ContentPackageDefinition expected = CreatePackage();
        byte[] data = serializer.Serialize(expected).Data!;

        ContentPackageDeserializationResult result = serializer.Deserialize(data);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.Id, result.Package!.Id);
        Assert.Equal(expected.DisplayName, result.Package.DisplayName);
        Assert.Equal(
            expected.Terrains.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray(),
            result.Package.Terrains.ToArray());
        Assert.Equal(
            expected.ObjectArchetypes.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray(),
            result.Package.ObjectArchetypes.ToArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not-json")]
    [InlineData("{\"formatVersion\":1,\"unknown\":true}")]
    public void Deserialize_InvalidJson_ReturnsStructuredFailure(string json)
    {
        ContentPackageDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ContentPackagePersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    [Fact]
    public void Deserialize_InvalidSemantics_ReturnsValidationIssues()
    {
        const string json = """
            {
              "formatVersion": 1,
              "id": "",
              "displayName": "Synthetic",
              "terrains": [],
              "objectArchetypes": []
            }
            """;

        ContentPackageDeserializationResult result = serializer.Deserialize(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(ContentPackagePersistenceErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == ContentPackageValidationCode.PackageIdMissing);
    }

    [Fact]
    public void FileStore_SaveAndLoad_RoundTrips()
    {
        string path = Path.Combine(directory, "package.json");

        ContentPackageSaveResult save = store.Save(path, CreatePackage());
        ContentPackageLoadResult load = store.Load(path);

        Assert.True(save.IsSuccess);
        Assert.True(load.IsSuccess);
        Assert.Equal("synthetic", load.Package!.Id);
    }

    [Fact]
    public void FileStore_InvalidPackage_DoesNotCreateFile()
    {
        string path = Path.Combine(directory, "invalid.json");

        ContentPackageSaveResult result = store.Save(path, CreatePackage() with { Id = string.Empty });

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void FileStore_MissingInput_ReturnsFileNotFound()
    {
        ContentPackageLoadResult result = store.Load(Path.Combine(directory, "missing.json"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ContentPackagePersistenceErrorCode.FileNotFound, result.ErrorCode);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private static ContentPackageDefinition CreatePackage() =>
        new(
            ContentPackageFormatV1.Version,
            "synthetic",
            "Synthetic package",
            [
                new TerrainContentDefinition("plain", "Plain"),
                new TerrainContentDefinition("forest", "Forest"),
            ],
            [
                new ObjectArchetypeDefinition("z-object", "Z object"),
                new ObjectArchetypeDefinition("a-object", "A object"),
            ]);
}
