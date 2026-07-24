using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DisciplesRemaster.OriginalGame.Research.MapExperiments;

public sealed partial class MapExperimentValidationService : IMapExperimentValidationService
{
    public const int SupportedFormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public MapExperimentValidationResult LoadAndValidate(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        try
        {
            string fullPath = Path.GetFullPath(inputPath);
            string[] files;
            if (File.Exists(fullPath))
            {
                files = [fullPath];
            }
            else if (Directory.Exists(fullPath))
            {
                files = Directory.GetFiles(fullPath, "*.json", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.Ordinal);
            }
            else
            {
                throw new MapExperimentInputException("The metadata input does not exist or is not accessible.");
            }

            var records = new List<MapExperimentRecord>();
            var issues = new List<MapExperimentValidationIssue>();
            if (files.Length == 0)
            {
                issues.Add(Issue(
                    MapExperimentValidationCode.UnsupportedDocumentType,
                    "<input>",
                    "$",
                    "The input directory contains no JSON metadata files."));
            }

            foreach (string file in files)
            {
                LoadDocument(file, records, issues);
            }

            return MergeAndSort(Validate(records), issues);
        }
        catch (MapExperimentInputException)
        {
            throw;
        }
        catch (Exception exception) when (IsExpectedInputException(exception))
        {
            throw new MapExperimentInputException("The metadata input could not be read safely.", exception);
        }
    }

    public MapExperimentValidationResult Validate(IEnumerable<MapExperimentRecord> experiments)
    {
        ArgumentNullException.ThrowIfNull(experiments);
        MapExperimentRecord[] records = experiments
            .OrderBy(record => record.ExperimentId ?? string.Empty, StringComparer.Ordinal)
            .ToArray();
        var issues = new List<MapExperimentValidationIssue>();

        ValidateDuplicateIds(records, issues);
        var knownIds = records
            .Where(record => !string.IsNullOrWhiteSpace(record.ExperimentId))
            .GroupBy(record => record.ExperimentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (MapExperimentRecord record in records)
        {
            ValidateRecord(record, knownIds, issues);
        }

        ValidateBaselineCycles(knownIds, issues);
        return new MapExperimentValidationResult(records, SortIssues(issues));
    }

    private static void LoadDocument(
        string path,
        List<MapExperimentRecord> records,
        List<MapExperimentValidationIssue> issues)
    {
        string safeSource = Path.GetFileName(path);
        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                16 * 1024,
                FileOptions.SequentialScan);
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    MapExperimentValidationCode.UnsupportedDocumentType,
                    "<document>",
                    "$",
                    $"Metadata document '{safeSource}' must contain a JSON object."));
                return;
            }

            if (document.RootElement.TryGetProperty("experiments", out JsonElement experimentElements))
            {
                ValidateDocumentVersion(document.RootElement, "<catalog>", issues);
                if (experimentElements.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(Issue(
                        MapExperimentValidationCode.UnsupportedDocumentType,
                        "<catalog>",
                        "$.experiments",
                        "Catalog experiments must be an array."));
                    return;
                }

                foreach (JsonElement element in experimentElements.EnumerateArray())
                {
                    DeserializeRecord(element, safeSource, records, issues);
                }
            }
            else
            {
                DeserializeRecord(document.RootElement, safeSource, records, issues);
            }
        }
        catch (JsonException)
        {
            issues.Add(Issue(
                MapExperimentValidationCode.InvalidJson,
                "<document>",
                "$",
                $"Metadata document '{safeSource}' is not valid JSON."));
        }
    }

    private static void DeserializeRecord(
        JsonElement element,
        string safeSource,
        List<MapExperimentRecord> records,
        List<MapExperimentValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue(
                MapExperimentValidationCode.UnsupportedDocumentType,
                "<document>",
                "$",
                $"Metadata document '{safeSource}' contains a non-object experiment."));
            return;
        }

        try
        {
            MapExperimentRecord? record = element.Deserialize<MapExperimentRecord>(JsonOptions);
            if (record is null)
            {
                throw new JsonException();
            }

            records.Add(record);
        }
        catch (JsonException)
        {
            issues.Add(Issue(
                MapExperimentValidationCode.InvalidJson,
                "<document>",
                "$",
                $"Metadata document '{safeSource}' does not match map experiment metadata v1."));
        }
    }

    private static void ValidateDocumentVersion(
        JsonElement element,
        string experimentId,
        List<MapExperimentValidationIssue> issues)
    {
        if (!element.TryGetProperty("formatVersion", out JsonElement version) || version.ValueKind == JsonValueKind.Null)
        {
            issues.Add(Issue(
                MapExperimentValidationCode.MissingFormatVersion,
                experimentId,
                "$.formatVersion",
                "Format version is required."));
        }
        else if (!version.TryGetInt32(out int value) || value != SupportedFormatVersion)
        {
            issues.Add(Issue(
                MapExperimentValidationCode.UnsupportedFormatVersion,
                experimentId,
                "$.formatVersion",
                $"Only format version {SupportedFormatVersion} is supported."));
        }
    }

    private static void ValidateDuplicateIds(
        IReadOnlyList<MapExperimentRecord> records,
        List<MapExperimentValidationIssue> issues)
    {
        foreach (IGrouping<string, MapExperimentRecord> duplicate in records
                     .Where(record => !string.IsNullOrWhiteSpace(record.ExperimentId))
                     .GroupBy(record => record.ExperimentId!, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Issue(
                MapExperimentValidationCode.DuplicateExperimentId,
                duplicate.Key,
                "$.experimentId",
                "Experiment IDs must be unique within the validated set."));
        }
    }

    private static void ValidateRecord(
        MapExperimentRecord record,
        IReadOnlyDictionary<string, MapExperimentRecord> knownIds,
        List<MapExperimentValidationIssue> issues)
    {
        string id = string.IsNullOrWhiteSpace(record.ExperimentId) ? "<unknown>" : record.ExperimentId;
        if (record.FormatVersion is null)
        {
            issues.Add(Issue(MapExperimentValidationCode.MissingFormatVersion, id, "$.formatVersion", "Format version is required."));
        }
        else if (record.FormatVersion != SupportedFormatVersion)
        {
            issues.Add(Issue(MapExperimentValidationCode.UnsupportedFormatVersion, id, "$.formatVersion", "Only format version 1 is supported."));
        }

        if (string.IsNullOrWhiteSpace(record.ExperimentId))
        {
            issues.Add(Issue(MapExperimentValidationCode.MissingExperimentId, id, "$.experimentId", "Experiment ID is required."));
        }
        else if (!ExperimentIdRegex().IsMatch(record.ExperimentId))
        {
            issues.Add(Issue(MapExperimentValidationCode.InvalidExperimentId, id, "$.experimentId", "Experiment ID must use the stable MAP-NNN... form."));
        }

        if (string.IsNullOrWhiteSpace(record.Title))
        {
            issues.Add(Issue(MapExperimentValidationCode.MissingTitle, id, "$.title", "Experiment title is required."));
        }

        bool validStatus = TryParseName<MapExperimentStatus>(record.Status, out MapExperimentStatus status);
        if (!validStatus)
        {
            issues.Add(Issue(MapExperimentValidationCode.UnknownStatus, id, "$.status", "Experiment status is missing or unsupported."));
        }

        MapExperimentOperationType operationType = default;
        bool validOperation = record.Operation is not null &&
            TryParseName(record.Operation.Type, out operationType);
        if (record.Operation is null)
        {
            issues.Add(Issue(MapExperimentValidationCode.MissingOperation, id, "$.operation", "A controlled operation is required."));
        }
        else if (!validOperation)
        {
            issues.Add(Issue(MapExperimentValidationCode.UnsupportedOperationType, id, "$.operation.type", "Operation type is missing or unsupported."));
        }

        ValidateReferences(record, id, knownIds, validOperation, operationType, issues);
        ValidateArtifacts(record, id, issues);
        ValidateStatusRequirements(record, id, validStatus, status, issues);
        ValidateHypotheses(record, id, knownIds, issues);
        ValidateConclusions(record, id, knownIds, issues);
        ValidateObservations(record, id, issues);
        ValidateSensitiveStrings(record, id, issues);
    }

    private static void ValidateReferences(
        MapExperimentRecord record,
        string id,
        IReadOnlyDictionary<string, MapExperimentRecord> knownIds,
        bool validOperation,
        MapExperimentOperationType operationType,
        List<MapExperimentValidationIssue> issues)
    {
        bool baselineMayBeAbsent = validOperation && operationType == MapExperimentOperationType.CreateBaseline;
        if (string.IsNullOrWhiteSpace(record.BaselineExperimentId))
        {
            if (!baselineMayBeAbsent)
            {
                issues.Add(Issue(MapExperimentValidationCode.MissingBaselineReference, id, "$.baselineExperimentId", "Non-baseline experiments require a baseline reference."));
            }
        }
        else if (string.Equals(record.BaselineExperimentId, record.ExperimentId, StringComparison.Ordinal))
        {
            issues.Add(Issue(MapExperimentValidationCode.SelfBaselineReference, id, "$.baselineExperimentId", "An experiment cannot be its own baseline."));
        }
        else if (!knownIds.ContainsKey(record.BaselineExperimentId))
        {
            issues.Add(Issue(MapExperimentValidationCode.BaselineExperimentNotFound, id, "$.baselineExperimentId", "The baseline experiment is not present in the validated set."));
        }

        string[] comparisons = record.ComparisonExperimentIds?.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray() ?? [];
        foreach (string duplicate in comparisons.GroupBy(value => value, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            issues.Add(Issue(MapExperimentValidationCode.DuplicateComparisonReference, id, "$.comparisonExperimentIds", $"Comparison reference '{duplicate}' is duplicated."));
        }

        foreach (string comparison in comparisons.Distinct(StringComparer.Ordinal))
        {
            if (!knownIds.ContainsKey(comparison))
            {
                issues.Add(Issue(MapExperimentValidationCode.UnknownComparisonExperiment, id, "$.comparisonExperimentIds", $"Comparison experiment '{comparison}' is unknown."));
            }
        }
    }

    private static void ValidateArtifacts(
        MapExperimentRecord record,
        string id,
        List<MapExperimentValidationIssue> issues)
    {
        MapExperimentArtifacts artifacts = record.Artifacts ?? new MapExperimentArtifacts();
        ValidateArtifact(artifacts.Baseline, "$.artifacts.baseline", id, issues);
        ValidateArtifact(artifacts.Result, "$.artifacts.result", id, issues);
    }

    private static void ValidateArtifact(
        MapExperimentArtifactReference? artifact,
        string path,
        string id,
        List<MapExperimentValidationIssue> issues)
    {
        if (artifact is null)
        {
            return;
        }

        foreach ((string? value, string property) in new[]
                 {
                     (artifact.SafeName, path + ".safeName"),
                     (artifact.RelativeResearchLabel, path + ".relativeResearchLabel"),
                 })
        {
            if (!string.IsNullOrWhiteSpace(value) && LooksLikeAbsolutePath(value))
            {
                issues.Add(Issue(MapExperimentValidationCode.AbsoluteArtifactPath, id, property, "Artifact references may not contain absolute paths."));
            }
        }

        if (!string.IsNullOrWhiteSpace(artifact.Sha256) && !Sha256Regex().IsMatch(artifact.Sha256))
        {
            issues.Add(Issue(MapExperimentValidationCode.InvalidSha256, id, path + ".sha256", "SHA-256 must contain exactly 64 hexadecimal characters."));
        }

        if (artifact.SizeBytes < 0)
        {
            issues.Add(Issue(MapExperimentValidationCode.NegativeFileSize, id, path + ".sizeBytes", "Artifact size cannot be negative."));
        }
    }

    private static void ValidateStatusRequirements(
        MapExperimentRecord record,
        string id,
        bool validStatus,
        MapExperimentStatus status,
        List<MapExperimentValidationIssue> issues)
    {
        if (!validStatus)
        {
            return;
        }

        if (status is MapExperimentStatus.Executed or MapExperimentStatus.Compared or MapExperimentStatus.Analyzed)
        {
            MapExperimentArtifactReference? result = record.Artifacts?.Result;
            if (result is null || string.IsNullOrWhiteSpace(result.Sha256) || result.SizeBytes is null)
            {
                issues.Add(Issue(MapExperimentValidationCode.ExecutedWithoutResultHash, id, "$.artifacts.result", "Executed or later experiments require result SHA-256 and size."));
            }
        }

        if (status is MapExperimentStatus.Compared or MapExperimentStatus.Analyzed && (record.EvidenceReferences?.Count ?? 0) == 0)
        {
            issues.Add(Issue(MapExperimentValidationCode.ComparedWithoutEvidence, id, "$.evidenceReferences", "Compared or analyzed experiments require a comparison evidence reference."));
        }

        if (status == MapExperimentStatus.Analyzed && (record.Observations?.Count ?? 0) == 0)
        {
            issues.Add(Issue(MapExperimentValidationCode.AnalyzedWithoutObservation, id, "$.observations", "Analyzed experiments require an observation, including an explicit no-difference observation when applicable."));
        }

        if (status == MapExperimentStatus.Invalidated && string.IsNullOrWhiteSpace(record.InvalidatedReason))
        {
            issues.Add(Issue(MapExperimentValidationCode.InvalidatedWithoutReason, id, "$.invalidatedReason", "Invalidated experiments require a reason."));
        }
    }

    private static void ValidateHypotheses(
        MapExperimentRecord record,
        string id,
        IReadOnlyDictionary<string, MapExperimentRecord> knownIds,
        List<MapExperimentValidationIssue> issues)
    {
        MapExperimentHypothesis[] hypotheses = record.Hypotheses?.ToArray() ?? [];
        foreach (string duplicate in hypotheses
                     .Where(value => !string.IsNullOrWhiteSpace(value.HypothesisId))
                     .GroupBy(value => value.HypothesisId!, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(Issue(MapExperimentValidationCode.DuplicateHypothesisId, id, "$.hypotheses", $"Hypothesis ID '{duplicate}' is duplicated."));
        }

        for (int index = 0; index < hypotheses.Length; index++)
        {
            MapExperimentHypothesis hypothesis = hypotheses[index];
            string path = $"$.hypotheses[{index}]";
            bool validConfidence = TryParseName<ResearchConfidenceLevel>(hypothesis.Confidence, out _);
            if (!validConfidence)
            {
                issues.Add(Issue(MapExperimentValidationCode.HypothesisMissingConfidence, id, path + ".confidence", "Every hypothesis requires a supported confidence level."));
            }

            TryParseName<MapExperimentHypothesisStatus>(hypothesis.Status, out MapExperimentHypothesisStatus hypothesisStatus);
            string[] support = hypothesis.SupportingExperimentIds?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
            if (hypothesisStatus == MapExperimentHypothesisStatus.Supported && support.Length == 0)
            {
                issues.Add(Issue(MapExperimentValidationCode.SupportedHypothesisWithoutSupportingExperiment, id, path + ".supportingExperimentIds", "A supported hypothesis requires at least one supporting experiment."));
            }

            if (hypothesisStatus == MapExperimentHypothesisStatus.Confirmed)
            {
                if (support.Length < 2)
                {
                    issues.Add(Issue(MapExperimentValidationCode.ConfirmedHypothesisInsufficientSupport, id, path + ".supportingExperimentIds", "A confirmed hypothesis requires at least two independent supporting experiments."));
                }

                if ((hypothesis.ContradictingExperimentIds?.Count ?? 0) > 0 || (hypothesis.UnresolvedQuestions?.Count ?? 0) > 0)
                {
                    issues.Add(Issue(MapExperimentValidationCode.ConfirmedHypothesisHasUnresolvedContradictions, id, path, "A confirmed hypothesis cannot retain contradictions or unresolved questions."));
                }
            }

            if (hypothesisStatus == MapExperimentHypothesisStatus.Rejected && string.IsNullOrWhiteSpace(hypothesis.RejectionReason))
            {
                issues.Add(Issue(MapExperimentValidationCode.RejectedHypothesisMissingReason, id, path + ".rejectionReason", "A rejected hypothesis requires a reason."));
            }

            ValidateSupportingIds(support, id, path + ".supportingExperimentIds", knownIds, issues);
        }
    }

    private static void ValidateConclusions(
        MapExperimentRecord record,
        string id,
        IReadOnlyDictionary<string, MapExperimentRecord> knownIds,
        List<MapExperimentValidationIssue> issues)
    {
        MapExperimentConclusion[] conclusions = record.Conclusions?.ToArray() ?? [];
        for (int index = 0; index < conclusions.Length; index++)
        {
            MapExperimentConclusion conclusion = conclusions[index];
            string path = $"$.conclusions[{index}]";
            if ((conclusion.EvidenceReferences?.Count ?? 0) == 0)
            {
                issues.Add(Issue(MapExperimentValidationCode.ConclusionWithoutEvidence, id, path + ".evidenceReferences", "Every conclusion requires explicit evidence references."));
            }

            string[] support = conclusion.SupportingExperimentIds?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
            ValidateSupportingIds(support, id, path + ".supportingExperimentIds", knownIds, issues);
            TryParseName<MapExperimentConclusionStatus>(conclusion.Status, out MapExperimentConclusionStatus conclusionStatus);
            if (conclusionStatus != MapExperimentConclusionStatus.Confirmed)
            {
                continue;
            }

            if (support.Length < 2)
            {
                issues.Add(Issue(MapExperimentValidationCode.ConfirmedConclusionMissingIndependentSupport, id, path + ".supportingExperimentIds", "A confirmed conclusion requires at least two independent controlled experiments."));
            }

            if (!conclusion.HasReverseOrAlternativeValueEvidence)
            {
                issues.Add(Issue(MapExperimentValidationCode.ConfirmedConclusionMissingReverseEvidence, id, path + ".hasReverseOrAlternativeValueEvidence", "A confirmed conclusion requires reverse-direction or alternative-value evidence."));
            }

            if (!conclusion.BaselineNoiseExcluded)
            {
                issues.Add(Issue(MapExperimentValidationCode.ConfirmedConclusionBaselineNoiseNotExcluded, id, path + ".baselineNoiseExcluded", "Relevant baseline noise must be excluded before confirmation."));
            }

            if (!conclusion.HasRepeatableChangedRangeOrExplicitSearch)
            {
                issues.Add(Issue(MapExperimentValidationCode.ConfirmedConclusionMissingRepeatableEvidence, id, path + ".hasRepeatableChangedRangeOrExplicitSearch", "Confirmation requires a repeatable changed range or explicit search match."));
            }

            bool highConfidence = TryParseName<ResearchConfidenceLevel>(conclusion.Confidence, out ResearchConfidenceLevel confidence) &&
                confidence is ResearchConfidenceLevel.High or ResearchConfidenceLevel.ConfirmedByMultipleIndependentExperiments;
            if (!highConfidence)
            {
                issues.Add(Issue(MapExperimentValidationCode.ConfirmedConclusionLowConfidence, id, path + ".confidence", "A confirmed conclusion requires High or ConfirmedByMultipleIndependentExperiments confidence."));
            }
        }
    }

    private static void ValidateSupportingIds(
        IEnumerable<string> supportingIds,
        string experimentId,
        string path,
        IReadOnlyDictionary<string, MapExperimentRecord> knownIds,
        List<MapExperimentValidationIssue> issues)
    {
        foreach (string supportingId in supportingIds)
        {
            if (!knownIds.ContainsKey(supportingId))
            {
                issues.Add(Issue(MapExperimentValidationCode.UnknownSupportingExperiment, experimentId, path, $"Supporting experiment '{supportingId}' is unknown."));
            }
        }
    }

    private static void ValidateObservations(
        MapExperimentRecord record,
        string id,
        List<MapExperimentValidationIssue> issues)
    {
        MapExperimentObservation[] observations = record.Observations?.ToArray() ?? [];
        for (int index = 0; index < observations.Length; index++)
        {
            string statement = observations[index].Statement ?? string.Empty;
            if (HexByteRegex().Matches(statement).Count > 64)
            {
                issues.Add(Issue(MapExperimentValidationCode.ObservationByteDumpTooLarge, id, $"$.observations[{index}].statement", "Observation byte excerpts are limited to 64 hexadecimal bytes."));
            }
        }
    }

    private static void ValidateSensitiveStrings(
        MapExperimentRecord record,
        string id,
        List<MapExperimentValidationIssue> issues)
    {
        JsonElement element = JsonSerializer.SerializeToElement(record, JsonOptions);
        bool absoluteReported = false;
        bool payloadReported = false;
        Visit(element, "$", (value, path) =>
        {
            if (!payloadReported && Base64LikeRegex().IsMatch(value))
            {
                payloadReported = true;
                issues.Add(Issue(MapExperimentValidationCode.EmbeddedBinaryPayload, id, path, "Metadata must not embed base64-like binary payloads."));
            }

            if (!absoluteReported && LooksLikeAbsolutePath(value))
            {
                absoluteReported = true;
                issues.Add(Issue(MapExperimentValidationCode.AbsoluteLocalPathDetected, id, path, "Metadata must not contain absolute local paths."));
            }
        });
    }

    private static void Visit(JsonElement element, string path, Action<string, string> visitor)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    Visit(property.Value, path + "." + property.Name, visitor);
                }

                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    Visit(item, $"{path}[{index++}]", visitor);
                }

                break;
            case JsonValueKind.String:
                visitor(element.GetString() ?? string.Empty, path);
                break;
        }
    }

    private static void ValidateBaselineCycles(
        IReadOnlyDictionary<string, MapExperimentRecord> records,
        List<MapExperimentValidationIssue> issues)
    {
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (string start in records.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            var chain = new List<string>();
            string? current = start;
            while (current is not null && records.TryGetValue(current, out MapExperimentRecord? record))
            {
                if (positions.TryGetValue(current, out int cycleStart))
                {
                    foreach (string member in chain.Skip(cycleStart).OrderBy(value => value, StringComparer.Ordinal))
                    {
                        if (reported.Add(member))
                        {
                            issues.Add(Issue(MapExperimentValidationCode.CircularBaselineChain, member, "$.baselineExperimentId", "Baseline references form a circular chain."));
                        }
                    }

                    break;
                }

                positions[current] = chain.Count;
                chain.Add(current);
                current = record.BaselineExperimentId;
            }
        }
    }

    private static bool TryParseName<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        string normalized = NormalizeName(value);
        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(normalized, NormalizeName(candidate.ToString()), StringComparison.Ordinal))
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static string NormalizeName(string? value) => new((value ?? string.Empty)
        .Where(char.IsLetterOrDigit)
        .Select(char.ToUpperInvariant)
        .ToArray());

    private static bool LooksLikeAbsolutePath(string value) =>
        WindowsAbsolutePathRegex().IsMatch(value) ||
        value.StartsWith("\\\\", StringComparison.Ordinal) ||
        value.StartsWith("/Users/", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/home/", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase);

    private static MapExperimentValidationResult MergeAndSort(
        MapExperimentValidationResult result,
        IEnumerable<MapExperimentValidationIssue> sourceIssues) =>
        new(result.Experiments, SortIssues(result.Issues.Concat(sourceIssues)));

    private static IReadOnlyList<MapExperimentValidationIssue> SortIssues(IEnumerable<MapExperimentValidationIssue> issues) =>
        issues.OrderBy(issue => issue.ExperimentId, StringComparer.Ordinal)
            .ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray();

    private static MapExperimentValidationIssue Issue(
        MapExperimentValidationCode code,
        string experimentId,
        string propertyPath,
        string message) =>
        new(code, MapExperimentValidationSeverity.Error, experimentId, propertyPath, message);

    private static bool IsExpectedInputException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException;

    [GeneratedRegex(@"^MAP-[0-9]{3}[A-Z0-9]*(?:-[A-Z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExperimentIdRegex();

    [GeneratedRegex(@"^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex(@"\b[0-9a-fA-F]{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex HexByteRegex();

    [GeneratedRegex(@"^[A-Za-z]:[\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsAbsolutePathRegex();

    [GeneratedRegex(@"(?:^|[^A-Za-z0-9+/])[A-Za-z0-9+/]{128,}={0,2}(?:$|[^A-Za-z0-9+/])", RegexOptions.CultureInvariant)]
    private static partial Regex Base64LikeRegex();
}
