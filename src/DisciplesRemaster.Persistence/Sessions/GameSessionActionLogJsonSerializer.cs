using System.Text.Json;
using System.Text.Json.Serialization;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Persistence.Sessions;

public sealed class GameSessionActionLogJsonSerializer : IGameSessionActionLogSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public GameSessionActionLogSerializationResult Serialize(GameSessionActionLog? log)
    {
        IReadOnlyList<GameSessionActionLogIssue> issues = Validate(log);
        if (issues.Count > 0 || log is null)
        {
            return new GameSessionActionLogSerializationResult(
                false,
                null,
                GameSessionActionLogErrorCode.ValidationFailed,
                issues,
                "Game session action log validation failed.");
        }

        var dto = new ActionLogDto(
            log.FormatVersion,
            log.Actions.Select(ToDto).ToArray());
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        if (serialized.LongLength + 1 > GameSessionActionLogFormatV1.MaximumDocumentBytes)
        {
            return new GameSessionActionLogSerializationResult(
                false,
                null,
                GameSessionActionLogErrorCode.DocumentTooLarge,
                [],
                "Game session action log exceeds the safety limit.");
        }

        byte[] data = new byte[serialized.Length + 1];
        serialized.CopyTo(data, 0);
        data[^1] = (byte)'\n';
        return new GameSessionActionLogSerializationResult(
            true,
            data,
            GameSessionActionLogErrorCode.None,
            issues,
            null);
    }

    public GameSessionActionLogDeserializationResult Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            ActionLogDto? dto = JsonSerializer.Deserialize<ActionLogDto>(data, JsonOptions);
            if (dto is null)
            {
                return InvalidJson("Game session action log is missing.");
            }

            List<GameSessionActionLogIssue> parseIssues = [];
            List<GameSessionAction> actions = [];
            ActionDto?[] actionDtos = dto.Actions?.ToArray() ?? [];
            if (dto.Actions is null)
            {
                parseIssues.Add(Issue(
                    GameSessionActionLogIssueCode.ActionsMissing,
                    "actions",
                    nameof(GameSessionActionLogIssueCode.ActionsMissing),
                    "Actions array is required."));
            }
            for (int index = 0; index < actionDtos.Length; index++)
            {
                ActionDto? action = actionDtos[index];
                string path = $"actions[{index}]";
                if (action is null)
                {
                    parseIssues.Add(Issue(
                        GameSessionActionLogIssueCode.ActionMissing,
                        path,
                        nameof(GameSessionActionLogIssueCode.ActionMissing),
                        "Action is missing."));
                    continue;
                }

                if (!TryParseKind(action.Kind, out GameSessionActionKind kind))
                {
                    parseIssues.Add(Issue(
                        GameSessionActionLogIssueCode.UnsupportedActionKind,
                        $"{path}.kind",
                        action.Kind ?? "<missing>",
                        "Action kind is unsupported."));
                    continue;
                }

                GridPosition? destination = action.X.HasValue && action.Y.HasValue
                    ? new GridPosition(action.X.Value, action.Y.Value)
                    : null;
                actions.Add(new GameSessionAction(action.Sequence, kind, action.ActorId, destination));
            }

            var log = new GameSessionActionLog(dto.FormatVersion, actions);
            IReadOnlyList<GameSessionActionLogIssue> validationIssues = Validate(log);
            GameSessionActionLogIssue[] issues = parseIssues
                .Concat(validationIssues)
                .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
                .ToArray();
            if (issues.Length > 0)
            {
                return new GameSessionActionLogDeserializationResult(
                    false,
                    log,
                    GameSessionActionLogErrorCode.ValidationFailed,
                    issues,
                    "Game session action log validation failed.");
            }

            return new GameSessionActionLogDeserializationResult(
                true,
                log,
                GameSessionActionLogErrorCode.None,
                [],
                null);
        }
        catch (JsonException)
        {
            return InvalidJson("Document is not valid JSON for game session action log version 1.");
        }
        catch (NotSupportedException)
        {
            return InvalidJson("Game session action log contains unsupported JSON values.");
        }
    }

    private static IReadOnlyList<GameSessionActionLogIssue> Validate(GameSessionActionLog? log)
    {
        if (log is null)
        {
            return [Issue(
                GameSessionActionLogIssueCode.DocumentMissing,
                "$",
                nameof(GameSessionActionLogIssueCode.DocumentMissing),
                "Game session action log is missing.")];
        }

        List<GameSessionActionLogIssue> issues = [];
        if (log.FormatVersion != GameSessionActionLogFormatV1.Version)
        {
            issues.Add(Issue(
                GameSessionActionLogIssueCode.UnsupportedFormatVersion,
                "formatVersion",
                log.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Only game session action log format version 1 is supported."));
        }

        if (log.Actions is null)
        {
            issues.Add(Issue(
                GameSessionActionLogIssueCode.ActionsMissing,
                "actions",
                nameof(GameSessionActionLogIssueCode.ActionsMissing),
                "Actions array is required."));
            return Sort(issues);
        }

        IReadOnlyList<GameSessionActionIssue> actionIssues = GameSessionActionValidation.Validate(
            log.Actions.Cast<GameSessionAction?>().ToArray());
        issues.AddRange(actionIssues.Select(issue => Issue(
            GameSessionActionLogIssueCode.ActionValidationFailed,
            issue.PropertyPath,
            issue.DetailCode,
            issue.Message)));
        return Sort(issues);
    }

    private static IReadOnlyList<GameSessionActionLogIssue> Sort(
        IEnumerable<GameSessionActionLogIssue> issues) =>
        issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
            .ToArray();

    private static ActionDto ToDto(GameSessionAction action) =>
        action.Kind switch
        {
            GameSessionActionKind.AdvanceTurn => new ActionDto(
                action.Sequence,
                "advanceTurn",
                null,
                null,
                null),
            GameSessionActionKind.MoveActor => new ActionDto(
                action.Sequence,
                "moveActor",
                action.ActorId,
                action.Destination?.X,
                action.Destination?.Y),
            _ => throw new InvalidOperationException("Validated action kind is unsupported."),
        };

    private static bool TryParseKind(string? value, out GameSessionActionKind kind)
    {
        switch (value)
        {
            case "advanceTurn":
                kind = GameSessionActionKind.AdvanceTurn;
                return true;
            case "moveActor":
                kind = GameSessionActionKind.MoveActor;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static GameSessionActionLogIssue Issue(
        GameSessionActionLogIssueCode code,
        string propertyPath,
        string detailCode,
        string message) =>
        new(code, propertyPath, detailCode, message);

    private static GameSessionActionLogDeserializationResult InvalidJson(string message) =>
        new(false, null, GameSessionActionLogErrorCode.InvalidJson, [], message);

    private sealed record ActionLogDto(
        int FormatVersion,
        IReadOnlyList<ActionDto?>? Actions);

    private sealed record ActionDto(
        int Sequence,
        string? Kind,
        string? ActorId,
        int? X,
        int? Y);
}
