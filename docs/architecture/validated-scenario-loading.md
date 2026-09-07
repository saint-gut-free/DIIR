# Validated scenario loading

The playable client boundary must never receive a native scenario whose content references are unresolved. Loading therefore follows one explicit composition flow:

```text
native project manifest ─> portable path validation ─┐
                                                      │
native scenario JSON ─────> structural validation ────┤
                                                      ├─> validated native project
content package JSON ─────> typed catalog validation ─┤        │
                                                      │        └─> immutable scene data
optional checkpoint JSON ─> runtime validation ───────┘
```

`ScenarioBundleLoader` lives in `DisciplesRemaster.Persistence`. It coordinates the existing scenario and content-package stores, builds a `ContentCatalog`, and validates every terrain and object-archetype reference. It does not add fallback entries or infer missing content.

Failures are returned as sorted `ScenarioBundleLoadIssue` values. `InputLabel` uses stable labels such as `scenario` and `content[0]`; messages do not include local paths. A failed load never exposes a partial bundle.

`ValidatedScenarioSceneLoader` is an outer adapter in `game/`. It projects a successful bundle into deterministic immutable `ScenarioSceneData`. A future Godot host may create engine nodes from that data, while all JSON, catalog, and domain validation remains independent of Godot APIs.

`NativeProjectLoader` resolves the project-owned manifest relative to its directory, composes `ScenarioBundleLoader`, and optionally loads a checkpoint whose dimensions must match the scenario. `NativeProjectSceneLoader` is the preferred outer boundary: it exposes the projected scene and validated runtime state together, never a partially loaded project. The runnable headless host exercises this path with `validate-project` and `summary-project`.

This flow handles only project-owned formats. It does not read `.sg`, import original content, or establish compatibility with any original-game behavior.
