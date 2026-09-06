using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Content;
using DisciplesRemaster.Persistence.Scenarios;

var validationService = new ScenarioValidationService();
var serializer = new ScenarioJsonSerializer(validationService);
var store = new ScenarioFileStore(serializer);
var contentValidation = new DisciplesRemaster.Content.Catalog.ContentPackageValidationService();
var contentStore = new ContentPackageFileStore(new ContentPackageJsonSerializer(contentValidation));

return args.FirstOrDefault() is "validate-content" or "validate-scenario-content"
    ? ContentEditorCommand.Run(args, contentStore, store, contentValidation, Console.Out, Console.Error)
    : ScenarioEditorCommand.Run(args, store, Console.Out, Console.Error);
