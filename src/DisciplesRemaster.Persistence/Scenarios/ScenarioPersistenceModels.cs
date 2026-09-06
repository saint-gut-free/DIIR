using DisciplesRemaster.Content.Scenarios;

namespace DisciplesRemaster.Persistence.Scenarios;

public enum ScenarioPersistenceErrorCode
{
    None,
    InvalidPath,
    FileNotFound,
    FileInaccessible,
    DocumentTooLarge,
    InvalidJson,
    ValidationFailed,
    WriteFailed,
    UnexpectedError,
}

public sealed record ScenarioSerializationResult(
    bool IsSuccess,
    byte[]? Data,
    ScenarioPersistenceErrorCode ErrorCode,
    IReadOnlyList<ScenarioValidationIssue> ValidationIssues,
    string? Message);

public sealed record ScenarioDeserializationResult(
    bool IsSuccess,
    ScenarioDefinition? Scenario,
    ScenarioPersistenceErrorCode ErrorCode,
    IReadOnlyList<ScenarioValidationIssue> ValidationIssues,
    string? Message);

public sealed record ScenarioLoadResult(
    bool IsSuccess,
    ScenarioDefinition? Scenario,
    ScenarioPersistenceErrorCode ErrorCode,
    IReadOnlyList<ScenarioValidationIssue> ValidationIssues,
    string? Message);

public sealed record ScenarioSaveResult(
    bool IsSuccess,
    ScenarioPersistenceErrorCode ErrorCode,
    IReadOnlyList<ScenarioValidationIssue> ValidationIssues,
    string? Message);

public interface IScenarioSerializer
{
    ScenarioSerializationResult Serialize(ScenarioDefinition? scenario);

    ScenarioDeserializationResult Deserialize(ReadOnlySpan<byte> data);
}

public interface IScenarioFileStore
{
    ScenarioLoadResult Load(string path);

    ScenarioSaveResult Save(string path, ScenarioDefinition? scenario);
}
