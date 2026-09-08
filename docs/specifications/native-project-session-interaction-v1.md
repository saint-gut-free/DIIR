# Native project session interaction v1

The outer game adapter owns a single-threaded `NativeProjectSessionController` for selecting runtime actors and applying the existing project-owned turn and movement rules. It consumes a validated `NativeProjectSceneData` with an optional checkpoint. Core remains independent of the UI, the scenario format, and Godot.

## Interaction contract

- Any existing actor can be selected for inspection. An unknown ID leaves the previous selection intact.
- A successful selection or clear operation discards the previous movement preview.
- Preview delegates the movement request to `GameSessionService` and retains only the destination and plan. The existing immutable checkpoint remains unchanged.
- A rejected preview clears the previous preview. Ownership, available movement, bounds, traversal, and search limits are checked by Core.
- Confirm rechecks the movement against the current checkpoint and caller-supplied traversal policy. An outdated route is never applied without another check.
- Successful confirmation replaces only the runtime checkpoint in the current project. Terrain, inert objects, content references, and the caller's original project stay unchanged.
- Failed confirmation leaves the checkpoint unchanged and clears the preview.
- End turn delegates the existing round-robin rule to Core, resets selection and preview, and exposes the new active participant. Only that participant's movement is replenished.
- If the round counter would overflow, the operation is rejected atomically.
- A project without a runtime checkpoint returns `SessionUnavailable` for runtime operations. No actors or gameplay settings are invented from inert scenario objects.

The controller accepts explicit topology, traversal predicate, and search limit in its constructor. These dependencies are runtime design choices, not inferred terrain rules. It does not access files, clocks, network services, or engine objects.

## Interactive test host

```powershell
dotnet run --project game/DisciplesRemaster.Godot -- play-open-grid samples/synthetic/minimal.project.json
```

The host selects the existing orthogonal topology with every in-bounds cell enterable. It explicitly labels this as the project-owned open-grid policy. Runtime actors and inert scenario objects do not implicitly block movement.

A reproducible transcript for the tracked synthetic project:

```text
actors
select blue-actor
preview 2 1
confirm
show
end-turn
select red-actor
preview 5 4
confirm
status
quit
```

Commands are `actors`, `select <actor-id>`, `preview <x> <y>`, `confirm`, `clear`, `end-turn`, `show [x y width height]`, `status`, `help`, and `quit`. `show` uses the bounded [diagnostic viewport](native-project-diagnostic-viewport-v1.md). Command input is bounded to 1,024 characters, actor listing to 200 entries, and displayed routes to 32 positions. Oversized lines are discarded completely before the next command and cancel any existing movement preview. Invalid `preview` syntax also cancels the previous preview. No command text is executed as shell code.

All interactive changes stay in memory. Quit and EOF end the session without saving; original native input documents remain unchanged. Use the existing explicit checkpoint and replay commands for persisted test workflows. The current host provides console interaction; graphical input, animation, and a Godot window remain a separate milestone.

Top-level exit codes remain `0` for a normal session end, `2` for unavailable input, `3` for an invalid project or missing checkpoint, `64` for invalid invocation, and `70` for an unexpected failure. Rejected commands inside the running session return structured feedback and allow the user to correct the input.

`TODO-D2-RESEARCH`: original-game movement, occupancy, terrain effects, selection rules, and turn behavior require independent evidence before making compatibility claims. This interaction layer only applies the already specified project-owned rules.
