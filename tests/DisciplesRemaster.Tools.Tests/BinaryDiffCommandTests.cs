using DisciplesRemaster.BinaryDiff;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.Tools.Tests;

public sealed class BinaryDiffCommandTests
{
    [Fact]
    public void Run_Success_ReturnsZeroAndUsesSafeLabels()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport());

        int exitCode = context.Run(["compare", "a.bin", "b.bin", "--label-a", "baseline", "--label-b", "one-change"]);

        Assert.Equal(BinaryDiffCommand.SuccessExitCode, exitCode);
        Assert.Equal("baseline", context.Service.LabelA);
        Assert.Equal("one-change", context.Service.LabelB);
        Assert.DoesNotContain(context.Service.FileAPath, context.Output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_InputFailure_ReturnsTwoWithoutStackTrace()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport())
        {
            ServiceException = new BinaryInputException("Input is unavailable."),
        };

        int exitCode = context.Run(["compare", "missing.bin", "b.bin"]);

        Assert.Equal(BinaryDiffCommand.InputErrorExitCode, exitCode);
        Assert.DoesNotContain(" at ", context.Error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Run_FileChangedWarning_ReturnsThree()
    {
        BinaryDiffReport report = BinaryDiffTestData.CreateReport(
            [new BinaryDiffWarning(BinaryDiffWarningCode.FileChangedDuringRead, "File A", "Changed.")]);
        var context = new CommandContext(report);

        int exitCode = context.Run(["compare", "a.bin", "b.bin"]);

        Assert.Equal(BinaryDiffCommand.PartialSuccessExitCode, exitCode);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("compare", "only-one-file")]
    [InlineData("compare", "a", "b", "--max-ranges", "0")]
    [InlineData("compare", "a", "b", "--context", "999")]
    [InlineData("compare", "a", "b", "--format", "xml")]
    public void Run_InvalidArguments_Returns64(params string[] arguments)
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport());

        int exitCode = context.Run(arguments);

        Assert.Equal(BinaryDiffCommand.UsageErrorExitCode, exitCode);
        Assert.Equal(0, context.Service.CallCount);
        Assert.Contains("Usage:", context.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_UnsafeLabel_Returns64()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport());

        int exitCode = context.Run(["compare", "a", "b", "--label-a", "bad\nlabel"]);

        Assert.Equal(BinaryDiffCommand.UsageErrorExitCode, exitCode);
    }

    [Fact]
    public void Run_UnexpectedFailure_Returns70()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport())
        {
            ServiceException = new InvalidOperationException("Synthetic failure."),
        };

        int exitCode = context.Run(["compare", "a", "b"]);

        Assert.Equal(BinaryDiffCommand.SoftwareErrorExitCode, exitCode);
    }

    [Fact]
    public void Run_LocationProviderUnexpectedFailure_Returns70WithoutStackTrace()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport())
        {
            LocationException = new InvalidOperationException("Synthetic provider failure."),
        };

        int exitCode = context.Run(["compare", "a", "b"]);

        Assert.Equal(BinaryDiffCommand.SoftwareErrorExitCode, exitCode);
        Assert.DoesNotContain("Synthetic provider failure", context.Error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, context.Service.CallCount);
    }

    [Fact]
    public void Run_RepeatedSearchOptionsAndEncoding_ArePassedToService()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport());

        int exitCode = context.Run([
            "compare", "a", "b",
            "--search-int32", "12",
            "--search-int32", "8",
            "--search-string", "Test",
            "--encoding", "all"]);

        Assert.Equal(BinaryDiffCommand.SuccessExitCode, exitCode);
        Assert.Equal(5, context.Service.Options!.Searches.Count);
        Assert.Equal(2, context.Service.Options.Searches.Count(search => search.Type == BinarySearchValueType.Int32));
        Assert.Equal(3, context.Service.Options.Searches.Count(search => search.Type == BinarySearchValueType.String));
    }

    [Fact]
    public void Run_OutputInsideOriginalRoot_Returns64BeforeComparison()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport());
        context.Writer.Validation = new BinaryDiffOutputValidationResult(
            false,
            null,
            BinaryDiffWarningCode.OutputInsideOriginalGameDirectory,
            "Rejected.");

        int exitCode = context.Run(["compare", "a", "b"]);

        Assert.Equal(BinaryDiffCommand.UsageErrorExitCode, exitCode);
        Assert.Equal(0, context.Service.CallCount);
        Assert.Equal(0, context.Writer.WriteCount);
    }

    [Fact]
    public void Run_FormatAndBounds_ArePassedToDependencies()
    {
        var context = new CommandContext(BinaryDiffTestData.CreateReport());

        int exitCode = context.Run(["compare", "a", "b", "--format", "json", "--context", "32", "--max-ranges", "7"]);

        Assert.Equal(BinaryDiffCommand.SuccessExitCode, exitCode);
        Assert.Equal(BinaryDiffOutputFormat.Json, context.Writer.Format);
        Assert.Equal(32, context.Service.Options!.Context);
        Assert.Equal(7, context.Service.Options.MaxRanges);
    }

    private sealed class CommandContext
    {
        public CommandContext(BinaryDiffReport report)
        {
            Service = new StubService(this, report);
            Writer = new StubWriter();
            LocationProvider = new StubLocationProvider(this);
            WorkingDirectory = Path.GetFullPath("synthetic-binary-diff-working-directory");
        }

        public Exception? ServiceException { get; set; }

        public Exception? LocationException { get; set; }

        public StubService Service { get; }

        public StubWriter Writer { get; }

        public StubLocationProvider LocationProvider { get; }

        public string WorkingDirectory { get; }

        public StringWriter Output { get; } = new();

        public StringWriter Error { get; } = new();

        public int Run(IReadOnlyList<string> arguments) => BinaryDiffCommand.Run(
            arguments,
            LocationProvider,
            Service,
            Writer,
            WorkingDirectory,
            Output,
            Error);

        public sealed class StubService(CommandContext context, BinaryDiffReport report) : IBinaryComparisonService
        {
            public int CallCount { get; private set; }

            public string FileAPath { get; private set; } = string.Empty;

            public string LabelA { get; private set; } = string.Empty;

            public string LabelB { get; private set; } = string.Empty;

            public BinaryDiffOptions? Options { get; private set; }

            public BinaryDiffReport Compare(string fileAPath, string fileBPath, string labelA, string labelB, BinaryDiffOptions options)
            {
                CallCount++;
                FileAPath = fileAPath;
                LabelA = labelA;
                LabelB = labelB;
                Options = options;
                if (context.ServiceException is not null) throw context.ServiceException;
                return report with
                {
                    FileA = report.FileA with { Label = labelA },
                    FileB = report.FileB with { Label = labelB },
                    Options = options,
                };
            }
        }

        public sealed class StubWriter : IBinaryDiffReportWriter
        {
            public BinaryDiffOutputValidationResult Validation { get; set; } =
                new(true, Path.GetFullPath("synthetic-binary-diff-output"), null, "Valid.");

            public int WriteCount { get; private set; }

            public BinaryDiffOutputFormat? Format { get; private set; }

            public BinaryDiffOutputValidationResult ValidateOutputPath(string outputPath, string fileAPath, string fileBPath, OriginalGameLocation? originalGameLocation) =>
                Validation with { NormalizedOutputPath = Validation.IsValid ? outputPath : null };

            public BinaryDiffReportPaths WriteReports(BinaryDiffReport report, string outputPath, BinaryDiffOutputFormat format)
            {
                WriteCount++;
                Format = format;
                return new BinaryDiffReportPaths(
                    format is BinaryDiffOutputFormat.Json or BinaryDiffOutputFormat.Both ? Path.Combine(outputPath, "binary-diff.json") : null,
                    format is BinaryDiffOutputFormat.Text or BinaryDiffOutputFormat.Both ? Path.Combine(outputPath, "binary-diff.txt") : null);
            }
        }

        public sealed class StubLocationProvider(CommandContext context) : IOriginalGameLocationProvider
        {
            public OriginalGameLocationValidationResult Validate()
            {
                if (context.LocationException is not null) throw context.LocationException;
                return OriginalGameLocationValidationResult.Failure(
                    OriginalGameLocationValidationError.EnvironmentVariableMissing,
                    "Not configured.",
                    "<not-configured>");
            }
        }
    }
}
