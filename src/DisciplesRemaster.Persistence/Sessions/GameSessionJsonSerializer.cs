using System.Text.Json;
using System.Text.Json.Serialization;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Persistence.Sessions;

public sealed class GameSessionJsonSerializer : IGameSessionSerializer
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

    public GameSessionSerializationResult Serialize(GameSessionState? session)
    {
        if (session is null)
        {
            return SerializationFailure(
                GameSessionPersistenceErrorCode.ValidationFailed,
                [Issue(GameSessionPersistenceIssueCode.SessionValidationFailed, "$", "SessionMissing", "Game session is missing.")],
                "Game session validation failed.");
        }

        var dto = new GameSessionDto(
            GameSessionCheckpointFormatV1.Version,
            session.MapSize.Width,
            session.MapSize.Height,
            session.Turn.ParticipantIds,
            session.Turn.ActiveIndex,
            session.Turn.RoundNumber,
            session.Actors.Values
                .OrderBy(actor => actor.Id, StringComparer.Ordinal)
                .Select(actor => new GameActorDto(
                    actor.Id,
                    actor.OwnerParticipantId,
                    actor.Position.X,
                    actor.Position.Y,
                    actor.MovementAllowance,
                    actor.RemainingMovement))
                .ToArray());
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        byte[] data = new byte[serialized.Length + 1];
        serialized.CopyTo(data, 0);
        data[^1] = (byte)'\n';
        return new GameSessionSerializationResult(
            true,
            data,
            GameSessionPersistenceErrorCode.None,
            [],
            null);
    }

    public GameSessionDeserializationResult Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            GameSessionDto? dto = JsonSerializer.Deserialize<GameSessionDto>(data, JsonOptions);
            if (dto is null)
            {
                return InvalidJson("Game session JSON contains no document.");
            }

            if (dto.FormatVersion != GameSessionCheckpointFormatV1.Version)
            {
                return ValidationFailure(
                [
                    Issue(
                        GameSessionPersistenceIssueCode.UnsupportedFormatVersion,
                        "formatVersion",
                        dto.FormatVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        "Game session checkpoint version is unsupported."),
                ]);
            }

            if (dto.MapWidth <= 0 || dto.MapHeight <= 0)
            {
                return ValidationFailure(
                [
                    Issue(
                        GameSessionPersistenceIssueCode.MapDimensionsInvalid,
                        "mapSize",
                        nameof(GameSessionPersistenceIssueCode.MapDimensionsInvalid),
                        "Game session map dimensions must be positive."),
                ]);
            }

            GameActorState?[] actors = (dto.Actors ?? [])
                .Select(actor => actor is null
                    ? null
                    : new GameActorState(
                        actor.Id ?? string.Empty,
                        actor.OwnerParticipantId ?? string.Empty,
                        new GridPosition(actor.X, actor.Y),
                        actor.MovementAllowance,
                        actor.RemainingMovement))
                .ToArray();
            GameSessionCreationResult restoration = GameSessionState.Restore(
                new GridSize(dto.MapWidth, dto.MapHeight),
                dto.ParticipantIds ?? [],
                dto.ActiveParticipantIndex,
                dto.RoundNumber,
                actors);
            if (!restoration.IsSuccess || restoration.Session is null)
            {
                return ValidationFailure(restoration.Issues.Select(issue => Issue(
                    GameSessionPersistenceIssueCode.SessionValidationFailed,
                    issue.PropertyPath,
                    issue.DetailCode,
                    issue.Message)));
            }

            return new GameSessionDeserializationResult(
                true,
                restoration.Session,
                GameSessionPersistenceErrorCode.None,
                [],
                null);
        }
        catch (JsonException)
        {
            return InvalidJson("Game session document is not valid JSON for checkpoint format version 1.");
        }
        catch (NotSupportedException)
        {
            return InvalidJson("Game session document contains unsupported JSON values.");
        }
    }

    private static GameSessionSerializationResult SerializationFailure(
        GameSessionPersistenceErrorCode code,
        IReadOnlyList<GameSessionPersistenceIssue> issues,
        string message) =>
        new(false, null, code, issues, message);

    private static GameSessionDeserializationResult InvalidJson(string message) =>
        new(false, null, GameSessionPersistenceErrorCode.InvalidJson, [], message);

    private static GameSessionDeserializationResult ValidationFailure(
        IEnumerable<GameSessionPersistenceIssue> issues) =>
        new(
            false,
            null,
            GameSessionPersistenceErrorCode.ValidationFailed,
            issues
                .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.DetailCode, StringComparer.Ordinal)
                .ToArray(),
            "Game session validation failed.");

    private static GameSessionPersistenceIssue Issue(
        GameSessionPersistenceIssueCode code,
        string propertyPath,
        string detailCode,
        string message) =>
        new(code, propertyPath, detailCode, message);

    private sealed record GameSessionDto(
        int FormatVersion,
        int MapWidth,
        int MapHeight,
        IReadOnlyList<string?>? ParticipantIds,
        int ActiveParticipantIndex,
        long RoundNumber,
        IReadOnlyList<GameActorDto?>? Actors);

    private sealed record GameActorDto(
        string? Id,
        string? OwnerParticipantId,
        int X,
        int Y,
        int MovementAllowance,
        int RemainingMovement);
}
