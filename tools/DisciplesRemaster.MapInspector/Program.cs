using DisciplesRemaster.MapInspector;
using DisciplesRemaster.OriginalGame.Research.MapExperiments;

return MapExperimentValidationCommand.Run(
    args,
    new MapExperimentValidationService(),
    Console.Out,
    Console.Error);
