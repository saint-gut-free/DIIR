using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Sessions;

var store = new GameSessionFileStore(new GameSessionJsonSerializer());
var sessionService = new GameSessionService(new MovementPlanner(new GridPathfinder()));
return HeadlessGameCommand.Run(args, store, sessionService, Console.Out, Console.Error);
