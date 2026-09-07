using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Editor;

public static class ProjectEditorCommand
{
    public static int Run(
        IReadOnlyList<string> arguments,
        INativeProjectLoader loader,
        INativeProjectManifestFileStore manifestStore,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(manifestStore);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return arguments.FirstOrDefault() switch
            {
                "create-project" => Create(arguments, manifestStore, output, error),
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

    private static int Create(
        IReadOnlyList<string> arguments,
        INativeProjectManifestFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error);
        }

        if (!TryParseCreateOptions(
            arguments.Skip(2).ToArray(),
            out CreateProjectOptions? options,
            out string? parseError))
        {
            return UsageFailure(error, parseError);
        }

        CreateProjectOptions parsed = options!;
        string path = arguments[1];
        if (File.Exists(path) && !parsed.Force)
        {
            error.WriteLine("Project manifest output already exists.");
            error.WriteLine("Code: OutputAlreadyExists");
            error.WriteLine($"File: {SafeName(path)}");
            return ScenarioEditorCommand.InputErrorExitCode;
        }

        var manifest = new NativeProjectManifest(
            NativeProjectManifestFormatV1.Version,
            parsed.Id!,
            parsed.Scenario!,
            parsed.ContentPackages,
            parsed.Session);
        NativeProjectManifestSaveResult save = store.Save(path, manifest);
        if (!save.IsSuccess)
        {
            error.WriteLine("Native project manifest could not be saved.");
            error.WriteLine($"Code: {save.ErrorCode}");
            error.WriteLine($"File: {SafeName(path)}");
            foreach (NativeProjectManifestValidationIssue issue in save.Issues)
            {
                error.WriteLine($"{issue.Code} [{issue.PropertyPath}]: {issue.Message}");
            }

            return ExitCodeFor(save.ErrorCode);
        }

        output.WriteLine("Native project manifest created.");
        output.WriteLine($"File: {SafeName(path)}");
        output.WriteLine($"Project ID: {manifest.Id}");
        output.WriteLine($"Content packages: {manifest.ContentPackages.Count}");
        output.WriteLine($"Session checkpoint: {(manifest.Session is null ? "not configured" : "configured")}");
        return ScenarioEditorCommand.SuccessExitCode;
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

    private static bool TryParseCreateOptions(
        IReadOnlyList<string> arguments,
        out CreateProjectOptions? options,
        out string? error)
    {
        options = new CreateProjectOptions();
        error = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Count; index++)
        {
            string option = arguments[index];
            if (option == "--force")
            {
                if (!seen.Add(option))
                {
                    error = "Duplicate option: --force.";
                    return false;
                }

                options.Force = true;
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                error = $"Missing value for {option}.";
                return false;
            }

            string value = arguments[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Empty value for {option}.";
                return false;
            }

            if (option != "--content" && !seen.Add(option))
            {
                error = $"Duplicate option: {option}.";
                return false;
            }

            switch (option)
            {
                case "--id":
                    options.Id = value;
                    break;
                case "--scenario":
                    options.Scenario = value;
                    break;
                case "--content":
                    options.ContentPackages.Add(value);
                    break;
                case "--session":
                    options.Session = value;
                    break;
                default:
                    error = $"Unknown option: {option}.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(options.Id) ||
            string.IsNullOrWhiteSpace(options.Scenario) ||
            options.ContentPackages.Count == 0)
        {
            error = "create-project requires --id, --scenario, and at least one --content value.";
            return false;
        }

        return true;
    }

    private static int ExitCodeFor(NativeProjectPersistenceErrorCode code) =>
        code switch
        {
            NativeProjectPersistenceErrorCode.ValidationFailed or
            NativeProjectPersistenceErrorCode.InvalidJson => ScenarioEditorCommand.ValidationErrorExitCode,
            NativeProjectPersistenceErrorCode.UnexpectedError => ScenarioEditorCommand.SoftwareErrorExitCode,
            _ => ScenarioEditorCommand.InputErrorExitCode,
        };

    private static int UsageFailure(TextWriter error, string? detail = null)
    {
        error.WriteLine(detail ?? "Unknown project command or invalid arguments.");
        error.WriteLine("Usage:");
        error.WriteLine("  create-project <manifest> --id <id> --scenario <relative-path> --content <relative-path> [--content <relative-path> ...] [--session <relative-path>] [--force]");
        error.WriteLine("  validate-project <project-manifest>");
        error.WriteLine("  summary-project <project-manifest>");
        return ScenarioEditorCommand.UsageErrorExitCode;
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<project>" : name;
    }

    private sealed class CreateProjectOptions
    {
        public string? Id { get; set; }

        public string? Scenario { get; set; }

        public List<string> ContentPackages { get; } = [];

        public string? Session { get; set; }

        public bool Force { get; set; }
    }
}
