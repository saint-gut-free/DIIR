using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Editor;

public static class ScenarioEditorCommand
{
    public const int SuccessExitCode = 0;
    public const int InputErrorExitCode = 2;
    public const int ValidationErrorExitCode = 3;
    public const int UsageErrorExitCode = 64;
    public const int SoftwareErrorExitCode = 70;

    public static int Run(
        IReadOnlyList<string> arguments,
        IScenarioFileStore store,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return arguments.Count == 0
                ? UsageFailure(error, "A command is required.")
                : arguments[0] switch
                {
                    "create" => Create(arguments, store, output, error),
                    "validate" => Validate(arguments, store, output, error),
                    "summary" => Summary(arguments, store, output, error),
                    "paint-terrain" => PaintTerrain(arguments, store, output, error),
                    _ => UsageFailure(error, "Unknown command."),
                };
        }
        catch (Exception)
        {
            error.WriteLine("Scenario editor command failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return SoftwareErrorExitCode;
        }
    }

    private static int Create(
        IReadOnlyList<string> arguments,
        IScenarioFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error, "create requires an output file.");
        }

        if (!TryParseCreateOptions(arguments.Skip(2).ToArray(), out CreateOptions? options, out string? parseError))
        {
            return UsageFailure(error, parseError!);
        }

        CreateOptions parsedOptions = options!;
        string path = arguments[1];
        if (File.Exists(path) && !parsedOptions.Force)
        {
            error.WriteLine("Scenario output already exists.");
            error.WriteLine("Code: OutputAlreadyExists");
            error.WriteLine($"File: {SafeName(path)}");
            return InputErrorExitCode;
        }

        var scenario = new ScenarioDefinition(
            ScenarioFormatV1.Version,
            parsedOptions.Id!,
            parsedOptions.Title!,
            parsedOptions.Description,
            new ScenarioMapDefinition(
                parsedOptions.Width,
                parsedOptions.Height,
                parsedOptions.DefaultTerrain!,
                []));

        ScenarioSaveResult result = store.Save(path, scenario);
        if (!result.IsSuccess)
        {
            return WriteSaveFailure(result, path, error);
        }

        output.WriteLine("Native scenario created.");
        output.WriteLine($"File: {SafeName(path)}");
        output.WriteLine($"Scenario ID: {scenario.Id}");
        output.WriteLine($"Map: {scenario.Map.Width} x {scenario.Map.Height}");
        output.WriteLine($"Default terrain: {scenario.Map.DefaultTerrain}");
        return SuccessExitCode;
    }

    private static int Validate(
        IReadOnlyList<string> arguments,
        IScenarioFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error, "validate requires one scenario file.");
        }

        ScenarioLoadResult result = store.Load(arguments[1]);
        if (!result.IsSuccess)
        {
            return WriteLoadFailure(result, arguments[1], error);
        }

        output.WriteLine("Native scenario is valid.");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        WriteWarnings(result.ValidationIssues, output);
        return SuccessExitCode;
    }

    private static int Summary(
        IReadOnlyList<string> arguments,
        IScenarioFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error, "summary requires one scenario file.");
        }

        ScenarioLoadResult result = store.Load(arguments[1]);
        if (!result.IsSuccess || result.Scenario is null)
        {
            return WriteLoadFailure(result, arguments[1], error);
        }

        ScenarioDefinition scenario = result.Scenario;
        output.WriteLine("Native scenario summary");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        output.WriteLine($"Format version: {scenario.FormatVersion}");
        output.WriteLine($"Scenario ID: {scenario.Id}");
        output.WriteLine($"Title: {scenario.Title}");
        output.WriteLine($"Map: {scenario.Map.Width} x {scenario.Map.Height}");
        output.WriteLine($"Default terrain: {scenario.Map.DefaultTerrain}");
        output.WriteLine($"Terrain overrides: {scenario.Map.Terrain.Count}");
        WriteWarnings(result.ValidationIssues, output);
        return SuccessExitCode;
    }

    private static int PaintTerrain(
        IReadOnlyList<string> arguments,
        IScenarioFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (!TryParsePaintArguments(arguments, out PaintOptions? options, out string? parseError))
        {
            return UsageFailure(error, parseError!);
        }

        PaintOptions parsedOptions = options!;
        ScenarioLoadResult load = store.Load(parsedOptions.InputPath!);
        if (!load.IsSuccess || load.Scenario is null)
        {
            return WriteLoadFailure(load, parsedOptions.InputPath!, error);
        }

        ScenarioDefinition source = load.Scenario;
        var position = new GridPosition(parsedOptions.X, parsedOptions.Y);
        if (source.Map.Width <= 0 || source.Map.Height <= 0 ||
            !new GridSize(source.Map.Width, source.Map.Height).Contains(position))
        {
            error.WriteLine("Terrain position is outside the map.");
            error.WriteLine("Code: TerrainPlacementOutsideMap");
            return ValidationErrorExitCode;
        }

        List<TerrainPlacement> terrain = source.Map.Terrain
            .Where(placement => placement.Position != position)
            .ToList();
        if (!string.Equals(parsedOptions.Terrain, source.Map.DefaultTerrain, StringComparison.Ordinal))
        {
            terrain.Add(new TerrainPlacement(position, parsedOptions.Terrain!));
        }

        ScenarioDefinition updated = source with
        {
            Map = source.Map with { Terrain = terrain },
        };
        string outputPath = parsedOptions.OutputPath ?? parsedOptions.InputPath!;
        ScenarioSaveResult save = store.Save(outputPath, updated);
        if (!save.IsSuccess)
        {
            return WriteSaveFailure(save, outputPath, error);
        }

        output.WriteLine("Terrain override updated.");
        output.WriteLine($"File: {SafeName(outputPath)}");
        output.WriteLine($"Position: {parsedOptions.X}, {parsedOptions.Y}");
        output.WriteLine($"Terrain: {parsedOptions.Terrain}");
        output.WriteLine($"Overrides: {updated.Map.Terrain.Count}");
        WriteWarnings(save.ValidationIssues, output);
        return SuccessExitCode;
    }

    private static bool TryParseCreateOptions(
        IReadOnlyList<string> arguments,
        out CreateOptions? options,
        out string? error)
    {
        options = new CreateOptions();
        error = null;
        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int index = 0; index < arguments.Count; index++)
        {
            string argument = arguments[index];
            if (string.Equals(argument, "--force", StringComparison.Ordinal))
            {
                if (!seen.Add(argument))
                {
                    error = "Duplicate option: --force.";
                    return false;
                }

                options.Force = true;
                continue;
            }

            if (index + 1 >= arguments.Count || !seen.Add(argument))
            {
                error = $"Missing or duplicate value for {argument}.";
                return false;
            }

            string value = arguments[++index];
            switch (argument)
            {
                case "--id":
                    options.Id = value;
                    break;
                case "--title":
                    options.Title = value;
                    break;
                case "--description":
                    options.Description = value;
                    break;
                case "--width" when int.TryParse(value, out int width):
                    options.Width = width;
                    break;
                case "--height" when int.TryParse(value, out int height):
                    options.Height = height;
                    break;
                case "--default-terrain":
                    options.DefaultTerrain = value;
                    break;
                default:
                    error = $"Unknown option or invalid value: {argument}.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(options.Id) ||
            string.IsNullOrWhiteSpace(options.Title) ||
            string.IsNullOrWhiteSpace(options.DefaultTerrain) ||
            options.Width == 0 ||
            options.Height == 0)
        {
            error = "create requires --id, --title, --width, --height, and --default-terrain.";
            return false;
        }

        return true;
    }

    private static bool TryParsePaintArguments(
        IReadOnlyList<string> arguments,
        out PaintOptions? options,
        out string? error)
    {
        options = null;
        error = null;
        if (arguments.Count is not (5 or 7) ||
            string.IsNullOrWhiteSpace(arguments[1]) ||
            !int.TryParse(arguments[2], out int x) ||
            !int.TryParse(arguments[3], out int y) ||
            string.IsNullOrWhiteSpace(arguments[4]))
        {
            error = "paint-terrain requires <file> <x> <y> <content-reference>.";
            return false;
        }

        string? outputPath = null;
        if (arguments.Count == 7)
        {
            if (!string.Equals(arguments[5], "--output", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(arguments[6]))
            {
                error = "paint-terrain accepts only --output <file> after the content reference.";
                return false;
            }

            outputPath = arguments[6];
        }

        options = new PaintOptions(arguments[1], x, y, arguments[4], outputPath);
        return true;
    }

    private static int WriteLoadFailure(ScenarioLoadResult result, string path, TextWriter error)
    {
        error.WriteLine("Native scenario could not be loaded.");
        error.WriteLine($"Code: {result.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        WriteIssues(result.ValidationIssues, error);
        return ExitCodeFor(result.ErrorCode);
    }

    private static int WriteSaveFailure(ScenarioSaveResult result, string path, TextWriter error)
    {
        error.WriteLine("Native scenario could not be saved.");
        error.WriteLine($"Code: {result.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        WriteIssues(result.ValidationIssues, error);
        return ExitCodeFor(result.ErrorCode);
    }

    private static int ExitCodeFor(ScenarioPersistenceErrorCode code) =>
        code switch
        {
            ScenarioPersistenceErrorCode.InvalidJson or
            ScenarioPersistenceErrorCode.ValidationFailed => ValidationErrorExitCode,
            ScenarioPersistenceErrorCode.UnexpectedError => SoftwareErrorExitCode,
            _ => InputErrorExitCode,
        };

    private static void WriteWarnings(IEnumerable<ScenarioValidationIssue> issues, TextWriter output)
    {
        ScenarioValidationIssue[] warnings = issues
            .Where(issue => issue.Severity == ScenarioValidationSeverity.Warning)
            .ToArray();
        if (warnings.Length == 0)
        {
            return;
        }

        output.WriteLine($"Warnings: {warnings.Length}");
        WriteIssues(warnings, output);
    }

    private static void WriteIssues(IEnumerable<ScenarioValidationIssue> issues, TextWriter writer)
    {
        foreach (ScenarioValidationIssue issue in issues)
        {
            writer.WriteLine($"{issue.Severity} {issue.Code} [{issue.PropertyPath}]: {issue.Message}");
        }
    }

    private static int UsageFailure(TextWriter error, string message)
    {
        error.WriteLine(message);
        WriteUsage(error);
        return UsageErrorExitCode;
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<scenario>" : name;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  create <file> --id <id> --title <title> --width <n> --height <n> --default-terrain <namespace:name> [--description <text>] [--force]");
        writer.WriteLine("  validate <file>");
        writer.WriteLine("  summary <file>");
        writer.WriteLine("  paint-terrain <file> <x> <y> <namespace:name> [--output <file>]");
    }

    private sealed class CreateOptions
    {
        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string? DefaultTerrain { get; set; }

        public bool Force { get; set; }
    }

    private sealed record PaintOptions(
        string? InputPath,
        int X,
        int Y,
        string? Terrain,
        string? OutputPath);
}
