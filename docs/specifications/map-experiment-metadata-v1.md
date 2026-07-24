# Map experiment metadata v1

## Scope and versioning

Version 1 records project-authored metadata for controlled editor experiments. It references artifacts by safe name, hash, and size only. It does not contain or validate `.sg` content. Breaking structural or semantic changes require a new `formatVersion`; readers reject unsupported versions.

A single experiment is one JSON object. A catalog has `formatVersion`, optional `catalogId`, and an `experiments` array. A directory input consists of top-level `*.json` files sorted by ordinal filename. Experiments and issues are sorted deterministically; validation adds no timestamp, random ID, or absolute path.

## Experiment structure

Core properties are:

- `formatVersion`: integer `1`;
- `experimentId`: stable `MAP-NNN...` identifier;
- `title`, `status`, and optional applicability;
- `baselineExperimentId` and `comparisonExperimentIds`;
- nullable environment facts, including edition, version, editor filename, OS, and locale;
- one controlled `operation`;
- `artifacts.baseline` and `artifacts.result` safe references;
- `expectedDifference`;
- separate `observations`, `hypotheses`, `conclusions`, and `evidenceReferences`;
- notes, tags, and an invalidation reason.

Artifact references allow only `safeName`, SHA-256, byte size, role, and an optional relative research label. They never contain an absolute path, file content, base64, or a large byte dump. The validator does not test whether the artifact exists.

## Statuses

- `Planned`: defined but not yet prepared; hashes are optional.
- `Prepared`: controlled setup and names are ready.
- `Executed`: manually saved result exists; result SHA-256 and size are required.
- `Compared`: BinaryDiff or equivalent bounded evidence is referenced.
- `Analyzed`: at least one direct observation, including explicit “no differences,” is recorded.
- `Invalidated`: contaminated or unusable; a reason is required.
- `Superseded`: retained for traceability after a replacement experiment.

The validator checks obvious contradictions, not a complete workflow state machine.

## Operation types

Supported values are `CreateBaseline`, `SaveWithoutChanges`, `ReopenAndSaveWithoutChanges`, `CreateEquivalentMap`, `ChangeMapDimension`, `PaintTerrain`, `AddObject`, `RemoveObject`, `MoveObject`, `ChangeProperty`, `ChangeText`, `ChangeOwner`, `AddEvent`, `ChangeVictoryCondition`, and `Other`. JSON may use snake case, for example `change_property`.

An operation may record entity type, safe entity ID, property, JSON `before` and `after` values, coordinates, and a short description. These fields describe user actions and do not claim corresponding `.sg` fields exist.

## Evidence layers

An observation is a directly measured bounded fact: file size, hash, changed-range count, a short range, explicit search match, or no differences. A hypothesis has a stable ID, statement, confidence, status, supporting/contradicting experiments, evidence, unresolved questions, and an optional rejection reason. A conclusion contains a stricter statement, confidence, supporting experiments, evidence, limitations, and explicit prerequisite flags.

Hypothesis statuses are `Proposed`, `Supported`, `Contradicted`, `Confirmed`, and `Rejected`. A supported hypothesis requires support; a confirmed hypothesis requires at least two independent supporting experiments and no unresolved contradictions; a rejected hypothesis requires a reason.

A confirmed conclusion requires at least two independent experiments, reverse-direction or alternative-value evidence, exclusion of baseline noise, a repeatable changed range or explicit search match, evidence references, and `High` or `ConfirmedByMultipleIndependentExperiments` confidence. Validation checks that these claims are recorded; it does not establish their truth.

## Confidence

Supported confidence values are `Unknown`, `Low`, `Medium`, `High`, and `ConfirmedByMultipleIndependentExperiments`. Their evidence meanings are defined in `docs/research/research-confidence-levels.md`.

## Validation issue structure

Every issue contains stable `code`, `severity` (`Info`, `Warning`, or `Error`), experiment ID, JSON-style property path, and a safe human-readable message. Implemented error codes are:

```text
InvalidJson
UnsupportedDocumentType
MissingFormatVersion
UnsupportedFormatVersion
MissingExperimentId
DuplicateExperimentId
InvalidExperimentId
MissingTitle
UnknownStatus
MissingBaselineReference
BaselineExperimentNotFound
SelfBaselineReference
CircularBaselineChain
UnknownComparisonExperiment
DuplicateComparisonReference
AbsoluteArtifactPath
EmbeddedBinaryPayload
MissingOperation
UnsupportedOperationType
ExecutedWithoutResultHash
InvalidSha256
NegativeFileSize
ComparedWithoutEvidence
HypothesisMissingConfidence
SupportedHypothesisWithoutSupportingExperiment
ConfirmedHypothesisInsufficientSupport
ConfirmedHypothesisHasUnresolvedContradictions
RejectedHypothesisMissingReason
ConfirmedConclusionMissingIndependentSupport
ConfirmedConclusionMissingReverseEvidence
ConfirmedConclusionBaselineNoiseNotExcluded
ConfirmedConclusionMissingRepeatableEvidence
ConfirmedConclusionLowConfidence
UnknownSupportingExperiment
InvalidatedWithoutReason
ObservationByteDumpTooLarge
DuplicateHypothesisId
ConclusionWithoutEvidence
AbsoluteLocalPathDetected
AnalyzedWithoutObservation
```

Issues sort by experiment ID, property path, code, and message using ordinal comparison.

## Privacy and bounded content

Metadata must not contain absolute local paths, personal data, embedded binary payloads, full source files, or byte excerpts longer than 64 hexadecimal bytes per observation. Safe labels and relative evidence identifiers distinguish artifacts. CLI output prints only the final input file or directory name and never serializes the input path.
