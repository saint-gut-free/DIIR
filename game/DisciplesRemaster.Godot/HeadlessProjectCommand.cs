using System.Globalization;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Godot;

public static class HeadlessProjectCommand
{
    public static int Run(
        IReadOnlyList<string> arguments,
        NativeProjectSceneLoader loader,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (arguments.Count != 2 ||
                arguments.FirstOrDefault() is not ("validate-project" or "summary-project") ||
                string.IsNullOrWhiteSpace(arguments[1]))
            {
                return UsageFailure(error);
            }

            NativeProjectSceneLoadResult load = loader.Load(arguments[1]);
            if (!load.IsSuccess || load.Project is null)
            {
                return WriteFailure(load, arguments[1], error);
            }

            return arguments[0] == "validate-project"
                ? WriteValidation(load.Project, arguments[1], output)
                : WriteSummary(load.Project, arguments[1], output);
        }
        catch (Exception)
        {
            error.WriteLine("Headless project host failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return HeadlessGameCommand.SoftwareErrorExitCode;
        }
    }

    private static int WriteValidation(
        NativeProjectSceneData project,
        string path,
        TextWriter output)
    {
        output.WriteLine("Native game project is valid and ready for an outer host.");
        output.WriteLine($"File: {SafeName(path)}");
        output.WriteLine($"Project ID: {project.ProjectId}");
        return HeadlessGameCommand.SuccessExitCode;
    }

    private static int WriteSummary(
        NativeProjectSceneData project,
        string path,
        TextWriter output)
    {
        output.WriteLine("Headless native project summary");
        output.WriteLine($"File: {SafeName(path)}");
        output.WriteLine($"Project ID: {project.ProjectId}");
        output.WriteLine($"Scenario ID: {project.Scene.ScenarioId}");
        output.WriteLine($"Title: {project.Scene.Title}");
        output.WriteLine($"Map: {project.Scene.Size.Width} x {project.Scene.Size.Height}");
        output.WriteLine($"Terrain overrides: {project.Scene.TerrainOverrides.Count.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Scene objects: {project.Scene.Objects.Count.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Content packages: {project.ContentPackageIds.Count.ToString(CultureInfo.InvariantCulture)}");
        output.WriteLine($"Runtime checkpoint: {(project.Session is null ? "not configured" : "valid")}");
        if (project.Session is not null)
        {
            output.WriteLine($"Round: {project.Session.Turn.RoundNumber.ToString(CultureInfo.InvariantCulture)}");
            output.WriteLine($"Active participant: {project.Session.Turn.ActiveParticipantId}");
            output.WriteLine($"Runtime actors: {project.Session.Actors.Count.ToString(CultureInfo.InvariantCulture)}");
        }

        return HeadlessGameCommand.SuccessExitCode;
    }

    private static int WriteFailure(
        NativeProjectSceneLoadResult load,
        string path,
        TextWriter error)
    {
        error.WriteLine("Native game project could not be loaded.");
        error.WriteLine($"File: {SafeName(path)}");
        foreach (NativeProjectLoadIssue issue in load.Issues)
        {
            error.WriteLine($"{issue.Code}/{issue.DetailCode} [{issue.InputLabel}:{issue.PropertyPath}]: {issue.Message}");
        }

        return ExitCodeFor(load.Issues);
    }

    private static int ExitCodeFor(IEnumerable<NativeProjectLoadIssue> issues)
    {
        string[] details = issues.Select(issue => issue.DetailCode).ToArray();
        if (details.Contains("UnexpectedError", StringComparer.Ordinal))
        {
            return HeadlessGameCommand.SoftwareErrorExitCode;
        }

        string[] inputErrors = ["InvalidPath", "FileNotFound", "FileInaccessible", "DocumentTooLarge"];
        return details.Any(code => inputErrors.Contains(code, StringComparer.Ordinal))
            ? HeadlessGameCommand.InputErrorExitCode
            : HeadlessGameCommand.ValidationErrorExitCode;
    }

    private static int UsageFailure(TextWriter error)
    {
        error.WriteLine("Unknown project command or invalid arguments.");
        error.WriteLine("Usage:");
        error.WriteLine("  validate-project <project-manifest>");
        error.WriteLine("  summary-project <project-manifest>");
        return HeadlessGameCommand.UsageErrorExitCode;
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<project>" : name;
    }
}
