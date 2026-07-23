using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.FileInventory;

/// <summary>Command-line entry points for read-only research tooling.</summary>
public static class FileInventoryCommand
{
    public const int SuccessExitCode = 0;
    public const int ValidationFailureExitCode = 2;
    public const int UsageErrorExitCode = 64;

    public static int Run(
        IReadOnlyList<string> arguments,
        IOriginalGameLocationProvider locationProvider,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(locationProvider);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Count != 1 || !string.Equals(arguments[0], "validate-path", StringComparison.Ordinal))
        {
            WriteUsage(error);
            return UsageErrorExitCode;
        }

        OriginalGameLocationValidationResult result = locationProvider.Validate();
        if (!result.IsSuccess)
        {
            error.WriteLine("Original game directory validation failed.");
            error.WriteLine($"Code: {result.Error}");
            error.WriteLine($"Location: {result.DisplayPath}");
            error.WriteLine(result.Message);
            return ValidationFailureExitCode;
        }

        output.WriteLine("Original game research directory is configured.");
        output.WriteLine($"Location: {result.DisplayPath}");
        output.WriteLine("Status: accessible");
        output.WriteLine("The configured directory is accessible and can be used as a research input.");
        return SuccessExitCode;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project tools/DisciplesRemaster.FileInventory -- validate-path");
    }
}
