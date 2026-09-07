using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Editing;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Editor;

public static class ContentEditorCommand
{
    public static int Run(
        IReadOnlyList<string> arguments,
        IContentPackageFileStore contentStore,
        IScenarioFileStore scenarioStore,
        IContentPackageValidationService contentValidationService,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(contentStore);
        ArgumentNullException.ThrowIfNull(scenarioStore);
        ArgumentNullException.ThrowIfNull(contentValidationService);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            return arguments.FirstOrDefault() switch
            {
                "create-content" => CreateContent(arguments, contentStore, output, error),
                "set-content-name" => EditContent(
                    arguments,
                    3,
                    "set-content-name requires <package-file> <display-name> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.SetDisplayName(arguments[2]),
                    "Content package display name updated.",
                    output,
                    error),
                "add-terrain" => EditContent(
                    arguments,
                    4,
                    "add-terrain requires <package-file> <id> <display-name> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.AddTerrain(new TerrainContentDefinition(arguments[2], arguments[3])),
                    "Terrain declaration added.",
                    output,
                    error),
                "set-terrain-name" => EditContent(
                    arguments,
                    4,
                    "set-terrain-name requires <package-file> <id> <display-name> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.SetTerrainDisplayName(arguments[2], arguments[3]),
                    "Terrain display name updated.",
                    output,
                    error),
                "remove-terrain" => EditContent(
                    arguments,
                    3,
                    "remove-terrain requires <package-file> <id> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.RemoveTerrain(arguments[2]),
                    "Terrain declaration removed.",
                    output,
                    error),
                "add-object-archetype" => EditContent(
                    arguments,
                    4,
                    "add-object-archetype requires <package-file> <id> <display-name> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.AddObjectArchetype(new ObjectArchetypeDefinition(arguments[2], arguments[3])),
                    "Object archetype declaration added.",
                    output,
                    error),
                "set-object-archetype-name" => EditContent(
                    arguments,
                    4,
                    "set-object-archetype-name requires <package-file> <id> <display-name> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.SetObjectArchetypeDisplayName(arguments[2], arguments[3]),
                    "Object archetype display name updated.",
                    output,
                    error),
                "remove-object-archetype" => EditContent(
                    arguments,
                    3,
                    "remove-object-archetype requires <package-file> <id> [--output <file>].",
                    contentStore,
                    contentValidationService,
                    session => session.RemoveObjectArchetype(arguments[2]),
                    "Object archetype declaration removed.",
                    output,
                    error),
                "validate-content" => ValidateContent(arguments, contentStore, output, error),
                "validate-scenario-content" => ValidateScenarioContent(
                    arguments,
                    contentStore,
                    scenarioStore,
                    contentValidationService,
                    output,
                    error),
                _ => UsageFailure(error),
            };
        }
        catch (Exception)
        {
            error.WriteLine("Content validation failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return ScenarioEditorCommand.SoftwareErrorExitCode;
        }
    }

    private static int CreateContent(
        IReadOnlyList<string> arguments,
        IContentPackageFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count < 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error);
        }

        if (!TryParseCreateOptions(
            arguments.Skip(2).ToArray(),
            out string? packageId,
            out string? displayName,
            out bool force,
            out string? parseError))
        {
            return UsageFailure(error, parseError);
        }

        string path = arguments[1];
        if (File.Exists(path) && !force)
        {
            error.WriteLine("Content package output already exists.");
            error.WriteLine("Code: OutputAlreadyExists");
            error.WriteLine($"File: {SafeName(path)}");
            return ScenarioEditorCommand.InputErrorExitCode;
        }

        var package = new ContentPackageDefinition(
            ContentPackageFormatV1.Version,
            packageId!,
            displayName!,
            [],
            []);
        ContentPackageSaveResult save = store.Save(path, package);
        if (!save.IsSuccess)
        {
            return WriteContentSaveFailure(save, path, error);
        }

        output.WriteLine("Native content package created.");
        output.WriteLine($"File: {SafeName(path)}");
        output.WriteLine($"Package ID: {package.Id}");
        output.WriteLine($"Display name: {package.DisplayName}");
        return ScenarioEditorCommand.SuccessExitCode;
    }

    private static int EditContent(
        IReadOnlyList<string> arguments,
        int requiredCount,
        string usageMessage,
        IContentPackageFileStore store,
        IContentPackageValidationService validationService,
        Func<ContentPackageEditSession, ContentPackageEditResult> apply,
        string successMessage,
        TextWriter output,
        TextWriter error)
    {
        if ((arguments.Count != requiredCount && arguments.Count != requiredCount + 2) ||
            arguments.Take(requiredCount).Skip(1).Any(string.IsNullOrWhiteSpace) ||
            !TryParseOutput(arguments, requiredCount, out string? outputPath))
        {
            return UsageFailure(error, usageMessage);
        }

        ContentPackageLoadResult load = store.Load(arguments[1]);
        if (!load.IsSuccess || load.Package is null)
        {
            return WriteContentLoadFailure(load, arguments[1], error);
        }

        ContentPackageEditSessionCreationResult creation = ContentPackageEditSession.Create(
            load.Package,
            validationService);
        if (!creation.IsSuccess || creation.Session is null)
        {
            error.WriteLine(creation.Message ?? "Content package cannot be edited.");
            error.WriteLine("Code: ValidationFailed");
            WriteContentIssues(creation.ValidationIssues, error);
            return ScenarioEditorCommand.ValidationErrorExitCode;
        }

        ContentPackageEditSession session = creation.Session;
        ContentPackageEditResult edit = apply(session);
        if (!edit.IsSuccess)
        {
            error.WriteLine(edit.Message ?? "Content package edit was rejected.");
            error.WriteLine($"Code: {edit.Status}");
            WriteContentIssues(edit.ValidationIssues, error);

            return ScenarioEditorCommand.ValidationErrorExitCode;
        }

        string destination = outputPath ?? arguments[1];
        ContentPackageSaveResult save = store.Save(destination, edit.Package);
        if (!save.IsSuccess)
        {
            return WriteContentSaveFailure(save, destination, error);
        }

        output.WriteLine(successMessage);
        output.WriteLine($"File: {SafeName(destination)}");
        return ScenarioEditorCommand.SuccessExitCode;
    }

    private static int ValidateContent(
        IReadOnlyList<string> arguments,
        IContentPackageFileStore store,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count != 2 || string.IsNullOrWhiteSpace(arguments[1]))
        {
            return UsageFailure(error);
        }

        ContentPackageLoadResult load = store.Load(arguments[1]);
        if (!load.IsSuccess || load.Package is null)
        {
            return WriteContentLoadFailure(load, arguments[1], error);
        }

        output.WriteLine("Native content package is valid.");
        output.WriteLine($"File: {SafeName(arguments[1])}");
        output.WriteLine($"Package ID: {load.Package.Id}");
        output.WriteLine($"Terrains: {load.Package.Terrains.Count}");
        output.WriteLine($"Object archetypes: {load.Package.ObjectArchetypes.Count}");
        return ScenarioEditorCommand.SuccessExitCode;
    }

    private static int ValidateScenarioContent(
        IReadOnlyList<string> arguments,
        IContentPackageFileStore contentStore,
        IScenarioFileStore scenarioStore,
        IContentPackageValidationService contentValidationService,
        TextWriter output,
        TextWriter error)
    {
        if (arguments.Count < 3 || arguments.Skip(1).Any(string.IsNullOrWhiteSpace))
        {
            return UsageFailure(error);
        }

        ScenarioLoadResult scenarioLoad = scenarioStore.Load(arguments[1]);
        if (!scenarioLoad.IsSuccess || scenarioLoad.Scenario is null)
        {
            error.WriteLine("Native scenario could not be loaded.");
            error.WriteLine($"Code: {scenarioLoad.ErrorCode}");
            error.WriteLine($"File: {SafeName(arguments[1])}");
            return ExitCodeFor(scenarioLoad.ErrorCode);
        }

        List<ContentPackageDefinition?> packages = [];
        foreach (string path in arguments.Skip(2))
        {
            ContentPackageLoadResult packageLoad = contentStore.Load(path);
            if (!packageLoad.IsSuccess || packageLoad.Package is null)
            {
                return WriteContentLoadFailure(packageLoad, path, error);
            }

            packages.Add(packageLoad.Package);
        }

        ContentCatalogBuildResult catalogBuild = ContentCatalog.Build(packages, contentValidationService);
        if (!catalogBuild.IsSuccess || catalogBuild.Catalog is null)
        {
            error.WriteLine("Content catalog is invalid.");
            error.WriteLine("Code: CatalogValidationFailed");
            foreach (ContentCatalogBuildIssue issue in catalogBuild.Issues)
            {
                error.WriteLine($"{issue.Code} [{issue.PackageId}] {issue.PropertyPath}: {issue.Message}");
            }

            return ScenarioEditorCommand.ValidationErrorExitCode;
        }

        ScenarioContentValidationResult validation = new ScenarioContentValidationService()
            .ValidateReferences(scenarioLoad.Scenario, catalogBuild.Catalog);
        if (!validation.IsValid)
        {
            error.WriteLine("Scenario contains unresolved content references.");
            error.WriteLine("Code: ContentReferenceValidationFailed");
            foreach (ScenarioContentValidationIssue issue in validation.Issues)
            {
                error.WriteLine($"{issue.Code} [{issue.PropertyPath}]: {issue.ContentReference}");
            }

            return ScenarioEditorCommand.ValidationErrorExitCode;
        }

        output.WriteLine("Native scenario content references are valid.");
        output.WriteLine($"Scenario: {SafeName(arguments[1])}");
        output.WriteLine($"Packages: {packages.Count}");
        return ScenarioEditorCommand.SuccessExitCode;
    }

    private static int WriteContentLoadFailure(
        ContentPackageLoadResult result,
        string path,
        TextWriter error)
    {
        error.WriteLine("Native content package could not be loaded.");
        error.WriteLine($"Code: {result.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        WriteContentIssues(result.ValidationIssues, error);

        return result.ErrorCode is ContentPackagePersistenceErrorCode.InvalidJson or
            ContentPackagePersistenceErrorCode.ValidationFailed
                ? ScenarioEditorCommand.ValidationErrorExitCode
                : result.ErrorCode == ContentPackagePersistenceErrorCode.UnexpectedError
                    ? ScenarioEditorCommand.SoftwareErrorExitCode
                    : ScenarioEditorCommand.InputErrorExitCode;
    }

    private static int WriteContentSaveFailure(
        ContentPackageSaveResult result,
        string path,
        TextWriter error)
    {
        error.WriteLine("Native content package could not be saved.");
        error.WriteLine($"Code: {result.ErrorCode}");
        error.WriteLine($"File: {SafeName(path)}");
        WriteContentIssues(result.ValidationIssues, error);
        return ExitCodeFor(result.ErrorCode);
    }

    private static bool TryParseCreateOptions(
        IReadOnlyList<string> arguments,
        out string? packageId,
        out string? displayName,
        out bool force,
        out string? error)
    {
        packageId = null;
        displayName = null;
        force = false;
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

                force = true;
                continue;
            }

            if (argument is not ("--id" or "--display-name") ||
                index + 1 >= arguments.Count ||
                !seen.Add(argument))
            {
                error = $"Unknown, missing, or duplicate option: {argument}.";
                return false;
            }

            string value = arguments[++index];
            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"A value is required for {argument}.";
                return false;
            }

            if (string.Equals(argument, "--id", StringComparison.Ordinal))
            {
                packageId = value;
            }
            else
            {
                displayName = value;
            }
        }

        if (packageId is null || displayName is null)
        {
            error = "create-content requires --id and --display-name.";
            return false;
        }

        return true;
    }

    private static bool TryParseOutput(
        IReadOnlyList<string> arguments,
        int requiredCount,
        out string? outputPath)
    {
        outputPath = null;
        if (arguments.Count == requiredCount)
        {
            return true;
        }

        if (arguments.Count != requiredCount + 2 ||
            !string.Equals(arguments[requiredCount], "--output", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(arguments[requiredCount + 1]))
        {
            return false;
        }

        outputPath = arguments[requiredCount + 1];
        return true;
    }

    private static void WriteContentIssues(
        IEnumerable<ContentPackageValidationIssue> issues,
        TextWriter writer)
    {
        foreach (ContentPackageValidationIssue issue in issues)
        {
            writer.WriteLine($"{issue.Code} [{issue.PropertyPath}]: {issue.Message}");
        }
    }

    private static int ExitCodeFor(ContentPackagePersistenceErrorCode code) =>
        code switch
        {
            ContentPackagePersistenceErrorCode.InvalidJson or
            ContentPackagePersistenceErrorCode.ValidationFailed => ScenarioEditorCommand.ValidationErrorExitCode,
            ContentPackagePersistenceErrorCode.UnexpectedError => ScenarioEditorCommand.SoftwareErrorExitCode,
            _ => ScenarioEditorCommand.InputErrorExitCode,
        };

    private static int ExitCodeFor(ScenarioPersistenceErrorCode code) =>
        code switch
        {
            ScenarioPersistenceErrorCode.InvalidJson or
            ScenarioPersistenceErrorCode.ValidationFailed => ScenarioEditorCommand.ValidationErrorExitCode,
            ScenarioPersistenceErrorCode.UnexpectedError => ScenarioEditorCommand.SoftwareErrorExitCode,
            _ => ScenarioEditorCommand.InputErrorExitCode,
        };

    private static int UsageFailure(TextWriter error, string? message = null)
    {
        error.WriteLine(message ?? "Unknown content command or invalid arguments.");
        error.WriteLine("Usage:");
        error.WriteLine("  create-content <file> --id <id> --display-name <name> [--force]");
        error.WriteLine("  set-content-name <package-file> <display-name> [--output <file>]");
        error.WriteLine("  add-terrain <package-file> <id> <display-name> [--output <file>]");
        error.WriteLine("  set-terrain-name <package-file> <id> <display-name> [--output <file>]");
        error.WriteLine("  remove-terrain <package-file> <id> [--output <file>]");
        error.WriteLine("  add-object-archetype <package-file> <id> <display-name> [--output <file>]");
        error.WriteLine("  set-object-archetype-name <package-file> <id> <display-name> [--output <file>]");
        error.WriteLine("  remove-object-archetype <package-file> <id> [--output <file>]");
        error.WriteLine("  validate-content <package-file>");
        error.WriteLine("  validate-scenario-content <scenario-file> <package-file> [package-file ...]");
        return ScenarioEditorCommand.UsageErrorExitCode;
    }

    private static string SafeName(string path)
    {
        string name = Path.GetFileName(path.Trim());
        return string.IsNullOrWhiteSpace(name) ? "<document>" : name;
    }
}
