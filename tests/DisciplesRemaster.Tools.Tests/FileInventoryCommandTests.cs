using DisciplesRemaster.FileInventory;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.Tools.Tests;

public sealed class FileInventoryCommandTests
{
    [Fact]
    public void Run_WhenValidationSucceeds_ReturnsZeroAndPrintsOnlyRedactedPath()
    {
        const string fullPath = "C:\\Users\\PrivateUser\\Disciples2Reference";
        const string displayPath = "<configured-original-game-directory>/Disciples2Reference";
        var provider = new StubLocationProvider(
            OriginalGameLocationValidationResult.Success(new OriginalGameLocation(fullPath, displayPath)));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = FileInventoryCommand.Run(["validate-path"], provider, output, error);

        Assert.Equal(FileInventoryCommand.SuccessExitCode, exitCode);
        Assert.Contains("Status: accessible", output.ToString(), StringComparison.Ordinal);
        Assert.Contains(displayPath, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fullPath, output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public void Run_WhenValidationFails_ReturnsNonZeroAndPrintsStableCode()
    {
        const string fullPath = "C:\\Users\\PrivateUser\\missing-directory";
        const string displayPath = "<configured-original-game-directory>/missing-directory";
        var provider = new StubLocationProvider(
            OriginalGameLocationValidationResult.Failure(
                OriginalGameLocationValidationError.DirectoryNotFound,
                "The configured directory does not exist.",
                displayPath));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = FileInventoryCommand.Run(["validate-path"], provider, output, error);

        Assert.Equal(FileInventoryCommand.ValidationFailureExitCode, exitCode);
        Assert.Contains("Code: DirectoryNotFound", error.ToString(), StringComparison.Ordinal);
        Assert.Contains(displayPath, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fullPath, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown-command")]
    [InlineData("validate-path", "extra-argument")]
    public void Run_WhenCommandIsUnknown_ReturnsUsageError(params string[] arguments)
    {
        var provider = new StubLocationProvider(
            OriginalGameLocationValidationResult.Failure(
                OriginalGameLocationValidationError.UnexpectedError,
                "This result must not be used.",
                "<not-configured>"));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = FileInventoryCommand.Run(arguments, provider, output, error);

        Assert.Equal(FileInventoryCommand.UsageErrorExitCode, exitCode);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, provider.ValidationCount);
    }

    private sealed class StubLocationProvider(OriginalGameLocationValidationResult result)
        : IOriginalGameLocationProvider
    {
        public int ValidationCount { get; private set; }

        public OriginalGameLocationValidationResult Validate()
        {
            ValidationCount++;
            return result;
        }
    }
}
