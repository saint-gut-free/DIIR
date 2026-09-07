# DisciplesRemaster.Godot

This project is the outer game-client adapter. It currently loads the project-owned native scenario JSON and produces deterministic immutable scene data: dimensions, terrain references, and inert object placements.

`ValidatedScenarioSceneLoader` is the preferred composition boundary for a future host. It exposes scene data only after the scenario, every supplied content package, the combined typed catalog, and all scenario content references have passed validation. It never invents fallback content. See [validated scenario loading](../../docs/architecture/validated-scenario-loading.md).

No Godot SDK was installed automatically. The current assembly deliberately remains buildable with the pinned .NET SDK so domain, content, persistence, and adapter contracts can evolve and be tested independently.

Until a compatible Godot .NET installation is available, the project is also a runnable headless host for project-owned runtime checkpoints:

```powershell
dotnet run --project game/DisciplesRemaster.Godot -- validate-session samples/synthetic/sessions/minimal-session.json
dotnet run --project game/DisciplesRemaster.Godot -- summary-session samples/synthetic/sessions/minimal-session.json
dotnet run --project game/DisciplesRemaster.Godot -- advance-turn samples/synthetic/sessions/minimal-session.json --output artifacts/runtime/after-turn.session.json
dotnet run --project game/DisciplesRemaster.Godot -- move-open-grid samples/synthetic/sessions/minimal-session.json blue-actor 2 1 --output artifacts/runtime/after-move.session.json
```

`move-open-grid` is intentionally explicit: it uses the project-owned orthogonal topology and treats every in-bounds position as passable. It is a test harness, not a claim about terrain, occupancy, or original-game movement rules. Omitting `--output` atomically updates the input checkpoint.

The future Godot host will translate `ScenarioSceneData` into engine nodes and resources. Domain libraries must never reference Godot APIs.

`TODO-D2-RESEARCH`: before implementing compatibility gameplay, document which observed rules are confirmed and which mechanics are independent new-project design decisions.
