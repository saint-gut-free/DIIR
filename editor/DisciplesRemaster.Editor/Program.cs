using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Editor;
using DisciplesRemaster.Persistence.Scenarios;

var validationService = new ScenarioValidationService();
var serializer = new ScenarioJsonSerializer(validationService);
var store = new ScenarioFileStore(serializer);

return ScenarioEditorCommand.Run(args, store, Console.Out, Console.Error);
