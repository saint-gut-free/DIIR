using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Projects;
using DisciplesRemaster.Persistence.Scenarios;
using DisciplesRemaster.Persistence.Sessions;

var validationService = new ScenarioValidationService();
var serializer = new ScenarioJsonSerializer(validationService);
var store = new ScenarioFileStore(serializer);
var contentValidation = new DisciplesRemaster.Content.Catalog.ContentPackageValidationService();
var contentStore = new ContentPackageFileStore(new ContentPackageJsonSerializer(contentValidation));
var projectManifestValidation = new NativeProjectManifestValidationService();
var projectManifestStore = new NativeProjectManifestFileStore(
    new NativeProjectManifestJsonSerializer(projectManifestValidation));
var projectLoader = new NativeProjectLoader(
    projectManifestStore,
    new ScenarioBundleLoader(
        store,
        contentStore,
        contentValidation,
        new ScenarioContentValidationService()),
    new GameSessionFileStore(new GameSessionJsonSerializer()));

return args.FirstOrDefault() switch
{
    "create-content" or
    "set-content-name" or
    "add-terrain" or
    "set-terrain-name" or
    "remove-terrain" or
    "add-object-archetype" or
    "set-object-archetype-name" or
    "remove-object-archetype" or
    "validate-content" or
    "validate-scenario-content" =>
        ContentEditorCommand.Run(args, contentStore, store, contentValidation, Console.Out, Console.Error),
    "create-project" or "validate-project" or "summary-project" =>
        ProjectEditorCommand.Run(args, projectLoader, projectManifestStore, Console.Out, Console.Error),
    _ => ScenarioEditorCommand.Run(args, store, Console.Out, Console.Error),
};
