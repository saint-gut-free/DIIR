# DisciplesRemaster.Editor

Headless foundation of the project-owned scenario editor. It works only with the native JSON format and does not read or convert original `.sg` files.

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

```powershell
dotnet run --project editor/DisciplesRemaster.Editor -- paint-terrain `
  artifacts/scenarios/example.json 3 2 project:forest
```

This command expresses behavior of the new project format only. It does not imply any terrain representation or coordinate convention in Disciples II. Original-game questions remain `TODO-D2-RESEARCH` items.
