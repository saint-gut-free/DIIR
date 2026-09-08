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
            if (arguments.Count == 0)
            {
                return UsageFailure(error);
            }

            NativeProjectTextRenderOptions? renderOptions = null;
            switch (arguments[0])
            {
                case "validate-project":
                case "summary-project":
                    if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
                    {
                        return UsageFailure(error);
                    }

                    break;
                case "render-project":
                    if (!TryParseRenderOptions(arguments, out renderOptions))
                    {
                        return UsageFailure(error);
                    }

                    break;
                default:
                    return UsageFailure(error);
            }

            NativeProjectSceneLoadResult load = loader.Load(arguments[1]);
            if (!load.IsSuccess || load.Project is null)
            {
                return WriteFailure(load, arguments[1], error);
            }

            return arguments[0] switch
            {
                "validate-project" => WriteValidation(load.Project, arguments[1], output),
                "summary-project" => WriteSummary(load.Project, arguments[1], output),
                _ => WriteRender(load.Project, renderOptions!, output, error),
            };
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

    private static int WriteRender(
        NativeProjectSceneData project,
        NativeProjectTextRenderOptions options,
        TextWriter output,
        TextWriter error)
    {
        NativeProjectTextRenderResult result = new NativeProjectTextRenderer().Render(project, options);
        if (!result.IsSuccess || result.Text is null)
        {
            error.WriteLine("Native project viewport could not be rendered.");
            foreach (NativeProjectTextRenderIssue issue in result.Issues)
            {
                error.WriteLine($"{issue.Code} [{issue.PropertyPath}]: {issue.Message}");
            }

            return HeadlessGameCommand.ValidationErrorExitCode;
        }

        output.Write(result.Text);
        return HeadlessGameCommand.SuccessExitCode;
    }

    private static bool TryParseRenderOptions(
        IReadOnlyList<string> arguments,
        out NativeProjectTextRenderOptions? options)
    {
        options = null;
        if (arguments.Count < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return false;
        }

        int originX = 0;
        int originY = 0;
        int width = NativeProjectTextRenderRules.DefaultViewportWidth;
        int height = NativeProjectTextRenderRules.DefaultViewportHeight;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 2; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (index + 1 >= arguments.Count || !seen.Add(argument))
            {
                return false;
            }

            string value = arguments[++index];
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                return false;
            }

            switch (argument)
            {
                case "--origin-x":
                    originX = number;
                    break;
                case "--origin-y":
                    originY = number;
                    break;
                case "--width":
                    width = number;
                    break;
                case "--height":
                    height = number;
                    break;
                default:
                    return false;
            }
        }

        if (originX < 0 ||
            originY < 0 ||
            width < 1 ||
            width > NativeProjectTextRenderRules.MaximumViewportWidth ||
            height < 1 ||
            height > NativeProjectTextRenderRules.MaximumViewportHeight)
        {
            return false;
        }

        options = new NativeProjectTextRenderOptions(originX, originY, width, height);
        return true;
    }

    internal static int WriteFailure(
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
        error.WriteLine("  render-project <project-manifest> [--origin-x <n>] [--origin-y <n>] [--width <n>] [--height <n>]");
        return HeadlessGameCommand.UsageErrorExitCode;
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<project>" : name;
    }
}
