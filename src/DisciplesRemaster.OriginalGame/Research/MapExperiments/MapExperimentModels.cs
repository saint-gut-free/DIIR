using System.Text.Json;

namespace DisciplesRemaster.OriginalGame.Research.MapExperiments;

public enum MapExperimentStatus
{
    Planned,
    Prepared,
    Executed,
    Compared,
    Analyzed,
    Invalidated,
    Superseded,
}

public enum MapExperimentOperationType
{
    CreateBaseline,
    SaveWithoutChanges,
    ReopenAndSaveWithoutChanges,
    CreateEquivalentMap,
    ChangeMapDimension,
    PaintTerrain,
    AddObject,
    RemoveObject,
    MoveObject,
    ChangeProperty,
    ChangeText,
    ChangeOwner,
    AddEvent,
    ChangeVictoryCondition,
    Other,
}

public enum MapExperimentHypothesisStatus
{
    Proposed,
    Supported,
    Contradicted,
    Confirmed,
    Rejected,
}

public enum MapExperimentConclusionStatus
{
    Proposed,
    Confirmed,
    Rejected,
}

public enum ResearchConfidenceLevel
{
    Unknown,
    Low,
    Medium,
    High,
    ConfirmedByMultipleIndependentExperiments,
}

public enum MapExperimentValidationSeverity
{
    Info,
    Warning,
    Error,
}

public enum MapExperimentValidationCode
{
    InvalidJson,
    UnsupportedDocumentType,
    MissingFormatVersion,
    UnsupportedFormatVersion,
    MissingExperimentId,
    DuplicateExperimentId,
    InvalidExperimentId,
    MissingTitle,
    UnknownStatus,
    MissingBaselineReference,
    BaselineExperimentNotFound,
    SelfBaselineReference,
    CircularBaselineChain,
    UnknownComparisonExperiment,
    DuplicateComparisonReference,
    AbsoluteArtifactPath,
    EmbeddedBinaryPayload,
    MissingOperation,
    UnsupportedOperationType,
    ExecutedWithoutResultHash,
    InvalidSha256,
    NegativeFileSize,
    ComparedWithoutEvidence,
    HypothesisMissingConfidence,
    SupportedHypothesisWithoutSupportingExperiment,
    ConfirmedHypothesisInsufficientSupport,
    ConfirmedHypothesisHasUnresolvedContradictions,
    RejectedHypothesisMissingReason,
    ConfirmedConclusionMissingIndependentSupport,
    ConfirmedConclusionMissingReverseEvidence,
    ConfirmedConclusionBaselineNoiseNotExcluded,
    ConfirmedConclusionMissingRepeatableEvidence,
    ConfirmedConclusionLowConfidence,
    UnknownSupportingExperiment,
    InvalidatedWithoutReason,
    ObservationByteDumpTooLarge,
    DuplicateHypothesisId,
    ConclusionWithoutEvidence,
    AbsoluteLocalPathDetected,
    AnalyzedWithoutObservation,
}

public sealed record MapExperimentCatalog
{
    public int? FormatVersion { get; init; }

    public string? CatalogId { get; init; }

    public IReadOnlyList<MapExperimentRecord> Experiments { get; init; } = [];
}

public sealed record MapExperimentRecord
{
    public int? FormatVersion { get; init; }

    public string? ExperimentId { get; init; }

    public string? Title { get; init; }

    public string? Status { get; init; }

    public string? Applicability { get; init; }

    public string? BaselineExperimentId { get; init; }

    public IReadOnlyList<string> ComparisonExperimentIds { get; init; } = [];

    public MapExperimentEnvironment? Environment { get; init; }

    public MapExperimentOperation? Operation { get; init; }

    public MapExperimentArtifacts Artifacts { get; init; } = new();

    public string? ExpectedDifference { get; init; }

    public IReadOnlyList<MapExperimentObservation> Observations { get; init; } = [];

    public IReadOnlyList<MapExperimentHypothesis> Hypotheses { get; init; } = [];

    public IReadOnlyList<MapExperimentConclusion> Conclusions { get; init; } = [];

    public IReadOnlyList<MapExperimentEvidenceReference> EvidenceReferences { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? InvalidatedReason { get; init; }
}

public sealed record MapExperimentEnvironment
{
    public string? GameEdition { get; init; }

    public string? GameVersion { get; init; }

    public string? EditorFilename { get; init; }

    public string? EditorVersion { get; init; }

    public string? OperatingSystem { get; init; }

    public string? Locale { get; init; }
}

public sealed record MapExperimentOperation
{
    public string? Type { get; init; }

    public string? EntityType { get; init; }

    public string? EntitySafeId { get; init; }

    public string? Property { get; init; }

    public JsonElement? Before { get; init; }

    public JsonElement? After { get; init; }

    public MapExperimentCoordinates? Coordinates { get; init; }

    public string? Description { get; init; }
}

public sealed record MapExperimentCoordinates
{
    public int? X { get; init; }

    public int? Y { get; init; }
}

public sealed record MapExperimentArtifacts
{
    public MapExperimentArtifactReference? Baseline { get; init; }

    public MapExperimentArtifactReference? Result { get; init; }
}

public sealed record MapExperimentArtifactReference
{
    public string? SafeName { get; init; }

    public string? Sha256 { get; init; }

    public long? SizeBytes { get; init; }

    public string? Role { get; init; }

    public string? RelativeResearchLabel { get; init; }
}

public sealed record MapExperimentObservation
{
    public string? Statement { get; init; }

    public string? Kind { get; init; }

    public IReadOnlyList<MapExperimentEvidenceReference> EvidenceReferences { get; init; } = [];
}

public sealed record MapExperimentHypothesis
{
    public string? HypothesisId { get; init; }

    public string? Statement { get; init; }

    public string? Confidence { get; init; }

    public string? Status { get; init; }

    public IReadOnlyList<string> SupportingExperimentIds { get; init; } = [];

    public IReadOnlyList<string> ContradictingExperimentIds { get; init; } = [];

    public IReadOnlyList<MapExperimentEvidenceReference> EvidenceReferences { get; init; } = [];

    public IReadOnlyList<string> UnresolvedQuestions { get; init; } = [];

    public string? RejectionReason { get; init; }
}

public sealed record MapExperimentConclusion
{
    public string? Statement { get; init; }

    public string? Status { get; init; }

    public string? Confidence { get; init; }

    public IReadOnlyList<string> SupportingExperimentIds { get; init; } = [];

    public IReadOnlyList<MapExperimentEvidenceReference> EvidenceReferences { get; init; } = [];

    public IReadOnlyList<string> Limitations { get; init; } = [];

    public bool HasReverseOrAlternativeValueEvidence { get; init; }

    public bool BaselineNoiseExcluded { get; init; }

    public bool HasRepeatableChangedRangeOrExplicitSearch { get; init; }
}

public sealed record MapExperimentEvidenceReference
{
    public string? Kind { get; init; }

    public string? Label { get; init; }

    public string? ReportLabel { get; init; }

    public string? ExperimentId { get; init; }

    public string? StartOffsetHex { get; init; }

    public string? EndOffsetHex { get; init; }

    public string? Description { get; init; }
}

public sealed record MapExperimentValidationIssue(
    MapExperimentValidationCode Code,
    MapExperimentValidationSeverity Severity,
    string ExperimentId,
    string PropertyPath,
    string Message);

public sealed record MapExperimentValidationResult(
    IReadOnlyList<MapExperimentRecord> Experiments,
    IReadOnlyList<MapExperimentValidationIssue> Issues)
{
    public bool HasErrors => Issues.Any(issue => issue.Severity == MapExperimentValidationSeverity.Error);
}
