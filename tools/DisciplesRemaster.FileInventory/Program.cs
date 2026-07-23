using DisciplesRemaster.FileInventory;
using DisciplesRemaster.OriginalGame;

var provider = new EnvironmentOriginalGameLocationProvider(new ProcessEnvironmentVariableReader());
return FileInventoryCommand.Run(args, provider, Console.Out, Console.Error);
