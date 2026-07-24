# MapInspector metadata validation

Task 005 uses this tool only to validate controlled map-experiment metadata. It does not open, locate, parse, or compare `.sg` files and does not run the original editor.

Validate one experiment, a catalog, or a directory containing experiment JSON files:

```powershell
dotnet run --project tools/DisciplesRemaster.MapInspector -- validate-experiments samples/synthetic/map-experiments/valid-catalog.json
```

Exit codes are `0` for valid metadata, `2` for an unavailable input, `3` for validation errors, `64` for invalid arguments, and `70` for an unexpected failure. Only the final file or directory name is shown; absolute paths are not printed.
