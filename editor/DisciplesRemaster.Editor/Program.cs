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
var projectLoader = new NativeProjectLoader(
    new NativeProjectManifestFileStore(new NativeProjectManifestJsonSerializer(projectManifestValidation)),
    new ScenarioBundleLoader(
        store,
        contentStore,
        contentValidation,
        new ScenarioContentValidationService()),
    new GameSessionFileStore(new GameSessionJsonSerializer()));

return args.FirstOrDefault() switch
{
    "validate-content" or "validate-scenario-content" =>
        ContentEditorCommand.Run(args, contentStore, store, contentValidation, Console.Out, Console.Error),
    "validate-project" or "summary-project" =>
        ProjectEditorCommand.Run(args, projectLoader, Console.Out, Console.Error),
    _ => ScenarioEditorCommand.Run(args, store, Console.Out, Console.Error),
};
