# DisciplesRemaster.Godot

This project is the outer game-client adapter. It currently loads the project-owned native scenario JSON and produces deterministic immutable scene data: dimensions, terrain references, and inert object placements.

`ValidatedScenarioSceneLoader` is the preferred composition boundary for a future host. It exposes scene data only after the scenario, every supplied content package, the combined typed catalog, and all scenario content references have passed validation. It never invents fallback content. See [validated scenario loading](../../docs/architecture/validated-scenario-loading.md).

No Godot SDK was installed automatically. The current assembly deliberately remains buildable with the pinned .NET SDK so domain, content, persistence, and adapter contracts can evolve and be tested independently.

The future Godot host will translate `ScenarioSceneData` into engine nodes and resources. Domain libraries must never reference Godot APIs.

`TODO-D2-RESEARCH`: before implementing compatibility gameplay, document which observed rules are confirmed and which mechanics are independent new-project design decisions.
