# Native project diagnostic viewport v1

This project-owned text view is a bounded inspection surface for a validated native project. It exists so the complete loading and scene-projection boundary can be exercised before a compatible Godot .NET SDK is installed. It is not a replacement for the graphical host and does not reconstruct original-game visuals or mechanics.

## Input boundary

The renderer accepts only `NativeProjectSceneData` produced after the portable manifest, scenario, content packages, cross-content references, and optional runtime checkpoint have passed validation. It never reads project files directly and never invents missing content.

## Viewport

- origin defaults to `(0, 0)` and must be inside the map;
- width defaults to `40` and is bounded to `1..120`;
- height defaults to `20` and is bounded to `1..60`;
- a viewport extending beyond the map is clamped to remaining cells;
- visible detail rows have a shared hard limit of 200.

The command is:

```powershell
dotnet run --project game/DisciplesRemaster.Godot -- render-project `
  samples/synthetic/minimal.project.json `
  --origin-x 0 --origin-y 0 --width 20 --height 12
```

## Symbols

- `.` — the scenario default terrain has no additional visible layer;
- `T` — one terrain override;
- `O` — one inert scenario object;
- `A` — one runtime actor;
- `*` — more than one visible layer or item occupies the cell.

These symbols identify adapter data layers only. They do not specify terrain appearance, passability, object behavior, actor interactions, or any Disciples II rule.

## Determinism and safety

Rows and columns follow increasing zero-based project coordinates. Detail rows are ordered by position or stable ID as appropriate. Numeric formatting is invariant and line endings are LF, so identical input and options produce identical text across supported platforms. Output is bounded, contains no absolute paths, performs no network or original-file operations, and never serializes binary content.
