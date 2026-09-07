# Native project manifest v1

`minimal.project.json` is the portable entry point for a project-owned scenario bundle. It contains references, not embedded scenario, content, or checkpoint data. The format is independent of Disciples II and does not describe `.sg` files.

## Document shape

```json
{
  "formatVersion": 1,
  "id": "synthetic-project",
  "scenario": "scenarios/minimal-scenario.json",
  "contentPackages": [
    "content/synthetic.package.json"
  ],
  "session": "sessions/minimal-session.json"
}
```

- `formatVersion` must be `1`.
- `id` is a stable project-owned identifier.
- `scenario` references one native scenario v1 document.
- `contentPackages` references one or more native content package v1 documents.
- `session` optionally references a native runtime checkpoint v1 document.

All document references are relative to the manifest directory. Absolute paths and `.` or `..` segments are invalid. Forward slashes are the canonical serialized separator, content-package references are sorted with ordinal comparison, unknown JSON properties are rejected, and serialization ends with one line feed. These rules keep tracked manifests portable and deterministic.

## Validated loading boundary

Loading succeeds only when:

1. the manifest is structurally valid;
2. every path resolves lexically below the manifest directory;
3. the scenario and all content packages load successfully;
4. package IDs and definitions form a valid typed catalog;
5. every scenario content reference resolves to the expected content kind;
6. an optional checkpoint loads successfully and has the same map dimensions as the scenario.

The loader returns stable labels such as `manifest`, `scenario`, `content[0]`, and `session` in diagnostics. It does not expose absolute local paths in its issue model or CLI reports. Referenced documents remain separate files and are never copied into the manifest.

## Safety limits and versioning

Version 1 limits the manifest to 1 MiB, 256 content-package references, 96 characters for the project ID, and 512 characters per relative path. A future incompatible schema requires a new `formatVersion`; version 1 readers reject it rather than guessing.

This is an internal project format. Original-game formats remain read-only future import inputs. `TODO-D2-RESEARCH`: no project manifest field implies compatibility with an original Disciples II project or scenario concept.
