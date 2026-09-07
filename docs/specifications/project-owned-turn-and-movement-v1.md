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
