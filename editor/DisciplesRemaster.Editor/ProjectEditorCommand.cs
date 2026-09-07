using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Editor;

public static class ProjectEditorCommand
{
    public static int Run(
        IReadOnlyList<string> arguments,
        INativeProjectLoader loader,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return arguments.FirstOrDefault() switch
            {
                "validate-project" => Validate(arguments, loader, output, error),
                "summary-project" => Summary(arguments, loader, output, error),
                _ => UsageFailure(error),
            };
        }
        catch (Exception)
        {
            error.WriteLine("Native project command failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return ScenarioEditorCommand.SoftwareErrorExitCode;
        }
    }

    private static int Validate(
        IReadOnlyList<string> arguments,
        INativeProjectLoader loader,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error);
        }

        NativeProjectLoadResult load = loader.Load(arguments[1]);
        if (!load.IsSuccess || load.Project is null)
        {
            return WriteFailure(load, arguments[1], error);
        }

        output.WriteLine("Native project is valid.");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        output.WriteLine($"Project ID: {load.Project.Manifest.Id}");
        return ScenarioEditorCommand.SuccessExitCode;
    }

    private static int Summary(
        IReadOnlyList<string> arguments,
        INativeProjectLoader loader,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error);
        }

        NativeProjectLoadResult load = loader.Load(arguments[1]);
        if (!load.IsSuccess || load.Project is null)
        {
            return WriteFailure(load, arguments[1], error);
        }

        NativeProject project = load.Project;
        output.WriteLine("Native project summary");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        output.WriteLine($"Project ID: {project.Manifest.Id}");
        output.WriteLine($"Scenario ID: {project.ScenarioBundle.Scenario.Id}");
        output.WriteLine($"Title: {project.ScenarioBundle.Scenario.Title}");
        output.WriteLine($"Map: {project.ScenarioBundle.Scenario.Map.Width} x {project.ScenarioBundle.Scenario.Map.Height}");
        output.WriteLine($"Content packages: {project.ScenarioBundle.ContentPackageIds.Count}");
        foreach (string packageId in project.ScenarioBundle.ContentPackageIds)
        {
            output.WriteLine($"- {packageId}");
        }

        output.WriteLine($"Session checkpoint: {(project.Session is null ? "not configured" : "valid")}");
        return ScenarioEditorCommand.SuccessExitCode;
    }

    private static int WriteFailure(NativeProjectLoadResult load, string path, TextWriter error)
    {
        error.WriteLine("Native project could not be loaded.");
        error.WriteLine($"File: {SafeName(path)}");
        foreach (NativeProjectLoadIssue issue in load.Issues)
        {
            error.WriteLine($"{issue.Code}/{issue.DetailCode} [{issue.InputLabel}:{issue.PropertyPath}]: {issue.Message}");
        }

        return ExitCodeFor(load.Issues);
    }

    private static int ExitCodeFor(IEnumerable<NativeProjectLoadIssue> issues)
    {
        string[] detailCodes = issues.Select(issue => issue.DetailCode).ToArray();
        if (detailCodes.Contains(nameof(NativeProjectPersistenceErrorCode.UnexpectedError), StringComparer.Ordinal) ||
            detailCodes.Contains("UnexpectedError", StringComparer.Ordinal))
        {
            return ScenarioEditorCommand.SoftwareErrorExitCode;
        }

        string[] inputErrors = ["InvalidPath", "FileNotFound", "FileInaccessible", "DocumentTooLarge"];
        return detailCodes.Any(code => inputErrors.Contains(code, StringComparer.Ordinal))
            ? ScenarioEditorCommand.InputErrorExitCode
            : ScenarioEditorCommand.ValidationErrorExitCode;
    }

    private static int UsageFailure(TextWriter error)
    {
        error.WriteLine("Unknown project command or invalid arguments.");
        error.WriteLine("Usage:");
        error.WriteLine("  validate-project <project-manifest>");
        error.WriteLine("  summary-project <project-manifest>");
        return ScenarioEditorCommand.UsageErrorExitCode;
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<project>" : name;
    }
}
