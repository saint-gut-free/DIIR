# DisciplesRemaster.Editor

Headless foundation of the project-owned scenario editor. It works only with the native JSON format and does not read or convert original `.sg` files.

## Validate a complete native project

A portable project manifest links one scenario, one or more typed content packages, and an optional runtime checkpoint using paths relative to the manifest directory:

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- validate-project samples/synthetic/minimal.project.json
dotnet run --project editor/DisciplesRemaster.Editor -- summary-project samples/synthetic/minimal.project.json
```

Validation resolves all content references and, when a checkpoint is present, checks that its map dimensions match the scenario. Absolute paths and `.` or `..` path segments are rejected. See [native project manifest v1](../../docs/specifications/native-project-manifest-v1.md).

## Create a scenario

The output directory must already exist. Existing files are protected unless `--force` is explicitly supplied.

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- create `
  artifacts/scenarios/example.json `
  --id example `
  --title "Example scenario" `
  --width 16 `
  --height 12 `
  --default-terrain project:plain
```

## Validate and inspect

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- validate artifacts/scenarios/example.json
dotnet run --project editor/DisciplesRemaster.Editor -- summary artifacts/scenarios/example.json
```

## Paint one terrain override

Coordinates are zero-based. Omitting `--output` updates the native JSON document atomically. Painting the default terrain removes an existing override and keeps the representation canonical.

## Update scenario metadata and size

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- set-title artifacts/scenarios/example.json "Updated title"
dotnet run --project editor/DisciplesRemaster.Editor -- set-default-terrain artifacts/scenarios/example.json synthetic:water
dotnet run --project editor/DisciplesRemaster.Editor -- resize-map artifacts/scenarios/example.json 48 32
```

Changing the default removes overrides that would become redundant. Resizing is rejected if any existing terrain override or object would fall outside the new bounds. Each command supports `--output <file>` to preserve its input.

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- paint-terrain `
  artifacts/scenarios/example.json 3 2 project:forest
```

This command expresses behavior of the new project format only. It does not imply any terrain representation or coordinate convention in Disciples II. Original-game questions remain `TODO-D2-RESEARCH` items.

## Manage inert object placements

Object placements contain only a stable ID, a project content reference, and a zero-based position. They have no implied gameplay behavior.

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- place-object `
  artifacts/scenarios/example.json landmark-1 project:landmark 5 4

dotnet run --project editor/DisciplesRemaster.Editor -- move-object `
  artifacts/scenarios/example.json landmark-1 6 4

dotnet run --project editor/DisciplesRemaster.Editor -- remove-object `
  artifacts/scenarios/example.json landmark-1
```

## Validate content packages and references

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- `
  validate-content samples/synthetic/content/synthetic.package.json

dotnet run --project editor/DisciplesRemaster.Editor -- `
  validate-scenario-content `
  samples/synthetic/scenarios/minimal-scenario.json `
  samples/synthetic/content/synthetic.package.json
```

Validation is typed: a terrain reference cannot resolve to an object archetype with the same local ID.
