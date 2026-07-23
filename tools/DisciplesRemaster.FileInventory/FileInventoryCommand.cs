using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.FileInventory;

public static class FileInventoryCommand
{
    public const int SuccessExitCode = 0;
    public const int ValidationFailureExitCode = 2;
    public const int PartialSuccessExitCode = 3;
    public const int UsageErrorExitCode = 64;
    public const int SoftwareErrorExitCode = 70;

    public static int Run(
        IReadOnlyList<string> arguments,
        IOriginalGameLocationProvider locationProvider,
        TextWriter output,
        TextWriter error) =>
        Run(
            arguments,
            locationProvider,
            new OriginalGameInventoryService(),
            new OriginalGameInventoryReportWriter(),
            Environment.CurrentDirectory,
            output,
            error);

    public static int Run(
        IReadOnlyList<string> arguments,
        IOriginalGameLocationProvider locationProvider,
        IOriginalGameInventoryService inventoryService,
        IOriginalGameInventoryReportWriter reportWriter,
        string workingDirectory,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(locationProvider);
        ArgumentNullException.ThrowIfNull(inventoryService);
        ArgumentNullException.ThrowIfNull(reportWriter);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Count == 1 && string.Equals(arguments[0], "validate-path", StringComparison.Ordinal))
        {
            return ValidatePath(locationProvider, output, error);
        }

        if (arguments.Count == 0 || !string.Equals(arguments[0], "inventory", StringComparison.Ordinal))
        {
            WriteUsage(error);
            return UsageErrorExitCode;
        }

        if (!TryParseInventoryArguments(arguments.Skip(1).ToArray(), workingDirectory, error, out ParsedInventoryOptions? parsed))
        {
            return UsageErrorExitCode;
        }

        OriginalGameLocationValidationResult locationResult = locationProvider.Validate();
        if (!locationResult.IsSuccess)
        {
            WriteLocationFailure(locationResult, error);
            return ValidationFailureExitCode;
        }

        OriginalGameLocation location = locationResult.Location!;
        OriginalGameInventoryOutputValidationResult outputValidation = reportWriter.ValidateOutputPath(parsed!.OutputPath, location);
        if (!outputValidation.IsValid)
        {
            error.WriteLine("Inventory output validation failed.");
            error.WriteLine($"Code: {outputValidation.ErrorCode}");
            error.WriteLine(outputValidation.Message);
            return UsageErrorExitCode;
        }

        try
        {
            var options = new OriginalGameInventoryOptions(parsed.IncludeHidden, parsed.MaxDepth);
            OriginalGameInventory inventory = inventoryService.CreateInventory(location, options);
            OriginalGameInventoryReportPaths paths = reportWriter.WriteReports(inventory, outputValidation.NormalizedOutputPath!);
            output.WriteLine("Original game inventory completed.");
            output.WriteLine($"Location: {inventory.SourceLocation}");
            output.WriteLine($"Files processed: {inventory.Summary.FilesProcessed}");
            output.WriteLine($"Files skipped: {inventory.Summary.FilesSkipped}");
            output.WriteLine($"Total size: {inventory.Summary.TotalSizeBytes} bytes");
            output.WriteLine($"JSON report: {DisplayOutputPath(paths.JsonPath, workingDirectory)}");
            output.WriteLine($"Summary: {DisplayOutputPath(paths.MarkdownPath, workingDirectory)}");
            if (inventory.IsPartial)
            {
                output.WriteLine($"Warnings: {inventory.Warnings.Count}; inventory is partial.");
                return PartialSuccessExitCode;
            }

            return SuccessExitCode;
        }
        catch (Exception)
        {
            error.WriteLine("Original game inventory failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return SoftwareErrorExitCode;
        }
    }

    private static int ValidatePath(
        IOriginalGameLocationProvider locationProvider,
        TextWriter output,
        TextWriter error)
    {
        OriginalGameLocationValidationResult result = locationProvider.Validate();
        if (!result.IsSuccess)
        {
            WriteLocationFailure(result, error);
            return ValidationFailureExitCode;
        }

        output.WriteLine("Original game research directory is configured.");
        output.WriteLine($"Location: {result.DisplayPath}");
        output.WriteLine("Status: accessible");
        output.WriteLine("The configured directory is accessible and can be used as a research input.");
        return SuccessExitCode;
    }

    private static void WriteLocationFailure(OriginalGameLocationValidationResult result, TextWriter error)
    {
        error.WriteLine("Original game directory validation failed.");
        error.WriteLine($"Code: {result.Error}");
        error.WriteLine($"Location: {result.DisplayPath}");
        error.WriteLine(result.Message);
    }

    private static bool TryParseInventoryArguments(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TextWriter error,
        out ParsedInventoryOptions? options)
    {
        options = null;
        string outputPath = Path.Combine(workingDirectory, "artifacts", "research", "inventory");
        bool includeHidden = false;
        int maxDepth = OriginalGameInventoryOptions.DefaultMaxDepth;
        bool outputSeen = false;
        bool maxDepthSeen = false;

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (argument == "--include-hidden" && !includeHidden)
            {
                includeHidden = true;
                continue;
            }

            if (argument == "--output" && !outputSeen && index + 1 < arguments.Count)
            {
                outputSeen = true;
                string value = arguments[++index];
                if (string.IsNullOrWhiteSpace(value))
                {
                    return Invalid("--output requires a non-empty directory.");
                }

                try
                {
                    outputPath = Path.GetFullPath(value, workingDirectory);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    return Invalid("--output is not a valid directory path.");
                }

                continue;
            }

            if (argument == "--max-depth" && !maxDepthSeen && index + 1 < arguments.Count)
            {
                maxDepthSeen = true;
                if (!int.TryParse(arguments[++index], out maxDepth) || maxDepth <= 0)
                {
                    return Invalid("--max-depth requires a positive integer.");
                }

                continue;
            }

            return Invalid($"Unknown or incomplete inventory option: {argument}");
        }

        options = new ParsedInventoryOptions(outputPath, includeHidden, maxDepth);
        return true;

        bool Invalid(string message)
        {
            error.WriteLine(message);
            WriteUsage(error);
            return false;
        }
    }

    private static string DisplayOutputPath(string path, string workingDirectory)
    {
        string relative = Path.GetRelativePath(workingDirectory, path).Replace('\\', '/');
        return relative == "." || relative.StartsWith("../", StringComparison.Ordinal)
            ? path
            : relative;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project tools/DisciplesRemaster.FileInventory -- validate-path");
        writer.WriteLine("  dotnet run --project tools/DisciplesRemaster.FileInventory -- inventory [--output <directory>] [--include-hidden] [--max-depth <number>]");
    }

    private sealed record ParsedInventoryOptions(string OutputPath, bool IncludeHidden, int MaxDepth);
}
