using DisciplesRemaster.BinaryDiff;
using DisciplesRemaster.OriginalGame;

var locationProvider = new EnvironmentOriginalGameLocationProvider(new ProcessEnvironmentVariableReader());
return BinaryDiffCommand.Run(args, locationProvider, Console.Out, Console.Error);
