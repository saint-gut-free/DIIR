# Native scenario edit history

`ScenarioEditSession` is the engine-neutral application model for future graphical editing. It works only with the project-owned `ScenarioDefinition` and does not read or write files itself.

Supported v1 operations are:

- paint or clear a sparse terrain override;
- update title and default terrain;
- resize the map without silently clipping placements;
- place an inert object;
- move an inert object;
- remove an inert object;
- undo and redo.

Every applied edit produces a defensively copied scenario snapshot and passes structural validation before replacing the current state. Invalid edits leave both the document and history unchanged. No-op edits are successful but do not create history entries.

Undo and redo history is bounded (100 snapshots by default, at most 1,000). Applying a new edit after undo clears the redo branch. The session never performs persistence; a host explicitly saves `Current` through `IScenarioFileStore` when appropriate.

This component does not parse `.sg`, run the original editor, infer game behavior, or define graphical UI behavior.
