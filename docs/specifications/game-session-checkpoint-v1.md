# Game session checkpoint v1

This is a deterministic project-owned runtime checkpoint format. It is not an original-game `.sav` format and is not intended to preserve or imitate original save data.

## JSON structure

- `formatVersion`: integer `1`;
- `mapWidth`, `mapHeight`: positive grid dimensions;
- `participantIds`: ordered case-sensitive participant IDs;
- `activeParticipantIndex`: zero-based index into `participantIds`;
- `roundNumber`: positive integer;
- `actors`: runtime actors sorted by ID.

Each actor stores `id`, `ownerParticipantId`, `x`, `y`, `movementAllowance`, and `remainingMovement`. Remaining movement must be between zero and the allowance. IDs, ownership, positions, turn state, and movement values pass the same validation used by the engine-neutral runtime.

## Determinism and safety

JSON property names are camelCase, output uses two-space indentation and a final LF, and actor order is ordinal by ID. The document contains no timestamps, absolute paths, content bytes, or random identifiers. Unknown properties and unsupported versions are rejected. The file store limits documents to 16 MiB and writes through a temporary sibling followed by atomic replacement.

Use a project-owned `.session.json` suffix. The `.sav` suffix remains reserved for ignored original materials.

## Versioning

New required fields or semantic changes require a new format version and explicit migration. V1 stores only the minimal turn and movement state; actions, combat, economy, scenario scripting, and compatibility behavior are outside this format.

`TODO-D2-RESEARCH`: no field in this checkpoint may be described as compatible with original save behavior without separate reproducible evidence.
