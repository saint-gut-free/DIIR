using DisciplesRemaster.MapInspector;
using DisciplesRemaster.OriginalGame.Research.MapExperiments;

namespace DisciplesRemaster.Tools.Tests;

public sealed class MapExperimentValidationCommandTests
{
    [Fact]
    public void Run_ValidMetadata_ReturnsZero()
    {
        var context = new CommandContext(ValidResult());

        int exitCode = context.Run(["validate-experiments", "valid-catalog.json"]);

        Assert.Equal(MapExperimentValidationCommand.SuccessExitCode, exitCode);
        Assert.Contains("Errors: 0", context.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_UnavailableInput_ReturnsTwoWithoutStackTrace()
    {
        var context = new CommandContext(ValidResult())
        {
            Exception = new MapExperimentInputException("Synthetic input is unavailable."),
        };

        int exitCode = context.Run(["validate-experiments", "missing.json"]);

        Assert.Equal(MapExperimentValidationCommand.InputErrorExitCode, exitCode);
        Assert.DoesNotContain(" at ", context.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_ValidationErrors_ReturnsThree()
    {
        var issue = new MapExperimentValidationIssue(
            MapExperimentValidationCode.MissingOperation,
            MapExperimentValidationSeverity.Error,
            "MAP-001",
            "$.operation",
            "Operation is required.");
        var context = new CommandContext(new MapExperimentValidationResult([], [issue]));

        int exitCode = context.Run(["validate-experiments", "invalid.json"]);

        Assert.Equal(MapExperimentValidationCommand.ValidationErrorExitCode, exitCode);
        Assert.Contains("MissingOperation", context.Output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData()]
    [InlineData("validate-experiments")]
    [InlineData("validate-experiments", "a", "b")]
    public void Run_InvalidArguments_Returns64(params string[] arguments)
    {
        var context = new CommandContext(ValidResult());

        int exitCode = context.Run(arguments);

        Assert.Equal(MapExperimentValidationCommand.UsageErrorExitCode, exitCode);
        Assert.Equal(0, context.Service.CallCount);
    }

    [Fact]
    public void Run_UnknownCommand_Returns64()
    {
        var context = new CommandContext(ValidResult());

        int exitCode = context.Run(["inspect-map", "metadata.json"]);

        Assert.Equal(MapExperimentValidationCommand.UsageErrorExitCode, exitCode);
        Assert.Contains("Usage:", context.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_UnexpectedFailure_Returns70WithoutStackTrace()
    {
        var context = new CommandContext(ValidResult())
        {
            Exception = new InvalidOperationException("Sensitive synthetic detail."),
        };

        int exitCode = context.Run(["validate-experiments", "metadata.json"]);

        Assert.Equal(MapExperimentValidationCommand.SoftwareErrorExitCode, exitCode);
        Assert.DoesNotContain("Sensitive synthetic detail", context.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_AbsoluteInputPath_IsNotPrinted()
    {
        var context = new CommandContext(ValidResult());
        string absolute = Path.GetFullPath(Path.Combine("synthetic-private", "catalog.json"));

        int exitCode = context.Run(["validate-experiments", absolute]);

        Assert.Equal(MapExperimentValidationCommand.SuccessExitCode, exitCode);
        Assert.DoesNotContain(absolute, context.Output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Input: catalog.json", context.Output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WarningsWithoutErrors_ReturnsZeroAndPrintsWarning()
    {
        var warning = new MapExperimentValidationIssue(
            MapExperimentValidationCode.UnsupportedDocumentType,
            MapExperimentValidationSeverity.Warning,
            "MAP-001",
            "$.optional",
            "Synthetic warning.");
        var context = new CommandContext(new MapExperimentValidationResult([], [warning]));

        int exitCode = context.Run(["validate-experiments", "metadata.json"]);

        Assert.Equal(MapExperimentValidationCommand.SuccessExitCode, exitCode);
        Assert.Contains("Warnings: 1", context.Output.ToString(), StringComparison.Ordinal);
    }

    private static MapExperimentValidationResult ValidResult() => new(
        [new MapExperimentRecord
        {
            FormatVersion = 1,
            ExperimentId = "MAP-000A",
            Title = "Synthetic baseline",
            Status = "planned",
            Operation = new MapExperimentOperation { Type = "create_baseline" },
        }],
        []);

    private sealed class CommandContext(MapExperimentValidationResult result)
    {
        public StubValidationService Service { get; } = new(result);

        public Exception? Exception
        {
            set => Service.Exception = value;
        }

        public StringWriter Output { get; } = new();

        public StringWriter Error { get; } = new();

        public int Run(IReadOnlyList<string> arguments) =>
            MapExperimentValidationCommand.Run(arguments, Service, Output, Error);
    }

    private sealed class StubValidationService(MapExperimentValidationResult result) : IMapExperimentValidationService
    {
        public int CallCount { get; private set; }

        public Exception? Exception { get; set; }

        public MapExperimentValidationResult LoadAndValidate(string inputPath)
        {
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return result;
        }

        public MapExperimentValidationResult Validate(IEnumerable<MapExperimentRecord> experiments) => result;
    }
}
