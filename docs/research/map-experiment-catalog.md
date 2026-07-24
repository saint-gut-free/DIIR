# Planned controlled map-experiment catalog

Every entry starts as `Planned`. Applicability is `requires-manual-editor-check` until the original editor is manually shown to support the operation. The catalog states research questions, not facts about `.sg` structure. Unsupported operations are recorded as not applicable; they are not simulated or inferred.

## Group A — save noise and determinism (mandatory first)

| ID | Controlled operation | Purpose |
|---|---|---|
| MAP-000A | First minimal save | Establish the initial baseline. |
| MAP-000B | Save without changes in the same session | Measure same-session save noise. |
| MAP-000C | Reopen and save without changes | Measure reopen/session noise. |
| MAP-000D | Independently create an equivalent map | Test deterministic output, generated IDs, counters, and creation-order effects. |

No later group may be interpreted until Group A is compared and its unexplained changing ranges are recorded.

## Group B — map dimensions

| ID | Controlled operation | Applicability |
|---|---|---|
| MAP-001 | Width variant A | Only if width is controllable in the editor. |
| MAP-002 | Width variant B | Only if width is controllable; compare several values. |
| MAP-003 | Height variant A | Only if height is controllable in the editor. |
| MAP-004 | Height variant B | Only if height is controllable; include a reverse test. |

## Group C — terrain

| ID | Controlled operation | Applicability |
|---|---|---|
| MAP-010 | Change one terrain cell | Requires a reproducibly selectable cell and terrain value. |
| MAP-011 | Same cell, another terrain value | Requires MAP-010 to be uncontaminated. |
| MAP-012 | Same terrain at another coordinate | Tests value versus location effects. |

## Group D — object existence

| ID | Controlled operation | Applicability |
|---|---|---|
| MAP-020 | Add one decoration | Only if supported by the editor. |
| MAP-021 | Add one leader | Only if supported by the editor. |
| MAP-022 | Add one city | Only if supported by the editor. |
| MAP-023 | Add one neutral party | Only if supported by the editor. |
| MAP-024 | Add one item | Only if supported by the editor. |

## Group E — coordinates

| ID | Controlled operation | Purpose |
|---|---|---|
| MAP-030 | Leader X variants | Test several X values with Y and all other properties fixed. |
| MAP-031 | Leader Y variants | Test several Y values with X fixed. |
| MAP-032 | Same object at several coordinates | Separate object identity from position candidates. |
| MAP-033 | Move object back to original coordinate | Required reverse-direction evidence candidate. |

## Group F — strings

| ID | Controlled operation | Purpose |
|---|---|---|
| MAP-040 | Short ASCII-compatible map name | Establish a bounded text observation. |
| MAP-041 | Longer map name | Test length effects without inferring storage. |
| MAP-042 | Cyrillic map name | Test encoding behavior; locale must be recorded. |
| MAP-043 | Object or scenario description | Only if the editor exposes a controlled description field. |

## Group G — ownership and composition

| ID | Controlled operation | Applicability |
|---|---|---|
| MAP-050 | Change object owner | Requires an editor-supported owner control. |
| MAP-051 | Change party composition | One slot/change only. |
| MAP-052 | Change unit count or slot | Use multiple and reverse values if supported. |

## Group H — scenario logic

| ID | Controlled operation | Applicability |
|---|---|---|
| MAP-060 | Add one event | Only if supported; event internals remain `TODO-D2-RESEARCH`. |
| MAP-061 | Add one dialogue | Only if supported; one controlled text change. |
| MAP-062 | Add one region | Only if a minimal region can be controlled. |
| MAP-063 | Add one victory condition | Only if supported; do not infer runtime semantics. |

Each concrete variant should receive a stable suffix when needed, for example `MAP-030-X12`, `MAP-030-X13`, and `MAP-030-X20`. Metadata must link baselines, comparisons, evidence, contamination, and applicability explicitly.
