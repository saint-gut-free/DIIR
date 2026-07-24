using System.Text.Json;
using DisciplesRemaster.OriginalGame.Research.MapExperiments;

namespace DisciplesRemaster.OriginalGame.Tests;

public sealed class MapExperimentValidationServiceTests
{
    private const string SyntheticHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly MapExperimentValidationService service = new();

    [Fact]
    public void Validate_ValidPlannedExperiment_HasNoErrors()
    {
        MapExperimentValidationResult result = service.Validate([Baseline()]);

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Validate_ValidExecutedExperiment_HasNoErrors()
    {
        MapExperimentRecord executed = Variant("MAP-001") with
        {
            Status = "executed",
            Artifacts = ArtifactsWithResult(),
        };

        Assert.False(service.Validate([Baseline(), executed]).HasErrors);
    }

    [Fact]
    public void Validate_ValidComparedExperiment_HasNoErrors()
    {
        MapExperimentRecord compared = Variant("MAP-001") with
        {
            Status = "compared",
            Artifacts = ArtifactsWithResult(),
            EvidenceReferences = [Evidence()],
        };

        Assert.False(service.Validate([Baseline(), compared]).HasErrors);
    }

    [Fact]
    public void Validate_ValidAnalyzedExperiment_HasNoErrors()
    {
        MapExperimentRecord analyzed = Variant("MAP-001") with
        {
            Status = "analyzed",
            Artifacts = ArtifactsWithResult(),
            EvidenceReferences = [Evidence()],
            Observations = [new MapExperimentObservation { Statement = "No byte differences were found." }],
        };

        Assert.False(service.Validate([Baseline(), analyzed]).HasErrors);
    }

    [Fact]
    public void Validate_MissingFormatVersion_ReportsIssue() =>
        AssertCode(Baseline() with { FormatVersion = null }, MapExperimentValidationCode.MissingFormatVersion);

    [Fact]
    public void Validate_UnsupportedVersion_ReportsIssue() =>
        AssertCode(Baseline() with { FormatVersion = 2 }, MapExperimentValidationCode.UnsupportedFormatVersion);

    [Fact]
    public void Validate_MissingExperimentId_ReportsIssue() =>
        AssertCode(Baseline() with { ExperimentId = null }, MapExperimentValidationCode.MissingExperimentId);

    [Fact]
    public void Validate_DuplicateExperimentId_ReportsIssue()
    {
        MapExperimentValidationResult result = service.Validate([Baseline(), Baseline()]);

        Assert.Contains(result.Issues, issue => issue.Code == MapExperimentValidationCode.DuplicateExperimentId);
    }

    [Fact]
    public void Validate_InvalidExperimentId_ReportsIssue() =>
        AssertCode(Baseline() with { ExperimentId = "invalid" }, MapExperimentValidationCode.InvalidExperimentId);

    [Fact]
    public void Validate_MissingTitle_ReportsIssue() =>
        AssertCode(Baseline() with { Title = " " }, MapExperimentValidationCode.MissingTitle);

    [Fact]
    public void Validate_UnknownStatus_ReportsIssue() =>
        AssertCode(Baseline() with { Status = "finished" }, MapExperimentValidationCode.UnknownStatus);

    [Fact]
    public void Validate_MissingBaseline_ReportsIssue() =>
        AssertCode(Variant("MAP-001") with { BaselineExperimentId = null }, MapExperimentValidationCode.MissingBaselineReference);

    [Fact]
    public void Validate_UnknownBaseline_ReportsIssue() =>
        AssertCode(Variant("MAP-001"), MapExperimentValidationCode.BaselineExperimentNotFound);

    [Fact]
    public void Validate_SelfBaseline_ReportsIssue() =>
        AssertCode(Variant("MAP-001") with { BaselineExperimentId = "MAP-001" }, MapExperimentValidationCode.SelfBaselineReference);

    [Fact]
    public void Validate_CircularBaselineChain_ReportsIssue()
    {
        MapExperimentRecord first = Variant("MAP-001") with { BaselineExperimentId = "MAP-002" };
        MapExperimentRecord second = Variant("MAP-002") with { BaselineExperimentId = "MAP-001" };

        MapExperimentValidationResult result = service.Validate([first, second]);

        Assert.Equal(2, result.Issues.Count(issue => issue.Code == MapExperimentValidationCode.CircularBaselineChain));
    }

    [Fact]
    public void Validate_UnknownComparison_ReportsIssue() =>
        AssertCode(Baseline() with { ComparisonExperimentIds = ["MAP-999"] }, MapExperimentValidationCode.UnknownComparisonExperiment);

    [Fact]
    public void Validate_DuplicateComparison_ReportsIssue()
    {
        MapExperimentRecord baseline = Baseline() with { ComparisonExperimentIds = ["MAP-001", "MAP-001"] };
        MapExperimentValidationResult result = service.Validate([baseline, Variant("MAP-001")]);

        Assert.Contains(result.Issues, issue => issue.Code == MapExperimentValidationCode.DuplicateComparisonReference);
    }

    [Fact]
    public void Validate_AbsoluteArtifactPath_ReportsIssue()
    {
        MapExperimentRecord record = Baseline() with
        {
            Artifacts = new MapExperimentArtifacts
            {
                Result = new MapExperimentArtifactReference { SafeName = "X:\\synthetic-only\\MAP-000A.sg" },
            },
        };

        AssertCode(record, MapExperimentValidationCode.AbsoluteArtifactPath);
    }

    [Fact]
    public void Validate_Base64LikePayload_ReportsIssue() =>
        AssertCode(Baseline() with { Notes = [new string('A', 140)] }, MapExperimentValidationCode.EmbeddedBinaryPayload);

    [Fact]
    public void Validate_MissingOperation_ReportsIssue() =>
        AssertCode(Baseline() with { Operation = null }, MapExperimentValidationCode.MissingOperation);

    [Fact]
    public void Validate_UnknownOperation_ReportsIssue() =>
        AssertCode(Baseline() with { Operation = new MapExperimentOperation { Type = "decode_map" } }, MapExperimentValidationCode.UnsupportedOperationType);

    [Fact]
    public void Validate_ExecutedWithoutHash_ReportsIssue() =>
        AssertCode(Baseline() with { Status = "executed" }, MapExperimentValidationCode.ExecutedWithoutResultHash);

    [Fact]
    public void Validate_InvalidSha256_ReportsIssue()
    {
        MapExperimentRecord record = Baseline() with
        {
            Artifacts = new MapExperimentArtifacts
            {
                Result = new MapExperimentArtifactReference { SafeName = "MAP-000A.sg", Sha256 = "invalid" },
            },
        };

        AssertCode(record, MapExperimentValidationCode.InvalidSha256);
    }

    [Fact]
    public void Validate_NegativeSize_ReportsIssue()
    {
        MapExperimentRecord record = Baseline() with
        {
            Artifacts = new MapExperimentArtifacts
            {
                Result = new MapExperimentArtifactReference { SafeName = "MAP-000A.sg", SizeBytes = -1 },
            },
        };

        AssertCode(record, MapExperimentValidationCode.NegativeFileSize);
    }

    [Fact]
    public void Validate_ComparedWithoutEvidence_ReportsIssue()
    {
        MapExperimentRecord record = Baseline() with { Status = "compared", Artifacts = ArtifactsWithResult() };

        AssertCode(record, MapExperimentValidationCode.ComparedWithoutEvidence);
    }

    [Fact]
    public void Validate_HypothesisWithoutConfidence_ReportsIssue()
    {
        MapExperimentHypothesis hypothesis = Hypothesis() with { Confidence = null };

        AssertCode(Baseline() with { Hypotheses = [hypothesis] }, MapExperimentValidationCode.HypothesisMissingConfidence);
    }

    [Fact]
    public void Validate_SupportedHypothesisWithoutEvidence_ReportsIssue()
    {
        MapExperimentHypothesis hypothesis = Hypothesis() with { Status = "supported", SupportingExperimentIds = [] };

        AssertCode(Baseline() with { Hypotheses = [hypothesis] }, MapExperimentValidationCode.SupportedHypothesisWithoutSupportingExperiment);
    }

    [Fact]
    public void Validate_ConfirmedHypothesisWithOneExperiment_ReportsIssue()
    {
        MapExperimentHypothesis hypothesis = Hypothesis() with
        {
            Status = "confirmed",
            SupportingExperimentIds = ["MAP-001"],
        };

        MapExperimentValidationResult result = service.Validate([Baseline() with { Hypotheses = [hypothesis] }, Variant("MAP-001")]);

        Assert.Contains(result.Issues, issue => issue.Code == MapExperimentValidationCode.ConfirmedHypothesisInsufficientSupport);
    }

    [Fact]
    public void Validate_ConfirmedConclusionWithoutReverseEvidence_ReportsIssue()
    {
        MapExperimentConclusion conclusion = ValidConfirmedConclusion() with { HasReverseOrAlternativeValueEvidence = false };

        AssertConclusionCode(conclusion, MapExperimentValidationCode.ConfirmedConclusionMissingReverseEvidence);
    }

    [Fact]
    public void Validate_ConfirmedConclusionWithLowConfidence_ReportsIssue()
    {
        MapExperimentConclusion conclusion = ValidConfirmedConclusion() with { Confidence = "low" };

        AssertConclusionCode(conclusion, MapExperimentValidationCode.ConfirmedConclusionLowConfidence);
    }

    [Fact]
    public void Validate_InvalidatedWithoutReason_ReportsIssue() =>
        AssertCode(Baseline() with { Status = "invalidated" }, MapExperimentValidationCode.InvalidatedWithoutReason);

    [Fact]
    public void Validate_ExcessiveByteDump_ReportsIssue()
    {
        string dump = string.Join(' ', Enumerable.Repeat("AA", 65));
        MapExperimentRecord record = Baseline() with
        {
            Observations = [new MapExperimentObservation { Statement = dump }],
        };

        AssertCode(record, MapExperimentValidationCode.ObservationByteDumpTooLarge);
    }

    [Fact]
    public void Validate_DuplicateHypothesisIds_ReportsIssue()
    {
        MapExperimentRecord record = Baseline() with { Hypotheses = [Hypothesis(), Hypothesis()] };

        AssertCode(record, MapExperimentValidationCode.DuplicateHypothesisId);
    }

    [Fact]
    public void Validate_ConclusionWithoutEvidence_ReportsIssue()
    {
        var conclusion = new MapExperimentConclusion
        {
            Statement = "Synthetic conclusion.",
            Status = "proposed",
            Confidence = "low",
        };

        AssertCode(Baseline() with { Conclusions = [conclusion] }, MapExperimentValidationCode.ConclusionWithoutEvidence);
    }

    [Fact]
    public void Validate_UnknownSupportingExperiment_ReportsIssue()
    {
        MapExperimentHypothesis hypothesis = Hypothesis() with { SupportingExperimentIds = ["MAP-999"] };

        AssertCode(Baseline() with { Hypotheses = [hypothesis] }, MapExperimentValidationCode.UnknownSupportingExperiment);
    }

    [Fact]
    public void Validate_IssuesAreDeterministicallyOrdered()
    {
        MapExperimentRecord first = Variant("MAP-002") with { Title = null, Status = "unknown" };
        MapExperimentRecord second = Variant("MAP-001") with { FormatVersion = null, Operation = null };

        MapExperimentValidationResult forward = service.Validate([first, second]);
        MapExperimentValidationResult reverse = service.Validate([second, first]);

        Assert.Equal(forward.Issues, reverse.Issues);
        Assert.Equal(["MAP-001", "MAP-002"], forward.Experiments.Select(record => record.ExperimentId));
    }

    [Fact]
    public void LoadAndValidate_SingleFile_LoadsOneExperiment()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteJson("experiment.json", Baseline());

        MapExperimentValidationResult result = service.LoadAndValidate(path);

        Assert.False(result.HasErrors);
        Assert.Single(result.Experiments);
    }

    [Fact]
    public void LoadAndValidate_Catalog_LoadsAllExperiments()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteJson("catalog.json", new MapExperimentCatalog
        {
            FormatVersion = 1,
            CatalogId = "synthetic",
            Experiments = [Baseline(), Variant("MAP-001")],
        });

        MapExperimentValidationResult result = service.LoadAndValidate(path);

        Assert.False(result.HasErrors);
        Assert.Equal(2, result.Experiments.Count);
    }

    [Fact]
    public void LoadAndValidate_Directory_SortsAndValidatesJsonFiles()
    {
        using var directory = new TemporaryDirectory();
        directory.WriteJson("z.json", Variant("MAP-002"));
        directory.WriteJson("a.json", Baseline());
        directory.WriteJson("m.json", Variant("MAP-001"));

        MapExperimentValidationResult result = service.LoadAndValidate(directory.Path);

        Assert.False(result.HasErrors);
        Assert.Equal(["MAP-000A", "MAP-001", "MAP-002"], result.Experiments.Select(record => record.ExperimentId));
    }

    [Fact]
    public void LoadAndValidate_CommittedSyntheticValidCatalog_IsValid()
    {
        string path = System.IO.Path.Combine(RepositoryRoot(), "samples", "synthetic", "map-experiments", "valid-catalog.json");

        MapExperimentValidationResult result = service.LoadAndValidate(path);

        Assert.False(result.HasErrors);
        Assert.Equal(4, result.Experiments.Count);
    }

    [Fact]
    public void LoadAndValidate_BaselineNoiseCatalogContainsMandatoryGroup()
    {
        string path = System.IO.Path.Combine(RepositoryRoot(), "samples", "synthetic", "map-experiments", "valid-catalog.json");

        MapExperimentValidationResult result = service.LoadAndValidate(path);

        Assert.Equal(
            ["MAP-000A", "MAP-000B", "MAP-000C", "MAP-000D"],
            result.Experiments.Select(record => record.ExperimentId));
    }

    [Fact]
    public void LoadAndValidate_InvalidJson_ReturnsStructuredIssue()
    {
        using var directory = new TemporaryDirectory();
        string path = directory.WriteText("invalid.json", "{ invalid");

        MapExperimentValidationResult result = service.LoadAndValidate(path);

        Assert.Contains(result.Issues, issue => issue.Code == MapExperimentValidationCode.InvalidJson);
    }

    private void AssertCode(MapExperimentRecord record, MapExperimentValidationCode code)
    {
        MapExperimentValidationResult result = service.Validate([record]);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    private void AssertConclusionCode(MapExperimentConclusion conclusion, MapExperimentValidationCode code)
    {
        MapExperimentRecord subject = Variant("MAP-003") with { Conclusions = [conclusion] };
        MapExperimentValidationResult result = service.Validate([Baseline(), Variant("MAP-001"), Variant("MAP-002"), subject]);
        Assert.Contains(result.Issues, issue => issue.Code == code);
    }

    private static MapExperimentRecord Baseline() => new()
    {
        FormatVersion = 1,
        ExperimentId = "MAP-000A",
        Title = "Synthetic baseline",
        Status = "planned",
        Operation = new MapExperimentOperation { Type = "create_baseline", Description = "Synthetic operation." },
        Artifacts = new MapExperimentArtifacts
        {
            Result = new MapExperimentArtifactReference { SafeName = "MAP-000A.sg", Role = "result" },
        },
        Notes = ["Synthetic metadata only."],
    };

    private static MapExperimentRecord Variant(string id) => new()
    {
        FormatVersion = 1,
        ExperimentId = id,
        Title = "Synthetic variant",
        Status = "planned",
        BaselineExperimentId = "MAP-000A",
        Operation = new MapExperimentOperation { Type = "change_property", Description = "Change one synthetic value." },
        Artifacts = new MapExperimentArtifacts
        {
            Baseline = new MapExperimentArtifactReference { SafeName = "MAP-000A.sg", Role = "baseline" },
            Result = new MapExperimentArtifactReference { SafeName = id + ".sg", Role = "result" },
        },
    };

    private static MapExperimentArtifacts ArtifactsWithResult() => new()
    {
        Baseline = new MapExperimentArtifactReference { SafeName = "MAP-000A.sg", Role = "baseline" },
        Result = new MapExperimentArtifactReference
        {
            SafeName = "result.sg",
            Role = "result",
            Sha256 = SyntheticHash,
            SizeBytes = 128,
        },
    };

    private static MapExperimentEvidenceReference Evidence() => new()
    {
        Kind = "binary-diff-report",
        ReportLabel = "synthetic-comparison",
        Description = "Bounded synthetic evidence.",
    };

    private static MapExperimentHypothesis Hypothesis() => new()
    {
        HypothesisId = "HYP-001",
        Statement = "Synthetic bytes may encode a synthetic value.",
        Confidence = "low",
        Status = "proposed",
    };

    private static MapExperimentConclusion ValidConfirmedConclusion() => new()
    {
        Statement = "Synthetic confirmed conclusion for validation only.",
        Status = "confirmed",
        Confidence = "high",
        SupportingExperimentIds = ["MAP-001", "MAP-002"],
        EvidenceReferences = [Evidence()],
        Limitations = ["Synthetic metadata only."],
        HasReverseOrAlternativeValueEvidence = true,
        BaselineNoiseExcluded = true,
        HasRepeatableChangedRangeOrExplicitSearch = true,
    };

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "DisciplesRemaster.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found for synthetic fixture tests.");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "disciples-map-metadata-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string WriteJson(string name, object value) => WriteText(name, JsonSerializer.Serialize(value, JsonOptions));

        public string WriteText(string name, string value)
        {
            string path = System.IO.Path.Combine(Path, name);
            File.WriteAllText(path, value);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
