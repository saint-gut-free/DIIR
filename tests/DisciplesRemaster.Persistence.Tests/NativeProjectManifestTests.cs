using System.Text;
using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Projects;
using DisciplesRemaster.Persistence.Scenarios;
using DisciplesRemaster.Persistence.Sessions;

namespace DisciplesRemaster.Persistence.Tests;

public sealed class NativeProjectManifestTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"d2r-project-{Guid.NewGuid():N}");
    private readonly NativeProjectManifestValidationService validation = new();

    public NativeProjectManifestTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public void Validate_PortableManifest_IsValid()
    {
        NativeProjectManifestValidationResult result = validation.Validate(CreateManifest());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_UnsafeAndDuplicatePaths_ReturnsDeterministicIssues()
    {
        NativeProjectManifest manifest = CreateManifest() with
        {
            Scenario = Path.Combine(directory, "scenario.json"),
            ContentPackages = ["content/../package.json", "content/package.json", "content\\package.json"],
            Session = "./session.json",
        };

        NativeProjectManifestValidationResult result = validation.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == NativeProjectManifestValidationCode.PathMustBeRelative && issue.PropertyPath == "scenario");
        Assert.Contains(result.Issues, issue =>
            issue.Code == NativeProjectManifestValidationCode.PathEscapesProjectDirectory && issue.PropertyPath == "contentPackages[0]");
        Assert.Contains(result.Issues, issue =>
            issue.Code == NativeProjectManifestValidationCode.DuplicateContentPackagePath);
        Assert.Contains(result.Issues, issue =>
            issue.Code == NativeProjectManifestValidationCode.PathEscapesProjectDirectory && issue.PropertyPath == "session");
        Assert.Equal(
            result.Issues.OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal).ThenBy(issue => issue.Code),
            result.Issues);
    }

    [Fact]
    public void Serialize_IsDeterministicPortableAndSorted()
    {
        var serializer = new NativeProjectManifestJsonSerializer(validation);
        NativeProjectManifest manifest = CreateManifest() with
        {
            Scenario = "scenarios\\scenario.json",
            ContentPackages = ["content/z.json", "content\\a.json"],
        };

        NativeProjectManifestSerializationResult first = serializer.Serialize(manifest);
        NativeProjectManifestSerializationResult second = serializer.Serialize(manifest);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Data, second.Data);
        string json = Encoding.UTF8.GetString(first.Data!);
        Assert.Contains("scenarios/scenario.json", json, StringComparison.Ordinal);
        Assert.True(json.IndexOf("content/a.json", StringComparison.Ordinal) < json.IndexOf("content/z.json", StringComparison.Ordinal));
        Assert.DoesNotContain(directory, json, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_UnknownProperty_IsInvalidJson()
    {
        var serializer = new NativeProjectManifestJsonSerializer(validation);
        byte[] data = Encoding.UTF8.GetBytes(
            """
            {"formatVersion":1,"id":"p","scenario":"scenario.json","contentPackages":["content.json"],"unknown":true}
            """);

        NativeProjectManifestDeserializationResult result = serializer.Deserialize(data);

        Assert.False(result.IsSuccess);
        Assert.Equal(NativeProjectPersistenceErrorCode.InvalidJson, result.ErrorCode);
    }

    [Fact]
    public void FileStore_MissingFile_DoesNotLeakPath()
    {
        var store = new NativeProjectManifestFileStore(new NativeProjectManifestJsonSerializer(validation));

        NativeProjectManifestLoadResult result = store.Load(Path.Combine(directory, "private", "missing.project.json"));

        Assert.False(result.IsSuccess);
        Assert.Equal(NativeProjectPersistenceErrorCode.FileNotFound, result.ErrorCode);
        Assert.DoesNotContain(directory, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileStore_SaveAndLoad_RoundTripsDeterministicManifest()
    {
        var store = new NativeProjectManifestFileStore(new NativeProjectManifestJsonSerializer(validation));
        string path = Path.Combine(directory, "saved.project.json");
        NativeProjectManifest manifest = CreateManifest();

        NativeProjectManifestSaveResult first = store.Save(path, manifest);
        byte[] firstBytes = File.ReadAllBytes(path);
        NativeProjectManifestSaveResult second = store.Save(path, manifest);
        byte[] secondBytes = File.ReadAllBytes(path);
        NativeProjectManifestLoadResult load = store.Load(path);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(firstBytes, secondBytes);
        Assert.True(load.IsSuccess);
        Assert.Equal(manifest.FormatVersion, load.Manifest!.FormatVersion);
        Assert.Equal(manifest.Id, load.Manifest.Id);
        Assert.Equal(manifest.Scenario, load.Manifest.Scenario);
        Assert.Equal(manifest.ContentPackages, load.Manifest.ContentPackages);
        Assert.Equal(manifest.Session, load.Manifest.Session);
        Assert.Empty(Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public void FileStore_InvalidManifestOrDirectory_DoesNotCreateOutput()
    {
        var store = new NativeProjectManifestFileStore(new NativeProjectManifestJsonSerializer(validation));
        string invalidManifestPath = Path.Combine(directory, "invalid.project.json");
        string missingDirectoryPath = Path.Combine(directory, "missing", "project.json");

        NativeProjectManifestSaveResult invalidManifest = store.Save(
            invalidManifestPath,
            CreateManifest() with { Scenario = "../escape.json" });
        NativeProjectManifestSaveResult missingDirectory = store.Save(
            missingDirectoryPath,
            CreateManifest());

        Assert.False(invalidManifest.IsSuccess);
        Assert.Equal(NativeProjectPersistenceErrorCode.ValidationFailed, invalidManifest.ErrorCode);
        Assert.False(missingDirectory.IsSuccess);
        Assert.Equal(NativeProjectPersistenceErrorCode.InvalidPath, missingDirectory.ErrorCode);
        Assert.False(File.Exists(invalidManifestPath));
        Assert.False(File.Exists(missingDirectoryPath));
    }

    [Fact]
    public void Load_CompleteProject_ResolvesRelativeDocuments()
    {
        NativeProjectLoader loader = CreateLoader();
        string manifestPath = WriteCompleteProject(CreateManifest());

        NativeProjectLoadResult result = loader.Load(manifestPath);

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-scenario", result.Project!.ScenarioBundle.Scenario.Id);
        Assert.Equal(["synthetic"], result.Project.ScenarioBundle.ContentPackageIds);
        Assert.NotNull(result.Project.Session);
    }

    [Fact]
    public void Load_SessionWithDifferentMapSize_IsRejected()
    {
        NativeProjectLoader loader = CreateLoader();
        string manifestPath = WriteCompleteProject(CreateManifest(), sessionWidth: 7);

        NativeProjectLoadResult result = loader.Load(manifestPath);

        NativeProjectLoadIssue issue = Assert.Single(result.Issues);
        Assert.Equal(NativeProjectLoadIssueCode.SessionMapSizeMismatch, issue.Code);
        Assert.DoesNotContain(directory, issue.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_MissingReferencedDocument_UsesSafeLabelsOnly()
    {
        NativeProjectLoader loader = CreateLoader();
        string manifestPath = WriteManifest(CreateManifest());

        NativeProjectLoadResult result = loader.Load(manifestPath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == NativeProjectLoadIssueCode.ScenarioBundleInvalid);
        Assert.DoesNotContain(directory, result.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Load_DefensivelyRejectsEscapingPathFromInjectedManifest()
    {
        NativeProjectManifest unsafeManifest = CreateManifest() with { Scenario = "../outside.json" };
        var manifestStore = new StubManifestStore(unsafeManifest);
        NativeProjectLoader loader = CreateLoader(manifestStore);
        string manifestPath = Path.Combine(directory, "project.json");

        NativeProjectLoadResult result = loader.Load(manifestPath);

        NativeProjectLoadIssue issue = Assert.Single(result.Issues);
        Assert.Equal(NativeProjectLoadIssueCode.ReferencedPathInvalid, issue.Code);
        Assert.DoesNotContain(directory, issue.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    private NativeProjectLoader CreateLoader(INativeProjectManifestFileStore? manifestStore = null)
    {
        var scenarioValidation = new ScenarioValidationService();
        var contentValidation = new ContentPackageValidationService();
        return new NativeProjectLoader(
            manifestStore ?? new NativeProjectManifestFileStore(new NativeProjectManifestJsonSerializer(validation)),
            new ScenarioBundleLoader(
                new ScenarioFileStore(new ScenarioJsonSerializer(scenarioValidation)),
                new ContentPackageFileStore(new ContentPackageJsonSerializer(contentValidation)),
                contentValidation,
                new ScenarioContentValidationService()),
            new GameSessionFileStore(new GameSessionJsonSerializer()));
    }

    private string WriteCompleteProject(NativeProjectManifest manifest, int sessionWidth = 8)
    {
        Directory.CreateDirectory(Path.Combine(directory, "scenarios"));
        Directory.CreateDirectory(Path.Combine(directory, "content"));
        Directory.CreateDirectory(Path.Combine(directory, "sessions"));

        var scenarioStore = new ScenarioFileStore(new ScenarioJsonSerializer(new ScenarioValidationService()));
        Assert.True(scenarioStore.Save(
            Path.Combine(directory, "scenarios", "scenario.json"),
            new ScenarioDefinition(
                ScenarioFormatV1.Version,
                "synthetic-scenario",
                "Synthetic scenario",
                null,
                new ScenarioMapDefinition(8, 6, "synthetic:plain", []))).IsSuccess);

        var contentValidation = new ContentPackageValidationService();
        var contentStore = new ContentPackageFileStore(new ContentPackageJsonSerializer(contentValidation));
        Assert.True(contentStore.Save(
            Path.Combine(directory, "content", "synthetic.json"),
            new ContentPackageDefinition(
                ContentPackageFormatV1.Version,
                "synthetic",
                "Synthetic",
                [new TerrainContentDefinition("plain", "Plain")],
                [])).IsSuccess);

        GameSessionCreationResult creation = GameSessionState.Create(
            new GridSize(sessionWidth, 6),
            ["participant"],
            []);
        Assert.True(creation.IsSuccess);
        var sessionStore = new GameSessionFileStore(new GameSessionJsonSerializer());
        Assert.True(sessionStore.Save(
            Path.Combine(directory, "sessions", "session.json"),
            creation.Session).IsSuccess);

        return WriteManifest(manifest);
    }

    private string WriteManifest(NativeProjectManifest manifest)
    {
        NativeProjectManifestSerializationResult serialization =
            new NativeProjectManifestJsonSerializer(validation).Serialize(manifest);
        Assert.True(serialization.IsSuccess);
        string path = Path.Combine(directory, "minimal.project.json");
        File.WriteAllBytes(path, serialization.Data!);
        return path;
    }

    private static NativeProjectManifest CreateManifest() =>
        new(
            NativeProjectManifestFormatV1.Version,
            "synthetic-project",
            "scenarios/scenario.json",
            ["content/synthetic.json"],
            "sessions/session.json");

    private sealed class StubManifestStore(NativeProjectManifest manifest) : INativeProjectManifestFileStore
    {
        public NativeProjectManifestLoadResult Load(string path) =>
            new(true, manifest, NativeProjectPersistenceErrorCode.None, [], null);

        public NativeProjectManifestSaveResult Save(string path, NativeProjectManifest? value) =>
            throw new NotSupportedException();
    }
}
