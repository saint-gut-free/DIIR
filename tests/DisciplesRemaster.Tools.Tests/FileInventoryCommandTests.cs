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

    [Fact]
    public void Run_InventorySuccess_ReturnsZero()
    {
        var context = new InventoryCommandTestContext(CreateInventory());

        int exitCode = context.Run(["inventory"]);

        Assert.Equal(FileInventoryCommand.SuccessExitCode, exitCode);
        Assert.Contains("Files processed: 1", context.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, context.Error.ToString());
    }

    [Fact]
    public void Run_PartialInventory_ReturnsThree()
    {
        OriginalGameInventory inventory = CreateInventory(
            [new OriginalGameInventoryWarning(
                OriginalGameInventoryWarningCode.FileInaccessible,
                "locked.bin",
                "File could not be read.")]);
        var context = new InventoryCommandTestContext(inventory);

        int exitCode = context.Run(["inventory"]);

        Assert.Equal(FileInventoryCommand.PartialSuccessExitCode, exitCode);
        Assert.Contains("inventory is partial", context.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_InventoryLocationFailure_ReturnsTwo()
    {
        var context = new InventoryCommandTestContext(CreateInventory())
        {
            LocationResult = OriginalGameLocationValidationResult.Failure(
                OriginalGameLocationValidationError.EnvironmentVariableMissing,
                "Missing.",
                "<not-configured>"),
        };

        int exitCode = context.Run(["inventory"]);

        Assert.Equal(FileInventoryCommand.ValidationFailureExitCode, exitCode);
        Assert.Equal(0, context.InventoryService.CallCount);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void Run_InvalidMaximumDepth_ReturnsUsageError(string value)
    {
        var context = new InventoryCommandTestContext(CreateInventory());

        int exitCode = context.Run(["inventory", "--max-depth", value]);

        Assert.Equal(FileInventoryCommand.UsageErrorExitCode, exitCode);
        Assert.Equal(0, context.LocationProvider.ValidationCount);
    }

    [Fact]
    public void Run_InvalidOutputArgument_ReturnsUsageError()
    {
        var context = new InventoryCommandTestContext(CreateInventory());

        int exitCode = context.Run(["inventory", "--output"]);

        Assert.Equal(FileInventoryCommand.UsageErrorExitCode, exitCode);
    }

    [Fact]
    public void Run_OutputInsideSource_ReturnsUsageErrorBeforeInventory()
    {
        var context = new InventoryCommandTestContext(CreateInventory());
        context.ReportWriter.OutputValidation = new OriginalGameInventoryOutputValidationResult(
            false,
            null,
            OriginalGameInventoryWarningCode.OutputInsideOriginalGameDirectory,
            "Output is inside source.");

        int exitCode = context.Run(["inventory"]);

        Assert.Equal(FileInventoryCommand.UsageErrorExitCode, exitCode);
        Assert.Equal(0, context.InventoryService.CallCount);
        Assert.Equal(0, context.ReportWriter.WriteCount);
    }

    [Fact]
    public void Run_InventoryOptions_ArePassedToService()
    {
        var context = new InventoryCommandTestContext(CreateInventory());

        int exitCode = context.Run(["inventory", "--include-hidden", "--max-depth", "7", "--output", "custom-output"]);

        Assert.Equal(FileInventoryCommand.SuccessExitCode, exitCode);
        Assert.Equal(new OriginalGameInventoryOptions(IncludeHidden: true, MaxDepth: 7), context.InventoryService.Options);
        Assert.EndsWith("custom-output", context.ReportWriter.ValidatedOutputPath, StringComparison.Ordinal);
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

    private static OriginalGameInventory CreateInventory(
        IReadOnlyList<OriginalGameInventoryWarning>? warnings = null)
    {
        var entry = new OriginalGameInventoryEntry(
            "file.bin",
            "file.bin",
            ".bin",
            3,
            new string('a', 64),
            DateTime.UnixEpoch,
            1,
            FileAttributes.Normal,
            OriginalGameFileCategory.Data,
            OriginalGameFileSignature.Unknown,
            IsHidden: false,
            IsReparsePoint: false,
            IsReadSuccessful: true,
            Warning: null);
        IReadOnlyList<OriginalGameInventoryWarning> inventoryWarnings = warnings ?? [];
        var summary = new OriginalGameInventorySummary(
            1,
            inventoryWarnings.Count,
            3,
            [new OriginalGameInventoryCount(".bin", 1)],
            [new OriginalGameInventoryCount("Data", 1)],
            []);
        return new OriginalGameInventory(
            OriginalGameInventory.CurrentReportFormatVersion,
            "<configured-original-game-directory>/synthetic",
            new OriginalGameInventoryOptions(),
            [entry],
            inventoryWarnings,
            summary);
    }

    private sealed class InventoryCommandTestContext
    {
        private readonly OriginalGameInventory inventory;

        public InventoryCommandTestContext(OriginalGameInventory inventory)
        {
            this.inventory = inventory;
            LocationResult = OriginalGameLocationValidationResult.Success(
                new OriginalGameLocation(Path.GetFullPath("synthetic-source"), "<configured-original-game-directory>/synthetic"));
            LocationProvider = new MutableLocationProvider(this);
            InventoryService = new StubInventoryService(inventory);
            ReportWriter = new StubReportWriter();
        }

        public OriginalGameLocationValidationResult LocationResult { get; set; }

        public MutableLocationProvider LocationProvider { get; }

        public StubInventoryService InventoryService { get; }

        public StubReportWriter ReportWriter { get; }

        public StringWriter Output { get; } = new();

        public StringWriter Error { get; } = new();

        public int Run(IReadOnlyList<string> arguments) =>
            FileInventoryCommand.Run(
                arguments,
                LocationProvider,
                InventoryService,
                ReportWriter,
                Path.GetFullPath("synthetic-working-directory"),
                Output,
                Error);

        public sealed class MutableLocationProvider(InventoryCommandTestContext context) : IOriginalGameLocationProvider
        {
            public int ValidationCount { get; private set; }

            public OriginalGameLocationValidationResult Validate()
            {
                ValidationCount++;
                return context.LocationResult;
            }
        }

        public sealed class StubInventoryService(OriginalGameInventory inventory) : IOriginalGameInventoryService
        {
            public int CallCount { get; private set; }

            public OriginalGameInventoryOptions? Options { get; private set; }

            public OriginalGameInventory CreateInventory(OriginalGameLocation location, OriginalGameInventoryOptions options)
            {
                CallCount++;
                Options = options;
                return inventory;
            }
        }

        public sealed class StubReportWriter : IOriginalGameInventoryReportWriter
        {
            public OriginalGameInventoryOutputValidationResult OutputValidation { get; set; } =
                new(true, Path.GetFullPath("synthetic-output"), null, "Valid.");

            public int WriteCount { get; private set; }

            public string ValidatedOutputPath { get; private set; } = string.Empty;

            public OriginalGameInventoryOutputValidationResult ValidateOutputPath(string outputPath, OriginalGameLocation location)
            {
                ValidatedOutputPath = outputPath;
                return OutputValidation with
                {
                    NormalizedOutputPath = OutputValidation.IsValid ? outputPath : null,
                };
            }

            public OriginalGameInventoryReportPaths WriteReports(OriginalGameInventory inventory, string outputPath)
            {
                WriteCount++;
                return new OriginalGameInventoryReportPaths(
                    Path.Combine(outputPath, "inventory.json"),
                    Path.Combine(outputPath, "inventory-summary.md"));
            }
        }
    }
}
