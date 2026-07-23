using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.OriginalGame.Tests;

public sealed class EnvironmentOriginalGameLocationProviderTests
{
    [Fact]
    public void Validate_WhenVariableIsMissing_ReturnsStructuredFailure()
    {
        OriginalGameLocationValidationResult result = CreateProvider(null).Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal(OriginalGameLocationValidationError.EnvironmentVariableMissing, result.Error);
        Assert.Null(result.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"   \"")]
    public void Validate_WhenVariableIsEmpty_ReturnsStructuredFailure(string value)
    {
        OriginalGameLocationValidationResult result = CreateProvider(value).Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal(OriginalGameLocationValidationError.EnvironmentVariableEmpty, result.Error);
    }

    [Fact]
    public void Validate_WhenPathIsQuoted_RemovesOuterQuotes()
    {
        using var directory = new TemporaryDirectory();

        OriginalGameLocationValidationResult result = CreateProvider($"\"{directory.Path}\"").Validate();

        Assert.True(result.IsSuccess);
        Assert.Equal(Normalize(directory.Path), result.Location!.FullPath);
    }

    [Fact]
    public void Validate_WhenPathHasTrailingSeparator_RemovesIt()
    {
        using var directory = new TemporaryDirectory();
        string configuredPath = directory.Path + Path.DirectorySeparatorChar;

        OriginalGameLocationValidationResult result = CreateProvider(configuredPath).Validate();

        Assert.True(result.IsSuccess);
        Assert.Equal(Normalize(directory.Path), result.Location!.FullPath);
    }

    [Fact]
    public void Validate_WhenPathContainsRelativeSegments_NormalizesThem()
    {
        using var directory = new TemporaryDirectory();
        string configuredPath = Path.Combine(directory.Path, ".", "unused", "..");

        OriginalGameLocationValidationResult result = CreateProvider(configuredPath).Validate();

        Assert.True(result.IsSuccess);
        Assert.Equal(Normalize(directory.Path), result.Location!.FullPath);
    }

    [Fact]
    public void Validate_WhenDirectoryDoesNotExist_ReturnsDirectoryNotFound()
    {
        using var parent = new TemporaryDirectory();
        string missingPath = Path.Combine(parent.Path, "missing-directory");

        OriginalGameLocationValidationResult result = CreateProvider(missingPath).Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal(OriginalGameLocationValidationError.DirectoryNotFound, result.Error);
    }

    [Fact]
    public void Validate_WhenPathIsAFile_ReturnsPathIsNotDirectory()
    {
        using var directory = new TemporaryDirectory();
        string filePath = Path.Combine(directory.Path, "synthetic-file.txt");
        File.WriteAllText(filePath, "synthetic test data");

        OriginalGameLocationValidationResult result = CreateProvider(filePath).Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal(OriginalGameLocationValidationError.PathIsNotDirectory, result.Error);
    }

    [Fact]
    public void Validate_WhenDirectoryExists_ReturnsAccessibleLocation()
    {
        using var directory = new TemporaryDirectory();

        OriginalGameLocationValidationResult result = CreateProvider(directory.Path).Validate();

        Assert.True(result.IsSuccess);
        Assert.Equal(OriginalGameLocationValidationError.None, result.Error);
        Assert.True(result.Location!.IsAccessible);
        Assert.Equal(Normalize(directory.Path), result.Location.FullPath);
    }

    [Fact]
    public void Validate_SafeRepresentationDoesNotContainFullPath()
    {
        using var directory = new TemporaryDirectory();

        OriginalGameLocationValidationResult result = CreateProvider(directory.Path).Validate();

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Location!.FullPath, result.DisplayPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Location.FullPath, result.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_SafeRepresentationContainsFinalDirectorySegment()
    {
        using var directory = new TemporaryDirectory();
        string finalSegment = Path.GetFileName(directory.Path);

        OriginalGameLocationValidationResult result = CreateProvider(directory.Path).Validate();

        Assert.True(result.IsSuccess);
        Assert.Contains(finalSegment, result.DisplayPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_DoesNotCreateMissingDirectory()
    {
        using var parent = new TemporaryDirectory();
        string missingPath = Path.Combine(parent.Path, "must-not-be-created");

        _ = CreateProvider(missingPath).Validate();

        Assert.False(Directory.Exists(missingPath));
    }

    [Fact]
    public void Validate_DoesNotCreateFilesInValidDirectory()
    {
        using var directory = new TemporaryDirectory();
        string[] entriesBefore = Directory.GetFileSystemEntries(directory.Path);

        _ = CreateProvider(directory.Path).Validate();

        string[] entriesAfter = Directory.GetFileSystemEntries(directory.Path);
        Assert.Equal(entriesBefore, entriesAfter);
    }

    [Fact]
    public void Validate_WhenPathIsInvalid_ReturnsInvalidPathWithoutThrowing()
    {
        OriginalGameLocationValidationResult? result = null;

        Exception? exception = Record.Exception(() => result = CreateProvider("invalid\0path").Validate());

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.Equal(OriginalGameLocationValidationError.InvalidPath, result.Error);
    }

    [Fact]
    public void Validate_WhenEnvironmentReaderThrows_ReturnsUnexpectedError()
    {
        var provider = new EnvironmentOriginalGameLocationProvider(new ThrowingEnvironmentVariableReader());

        OriginalGameLocationValidationResult result = provider.Validate();

        Assert.False(result.IsSuccess);
        Assert.Equal(OriginalGameLocationValidationError.UnexpectedError, result.Error);
    }

    private static EnvironmentOriginalGameLocationProvider CreateProvider(string? value) =>
        new(new StubEnvironmentVariableReader(value));

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private sealed class StubEnvironmentVariableReader(string? value) : IEnvironmentVariableReader
    {
        public string? GetEnvironmentVariable(string variableName)
        {
            Assert.Equal(EnvironmentOriginalGameLocationProvider.EnvironmentVariableName, variableName);
            return value;
        }
    }

    private sealed class ThrowingEnvironmentVariableReader : IEnvironmentVariableReader
    {
        public string? GetEnvironmentVariable(string variableName) =>
            throw new InvalidOperationException("Synthetic reader failure.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "DisciplesRemaster.Tests",
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
