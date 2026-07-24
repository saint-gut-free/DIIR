# Controlled map-editor experiment protocol

## Purpose and boundary

This protocol describes manual, reproducible experiments with files saved by a legally installed original editor. The repository does not run `ScenEdit.exe`, open or parse `.sg`, or contain original maps. The user performs editor operations manually; project tools only validate project-authored metadata and compare explicitly selected files read-only.

Research must preserve four distinct levels:

```text
Controlled operation
        ↓
Byte-level observation
        ↓
Format hypothesis
        ↓
Confirmed conclusion
```

An operation states what the user intentionally changed. An observation states only what was measured. A hypothesis proposes an explanation. A confirmed conclusion requires multiple independent controlled experiments and the safeguards defined below. Correlation is not proof of field meaning.

## Prepare local research storage

Use any directory outside both the Git repository and the original installation. The following path is illustrative and must not be hardcoded:

```text
E:\Disciples2Research\
├── map-experiments\
│   ├── MAP-000A\
│   │   ├── MAP-000A.sg
│   │   └── experiment.json
│   ├── MAP-000B\
│   └── MAP-000C\
└── reports\
```

Original `.sg` artifacts stay outside Git. BinaryDiff reports created inside the repository belong only under ignored `artifacts/`. Screenshots remain local until a separate provenance and licensing decision is made.

## Mandatory baseline-noise sequence

Do not begin with an object or coordinate experiment. First establish whether saving is deterministic and which ranges change without a user edit:

1. Prepare a separate local research directory.
2. Start the legally installed original editor manually. This project never starts it.
3. Create the smallest map the editor permits, with recorded settings.
4. Save it once as `MAP-000A.sg` — first minimal save.
5. Without changing the map, save as `MAP-000B.sg` in the same editor session.
6. Close the map, reopen it, and make no intentional change.
7. Save as `MAP-000C.sg` — reopen-and-save baseline.
8. Independently create a visually equivalent map with the same settings and save it as `MAP-000D.sg`.
9. Record size and SHA-256 immediately after each save:

   ```powershell
   Get-FileHash .\MAP-000A.sg -Algorithm SHA256
   ```

10. Compare baseline pairs, placing output outside the original installation:

    ```powershell
    dotnet run --project tools/DisciplesRemaster.BinaryDiff -- compare `
      "MAP-000A.sg" `
      "MAP-000B.sg" `
      --label-a MAP-000A `
      --label-b MAP-000B `
      --output artifacts/research/map-experiments/MAP-000A-vs-MAP-000B
    ```

11. Repeat for A/C, A/D, and useful B/C pairs. Record byte-level observations without assigning field meaning.
12. Mark ranges that vary without a controlled user change as baseline-noise candidates. Only after this analysis may object, coordinate, text, ownership, or scenario experiments begin.

`MAP-000A` through `MAP-000D` answer different questions: same-session stability, reopen stability, independent-creation determinism, generated IDs, counters, ordering, timestamps, checksums, and other service data. A range cannot be interpreted as a gameplay field until it has been separated from this noise.

## Controlled experiment rules

1. Make exactly one intentional change per experiment.
2. Never use an existing campaign map as a baseline.
3. Create baselines manually in the original editor.
4. Keep experimental `.sg` outside the Git repository.
5. Keep BinaryDiff reports in ignored `artifacts/` or another local research directory.
6. Complete save-noise experiments before interpreting property changes.
7. Use stable safe names such as `MAP-030-X12.sg`; do not include personal paths or data.
8. Record SHA-256 and size after every save.
9. After recording an artifact, never overwrite or resave it.
10. Test every hypothesis with several values.
11. When possible, change the value back or test a contrasting value.
12. If another property changed, mark the experiment contaminated or `Invalidated` and record the reason.
13. Keep editor screenshots local pending a separate decision.
14. Never commit original `.sg` files.
15. BinaryDiff reports must remain bounded and must not reproduce full source files.
16. Never confuse correlation with confirmed field meaning.

Record the editor filename, environment, locale, operation, expected difference, artifact hashes, and evidence labels. Unknown edition/version or editor behavior remains `null` or is marked `TODO-D2-RESEARCH`; do not invent it.

## From observation to conclusion

An acceptable observation is: “offsets `0x120–0x123` changed from a bounded four-byte value to another bounded four-byte value.” It is not acceptable to call those bytes a coordinate field at this stage.

A hypothesis may state that a range *may* encode the controlled value, with confidence and both supporting and contradicting experiments. Confirmation requires all of the following:

- at least two independent controlled experiments;
- several values and a reverse-direction or alternative-value change;
- relevant ranges separated from baseline noise;
- a repeatable changed range or explicit value-search match;
- no unresolved contradiction;
- `High` or `ConfirmedByMultipleIndependentExperiments` confidence;
- evidence references and stated limitations.

The validator verifies that these formal prerequisites are recorded. It does not prove the technical truth of a conclusion.

## Validate metadata

Validate one experiment JSON, a catalog JSON, or a directory containing experiment JSON:

```powershell
dotnet run --project tools/DisciplesRemaster.MapInspector -- validate-experiments .\experiment.json
```

Validation reads JSON metadata only. It deliberately does not locate or open artifact names referenced by the metadata.
