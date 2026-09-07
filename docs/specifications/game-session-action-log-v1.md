# Game session action log v1

This project-owned format records a bounded ordered batch of commands for the minimal immutable headless runtime. It supports deterministic tests, replay, and future input adapters. It is not an original-game replay or command format.

## Document shape

```json
{
  "formatVersion": 1,
  "actions": [
    {
      "sequence": 1,
      "kind": "moveActor",
      "actorId": "blue-actor",
      "x": 2,
      "y": 1
    },
    {
      "sequence": 2,
      "kind": "advanceTurn",
      "actorId": null,
      "x": null,
      "y": null
    }
  ]
}
```

`formatVersion` must be `1`. `actions` is required and may be empty. Sequence numbers must be contiguous, preserve document order, and start at `1`.

Version 1 supports only:

- `advanceTurn`, with no actor or destination;
- `moveActor`, with an actor ID and integer `x`/`y` destination.

The document is limited to 16 MiB and 10,000 actions. Actor IDs use the checkpoint limit of 96 characters. Unknown JSON properties, unknown action kinds, incomplete coordinates, invalid shapes, and non-contiguous sequences are rejected. Deterministic serialization uses the order defined by `sequence`, stable camel-case property names, no timestamp or random ID, and one trailing line feed.

## Atomic application

`GameSessionActionProcessor` first validates the complete batch, then applies actions to a local immutable session value. A rejected action returns no output session, so callers cannot accidentally persist a partially applied batch. The result still reports how many preceding actions were evaluated successfully for diagnostics.

Movement topology and passability are explicit processor inputs and are not encoded as hidden global rules. The current `replay-open-grid` CLI deliberately supplies orthogonal topology and an all-open in-bounds predicate. Its name and output identify this as a test harness. Future production gameplay must supply independently specified world rules.

```powershell
dotnet run --project game/DisciplesRemaster.Godot -- replay-open-grid `
  samples/synthetic/sessions/minimal-session.json `
  samples/synthetic/sessions/minimal-actions.json `
  --output artifacts/runtime/replayed.session.json
```

The input checkpoint and action log are opened read-only. The explicit output must differ from both inputs and is written using the existing atomic checkpoint store. CLI diagnostics show safe filenames rather than absolute paths.

`TODO-D2-RESEARCH`: compatibility of turns, movement, command ordering, passability, occupancy, costs, or any other action semantics with Disciples II requires separate reproducible evidence. This v1 contract is independent project design.
