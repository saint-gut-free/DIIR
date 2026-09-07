# Project-owned turn and movement rules v1

This specification defines a deliberately small independent gameplay primitive. It is not a statement about Disciples II behavior.

`TODO-D2-RESEARCH`: compatibility claims about original turn sequencing, movement costs, terrain effects, blocking, parties, actions, or round transitions require separate documented evidence.

## Round turn sequence

- A sequence contains one to 1,024 unique, case-sensitive participant IDs.
- Declared order is authoritative and remains unchanged.
- The first participant is active in round 1.
- Advancing selects the next participant.
- Advancing past the final participant selects the first participant and increments the round.
- State is immutable: advancing returns a new value and does not mutate prior state.
- Removing, inserting, skipping, defeating, or controlling participants is outside v1.

## Movement plan

- Grid size, start, destination, topology, passability predicate, and budget are explicit inputs.
- In v1 every traversed topology edge costs exactly one movement point.
- A zero budget permits a plan whose start equals its destination.
- A negative budget is invalid.
- Pathfinding remains deterministic and bounded by `maximumVisitedPositions`.
- If a path exists but exceeds the budget, the plan reports `MovementBudgetExceeded` and the required bounded route; it does not apply movement.
- Planning has no side effects. Applying movement to game state is outside v1.
- Weighted terrain, zones of control, party composition, combat engagement, diagonal movement, and other game rules are outside v1.

These primitives remain in `DisciplesRemaster.Core`, have no dependency on content, persistence, Godot, or editor code, and can be replaced or extended only through a versioned design decision.

## Minimal game session

`GameSessionState` composes these primitives without changing the native scenario format. Session setup explicitly supplies:

- map size;
- ordered participant IDs;
- actor ID, owner participant ID, position, and non-negative movement allowance.

The session validates ownership, positions, and unique actor IDs before it is created. Only an actor owned by the active participant may move. A successful move returns a new immutable session and spends the route's step cost; a rejected move returns no updated state. Advancing the turn resets movement only for actors of the newly active participant.

The caller supplies the passability predicate for each movement request. V1 does not implicitly block occupied actor positions or assign terrain costs; those policies must remain explicit until separately designed.

Session setup is intentionally separate from native scenario v1 because that format defines inert placements and makes no ownership or gameplay claim. A later versioned scenario/runtime composition contract may map project content into actors after those rules are designed.
