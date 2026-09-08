# DisciplesRemaster.Godot

This project is the outer game-client adapter. It currently loads the project-owned native scenario JSON and produces deterministic immutable scene data: dimensions, terrain references, and inert object placements.

`NativeProjectSceneLoader` is the preferred composition boundary for a future host. It exposes scene data and an optional runtime checkpoint only after the portable manifest, scenario, every supplied content package, combined typed catalog, content references, and matching map dimensions have passed validation. It never invents fallback content. See [validated scenario loading](../../docs/architecture/validated-scenario-loading.md).

No Godot SDK was installed automatically. The current assembly deliberately remains buildable with the pinned .NET SDK so domain, content, persistence, and adapter contracts can evolve and be tested independently.

Until a compatible Godot .NET installation is available, the project is also a runnable headless host for project-owned runtime checkpoints:

```powershell
dotnet run --project game/DisciplesRemaster.Godot -- play-open-grid samples/synthetic/minimal.project.json
dotnet run --project game/DisciplesRemaster.Godot -- validate-project samples/synthetic/minimal.project.json
dotnet run --project game/DisciplesRemaster.Godot -- summary-project samples/synthetic/minimal.project.json
dotnet run --project game/DisciplesRemaster.Godot -- render-project samples/synthetic/minimal.project.json --width 20 --height 12
dotnet run --project game/DisciplesRemaster.Godot -- validate-session samples/synthetic/sessions/minimal-session.json
dotnet run --project game/DisciplesRemaster.Godot -- summary-session samples/synthetic/sessions/minimal-session.json
dotnet run --project game/DisciplesRemaster.Godot -- advance-turn samples/synthetic/sessions/minimal-session.json --output artifacts/runtime/after-turn.session.json
dotnet run --project game/DisciplesRemaster.Godot -- move-open-grid samples/synthetic/sessions/minimal-session.json blue-actor 2 1 --output artifacts/runtime/after-move.session.json
dotnet run --project game/DisciplesRemaster.Godot -- replay-open-grid samples/synthetic/sessions/minimal-session.json samples/synthetic/sessions/minimal-actions.json --output artifacts/runtime/replayed.session.json
```

`play-open-grid` starts an interactive in-memory session. Try `select blue-actor`, `preview 2 1`, `confirm`, `show`, and `end-turn`; `help` lists all commands and `quit` exits. Selection and preview belong to the outer adapter; movement and turns are still evaluated by Core. Confirmation rechecks the traversal policy, and rejected actions leave the checkpoint unchanged. Source documents are read only throughout this session. See [session interaction v1](../../docs/specifications/native-project-session-interaction-v1.md).

`move-open-grid` is intentionally explicit: it uses the project-owned orthogonal topology and treats every in-bounds position as passable. It is a test harness, not a claim about terrain, occupancy, or original-game movement rules. Omitting `--output` atomically updates the input checkpoint.

`replay-open-grid` applies a strict, bounded [action log v1](../../docs/specifications/game-session-action-log-v1.md) atomically. The complete batch is validated before execution, and a rejected action never produces a partial output checkpoint. Its input files remain unchanged.

`render-project` produces a bounded diagnostic text viewport from the already validated scene projection. Symbols distinguish default terrain, terrain overrides, inert objects, runtime actors, and layered cells without assigning graphics or gameplay semantics. The default viewport is `40 x 20`; use `--origin-x`, `--origin-y`, `--width`, and `--height` within the documented hard limits. See [diagnostic viewport v1](../../docs/specifications/native-project-diagnostic-viewport-v1.md).

The future Godot host will translate `ScenarioSceneData` into engine nodes and resources. Domain libraries must never reference Godot APIs.

`TODO-D2-RESEARCH`: before implementing compatibility gameplay, document which observed rules are confirmed and which mechanics are independent new-project design decisions.
