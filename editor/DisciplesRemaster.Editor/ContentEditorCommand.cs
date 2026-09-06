using DisciplesRemaster.Content.Catalog;
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
        foreach (ContentPackageValidationIssue issue in result.ValidationIssues)
        {
            error.WriteLine($"{issue.Code} [{issue.PropertyPath}]: {issue.Message}");
        }

        return result.ErrorCode is ContentPackagePersistenceErrorCode.InvalidJson or
            ContentPackagePersistenceErrorCode.ValidationFailed
                ? ScenarioEditorCommand.ValidationErrorExitCode
                : result.ErrorCode == ContentPackagePersistenceErrorCode.UnexpectedError
                    ? ScenarioEditorCommand.SoftwareErrorExitCode
                    : ScenarioEditorCommand.InputErrorExitCode;
    }

    private static int ExitCodeFor(ScenarioPersistenceErrorCode code) =>
        code switch
        {
            ScenarioPersistenceErrorCode.InvalidJson or
            ScenarioPersistenceErrorCode.ValidationFailed => ScenarioEditorCommand.ValidationErrorExitCode,
            ScenarioPersistenceErrorCode.UnexpectedError => ScenarioEditorCommand.SoftwareErrorExitCode,
            _ => ScenarioEditorCommand.InputErrorExitCode,
        };

    private static int UsageFailure(TextWriter error)
    {
        error.WriteLine("Unknown content command or invalid arguments.");
        error.WriteLine("Usage:");
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
