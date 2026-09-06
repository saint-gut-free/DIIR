# DisciplesRemaster.Godot

This project is the outer game-client adapter. It currently loads the project-owned native scenario JSON and produces deterministic immutable scene data: dimensions, terrain references, and inert object placements.

No Godot SDK was installed automatically. The current assembly deliberately remains buildable with the pinned .NET SDK so domain, content, persistence, and adapter contracts can evolve and be tested independently.

The future Godot host will translate `ScenarioSceneData` into engine nodes and resources. Domain libraries must never reference Godot APIs.

`TODO-D2-RESEARCH`: before implementing compatibility gameplay, document which observed rules are confirmed and which mechanics are independent new-project design decisions.
