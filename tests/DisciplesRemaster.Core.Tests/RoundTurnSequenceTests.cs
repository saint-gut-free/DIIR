using DisciplesRemaster.Core.Turns;

namespace DisciplesRemaster.Core.Tests;

public sealed class RoundTurnSequenceTests
{
    [Fact]
    public void Create_PreservesDeclaredOrderAndStartsRoundOne()
    {
        RoundTurnSequenceCreationResult result = RoundTurnSequence.Create(["blue", "red", "neutral"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(["blue", "red", "neutral"], result.Sequence!.ParticipantIds);
        Assert.Equal("blue", result.Sequence.ActiveParticipantId);
        Assert.Equal(1, result.Sequence.RoundNumber);
    }

    [Fact]
    public void Advance_MovesDeterministicallyAndWrapsToNextRound()
    {
        RoundTurnSequence initial = RoundTurnSequence.Create(["blue", "red"]).Sequence!;

        RoundTurnSequence red = initial.Advance();
        RoundTurnSequence nextBlue = red.Advance();

        Assert.Equal("red", red.ActiveParticipantId);
        Assert.Equal(1, red.RoundNumber);
        Assert.Equal("blue", nextBlue.ActiveParticipantId);
        Assert.Equal(2, nextBlue.RoundNumber);
        Assert.Equal("blue", initial.ActiveParticipantId);
        Assert.Equal(1, initial.RoundNumber);
    }

    [Fact]
    public void Create_CopiesCallerOwnedCollection()
    {
        string?[] ids = ["blue", "red"];
        RoundTurnSequence sequence = RoundTurnSequence.Create(ids).Sequence!;

        ids[0] = "changed";

        Assert.Equal("blue", sequence.ActiveParticipantId);
    }

    [Fact]
    public void Create_EmptySequence_ReturnsStructuredIssue()
    {
        RoundTurnSequenceCreationResult result = RoundTurnSequence.Create([]);

        Assert.False(result.IsSuccess);
        Assert.Equal(RoundTurnValidationCode.NoParticipants, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Create_MissingAndDuplicateIds_ReturnsDeterministicIssues()
    {
        RoundTurnSequenceCreationResult result = RoundTurnSequence.Create(["blue", " ", "blue"]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            [RoundTurnValidationCode.ParticipantIdMissing, RoundTurnValidationCode.DuplicateParticipantId],
            result.Issues.Select(issue => issue.Code));
        Assert.Equal(["participantIds[1]", "participantIds[2]"], result.Issues.Select(issue => issue.PropertyPath));
    }

    [Fact]
    public void Create_TooLongId_ReturnsStructuredIssue()
    {
        string id = new('x', RoundTurnRules.MaximumParticipantIdLength + 1);

        RoundTurnSequenceCreationResult result = RoundTurnSequence.Create([id]);

        Assert.Equal(RoundTurnValidationCode.ParticipantIdTooLong, Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Create_TooManyIds_ReturnsStructuredIssue()
    {
        IEnumerable<string> ids = Enumerable.Range(0, RoundTurnRules.MaximumParticipants + 1)
            .Select(index => $"participant-{index}");

        RoundTurnSequenceCreationResult result = RoundTurnSequence.Create(ids);

        Assert.Contains(result.Issues, issue => issue.Code == RoundTurnValidationCode.TooManyParticipants);
    }
}
