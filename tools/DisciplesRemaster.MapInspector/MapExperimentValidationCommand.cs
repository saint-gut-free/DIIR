using DisciplesRemaster.OriginalGame.Research.MapExperiments;

namespace DisciplesRemaster.MapInspector;

public static class MapExperimentValidationCommand
{
    public const int SuccessExitCode = 0;
    public const int InputErrorExitCode = 2;
    public const int ValidationErrorExitCode = 3;
    public const int UsageErrorExitCode = 64;
    public const int SoftwareErrorExitCode = 70;

    public static int Run(
        IReadOnlyList<string> arguments,
        IMapExperimentValidationService validationService,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(validationService);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Count != 2 ||
            !string.Equals(arguments[0], "validate-experiments", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(arguments[1]))
        {
            error.WriteLine("Unknown command or invalid arguments.");
            WriteUsage(error);
            return UsageErrorExitCode;
        }

        try
        {
            MapExperimentValidationResult result = validationService.LoadAndValidate(arguments[1]);
            int errors = result.Issues.Count(issue => issue.Severity == MapExperimentValidationSeverity.Error);
            int warnings = result.Issues.Count(issue => issue.Severity == MapExperimentValidationSeverity.Warning);
            int information = result.Issues.Count(issue => issue.Severity == MapExperimentValidationSeverity.Info);

            output.WriteLine("Map experiment metadata validation completed.");
            output.WriteLine($"Input: {SafeInputLabel(arguments[1])}");
            output.WriteLine($"Experiments: {result.Experiments.Count}");
            output.WriteLine($"Errors: {errors}");
            output.WriteLine($"Warnings: {warnings}");
            output.WriteLine($"Info: {information}");

            foreach (MapExperimentValidationIssue issue in result.Issues)
            {
                output.WriteLine($"{issue.Severity} {issue.Code} [{issue.ExperimentId}] {issue.PropertyPath}: {issue.Message}");
            }

            return errors == 0 ? SuccessExitCode : ValidationErrorExitCode;
        }
        catch (MapExperimentInputException exception)
        {
            error.WriteLine("Map experiment metadata input is unavailable.");
            error.WriteLine("Code: FileInaccessible");
            error.WriteLine(exception.Message);
            return InputErrorExitCode;
        }
        catch (Exception)
        {
            error.WriteLine("Map experiment metadata validation failed unexpectedly.");
            error.WriteLine("Code: ValidationFailed");
            return SoftwareErrorExitCode;
        }
    }

    private static string SafeInputLabel(string input)
    {
        string trimmed = Path.TrimEndingDirectorySeparator(input.Trim());
        string label = Path.GetFileName(trimmed);
        return string.IsNullOrWhiteSpace(label) ? "<metadata-input>" : label;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  dotnet run --project tools/DisciplesRemaster.MapInspector -- validate-experiments <catalog-or-directory>");
    }
}
