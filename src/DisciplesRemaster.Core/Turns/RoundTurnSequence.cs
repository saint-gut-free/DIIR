using System.Collections.ObjectModel;

namespace DisciplesRemaster.Core.Turns;

public static class RoundTurnRules
{
    public const int MaximumParticipants = 1_024;
    public const int MaximumParticipantIdLength = 96;
}

public enum RoundTurnValidationCode
{
    NoParticipants,
    TooManyParticipants,
    ParticipantIdMissing,
    ParticipantIdTooLong,
    DuplicateParticipantId,
}

public sealed record RoundTurnValidationIssue(
    RoundTurnValidationCode Code,
    string PropertyPath,
    string Message);

public sealed record RoundTurnSequenceCreationResult(
    RoundTurnSequence? Sequence,
    IReadOnlyList<RoundTurnValidationIssue> Issues)
{
    public bool IsSuccess => Sequence is not null && Issues.Count == 0;
}

/// <summary>
/// Immutable, deterministic, project-owned round-robin turn sequence.
/// It does not claim compatibility with any original-game turn rules.
/// </summary>
public sealed class RoundTurnSequence
{
    private readonly ReadOnlyCollection<string> participantIds;

    private RoundTurnSequence(ReadOnlyCollection<string> participantIds, int activeIndex, long roundNumber)
    {
        this.participantIds = participantIds;
        ActiveIndex = activeIndex;
        RoundNumber = roundNumber;
    }

    public IReadOnlyList<string> ParticipantIds => participantIds;

    public int ActiveIndex { get; }

    public string ActiveParticipantId => participantIds[ActiveIndex];

    public long RoundNumber { get; }

    public static RoundTurnSequenceCreationResult Create(IEnumerable<string?> participantIds)
    {
        ArgumentNullException.ThrowIfNull(participantIds);

        string?[] values = participantIds.ToArray();
        List<RoundTurnValidationIssue> issues = [];
        if (values.Length == 0)
        {
            issues.Add(Issue(RoundTurnValidationCode.NoParticipants, "participantIds", "At least one participant is required."));
        }

        if (values.Length > RoundTurnRules.MaximumParticipants)
        {
            issues.Add(Issue(
                RoundTurnValidationCode.TooManyParticipants,
                "participantIds",
                "The turn sequence contains too many participants."));
        }

        HashSet<string> uniqueIds = new(StringComparer.Ordinal);
        for (int index = 0; index < values.Length; index++)
        {
            string? value = values[index];
            string path = $"participantIds[{index}]";
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(Issue(RoundTurnValidationCode.ParticipantIdMissing, path, "Participant ID is required."));
                continue;
            }

            if (value.Length > RoundTurnRules.MaximumParticipantIdLength)
            {
                issues.Add(Issue(RoundTurnValidationCode.ParticipantIdTooLong, path, "Participant ID is too long."));
            }

            if (!uniqueIds.Add(value))
            {
                issues.Add(Issue(RoundTurnValidationCode.DuplicateParticipantId, path, "Participant IDs must be unique."));
            }
        }

        RoundTurnValidationIssue[] sortedIssues = issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ToArray();
        if (sortedIssues.Length > 0)
        {
            return new RoundTurnSequenceCreationResult(null, sortedIssues);
        }

        var immutableIds = new ReadOnlyCollection<string>(values.Cast<string>().ToArray());
        return new RoundTurnSequenceCreationResult(new RoundTurnSequence(immutableIds, 0, 1), []);
    }

    public RoundTurnSequence Advance()
    {
        int nextIndex = ActiveIndex + 1;
        long nextRound = RoundNumber;
        if (nextIndex == participantIds.Count)
        {
            nextIndex = 0;
            nextRound = checked(RoundNumber + 1);
        }

        return new RoundTurnSequence(participantIds, nextIndex, nextRound);
    }

    private static RoundTurnValidationIssue Issue(
        RoundTurnValidationCode code,
        string propertyPath,
        string message) =>
        new(code, propertyPath, message);
}
