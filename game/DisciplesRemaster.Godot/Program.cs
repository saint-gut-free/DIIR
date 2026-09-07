using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Projects;
using DisciplesRemaster.Persistence.Scenarios;
using DisciplesRemaster.Persistence.Sessions;

var store = new GameSessionFileStore(new GameSessionJsonSerializer());
var sessionService = new GameSessionService(new MovementPlanner(new GridPathfinder()));
var actionProcessor = new GameSessionActionProcessor(sessionService);
var actionLogStore = new GameSessionActionLogFileStore(new GameSessionActionLogJsonSerializer());
var scenarioValidation = new ScenarioValidationService();
var contentValidation = new ContentPackageValidationService();
var projectLoader = new NativeProjectLoader(
    new NativeProjectManifestFileStore(
        new NativeProjectManifestJsonSerializer(new NativeProjectManifestValidationService())),
    new ScenarioBundleLoader(
        new ScenarioFileStore(new ScenarioJsonSerializer(scenarioValidation)),
        new ContentPackageFileStore(new ContentPackageJsonSerializer(contentValidation)),
        contentValidation,
        new ScenarioContentValidationService()),
    store);
var projectSceneLoader = new NativeProjectSceneLoader(projectLoader, scenarioValidation);

return args.FirstOrDefault() switch
{
    "validate-project" or "summary-project" or "render-project" =>
        HeadlessProjectCommand.Run(args, projectSceneLoader, Console.Out, Console.Error),
    "replay-open-grid" =>
        HeadlessReplayCommand.Run(args, store, actionLogStore, actionProcessor, Console.Out, Console.Error),
    _ => HeadlessGameCommand.Run(args, store, sessionService, Console.Out, Console.Error),
};
