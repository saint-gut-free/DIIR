using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Core.Tests;

public sealed class GameSessionActionProcessorTests
{
    private readonly GameSessionActionProcessor processor = new(
        new GameSessionService(new MovementPlanner(new GridPathfinder())));

    [Fact]
    public void Apply_EmptyBatch_ReturnsOriginalSession()
    {
        GameSessionState session = CreateSession();

        GameSessionActionBatchResult result = Apply(session, []);

        Assert.True(result.IsSuccess);
        Assert.Same(session, result.Session);
        Assert.Equal(0, result.AppliedActions);
    }

    [Fact]
    public void Apply_OrderedActions_ProducesDeterministicImmutableState()
    {
        GameSessionState session = CreateSession();
        GameSessionAction[] actions =
        [
            GameSessionAction.MoveActor(1, "blue-actor", new GridPosition(2, 0)),
            GameSessionAction.AdvanceTurn(2),
            GameSessionAction.MoveActor(3, "red-actor", new GridPosition(3, 3)),
        ];

        GameSessionActionBatchResult first = Apply(session, actions);
        GameSessionActionBatchResult second = Apply(session, actions);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(3, first.AppliedActions);
        Assert.Equal(new GridPosition(0, 0), session.Actors["blue-actor"].Position);
        Assert.Equal(new GridPosition(2, 0), first.Session!.Actors["blue-actor"].Position);
        Assert.Equal(new GridPosition(3, 3), first.Session.Actors["red-actor"].Position);
        Assert.Equal(first.Session.Turn.ActiveParticipantId, second.Session!.Turn.ActiveParticipantId);
        Assert.Equal(first.Session.Turn.RoundNumber, second.Session.Turn.RoundNumber);
        Assert.Equal(
            first.Session.Actors.Values.OrderBy(actor => actor.Id, StringComparer.Ordinal),
            second.Session.Actors.Values.OrderBy(actor => actor.Id, StringComparer.Ordinal));
    }

    [Fact]
    public void Apply_RejectedAction_DoesNotExposePartialSession()
    {
        GameSessionState session = CreateSession();
        GameSessionAction[] actions =
        [
            GameSessionAction.MoveActor(1, "blue-actor", new GridPosition(1, 0)),
            GameSessionAction.MoveActor(2, "blue-actor", new GridPosition(4, 4)),
        ];

        GameSessionActionBatchResult result = Apply(session, actions);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Session);
        Assert.Equal(1, result.AppliedActions);
        GameSessionActionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GameSessionActionIssueCode.ActionRejected, issue.Code);
        Assert.Equal(nameof(MovementPlanStatus.MovementBudgetExceeded), issue.DetailCode);
        Assert.Equal(new GridPosition(0, 0), session.Actors["blue-actor"].Position);
    }

    [Fact]
    public void Apply_BlockedDestination_ReturnsStructuredRejection()
    {
        GameSessionState session = CreateSession();

        GameSessionActionBatchResult result = processor.Apply(
            session,
            [GameSessionAction.MoveActor(1, "blue-actor", new GridPosition(1, 0))],
            OrthogonalGridTopology.Instance,
            position => position != new GridPosition(1, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(nameof(MovementPlanStatus.DestinationBlocked), Assert.Single(result.Issues).DetailCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Apply_NonContiguousSequence_IsRejectedBeforeExecution(int sequence)
    {
        GameSessionState session = CreateSession();

        GameSessionActionBatchResult result = Apply(session, [GameSessionAction.AdvanceTurn(sequence)]);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.AppliedActions);
        Assert.Equal(GameSessionActionIssueCode.InvalidSequence, Assert.Single(result.Issues).Code);
        Assert.Equal("blue", session.Turn.ActiveParticipantId);
    }

    [Fact]
    public void Apply_InvalidActionShapes_ReturnSortedIssues()
    {
        GameSessionAction?[] actions =
        [
            null,
            new GameSessionAction(2, GameSessionActionKind.MoveActor, null, null),
            new GameSessionAction(3, GameSessionActionKind.AdvanceTurn, "actor", new GridPosition(1, 1)),
        ];

        GameSessionActionBatchResult result = Apply(CreateSession(), actions);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionIssueCode.ActionMissing);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionIssueCode.ActorIdMissing);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionIssueCode.DestinationMissing);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionIssueCode.UnexpectedActorId);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionIssueCode.UnexpectedDestination);
        Assert.Equal(
            result.Issues.OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal).ThenBy(issue => issue.Code),
            result.Issues);
    }

    [Fact]
    public void Apply_TooManyActions_IsRejectedWithoutExecution()
    {
        GameSessionAction?[] actions = Enumerable.Range(1, GameSessionActionRules.MaximumActionsPerBatch + 1)
            .Select(GameSessionAction.AdvanceTurn)
            .ToArray();

        GameSessionActionBatchResult result = Apply(CreateSession(), actions);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionActionIssueCode.TooManyActions);
        Assert.Equal(0, result.AppliedActions);
    }

    [Fact]
    public void Apply_MoveOwnedByInactiveParticipant_IsRejected()
    {
        GameSessionActionBatchResult result = Apply(
            CreateSession(),
            [GameSessionAction.MoveActor(1, "red-actor", new GridPosition(3, 3))]);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            nameof(GameSessionMovementStatus.ActorNotControlledByActiveParticipant),
            Assert.Single(result.Issues).DetailCode);
    }

    [Fact]
    public void Apply_OverlongActorId_IsRejectedBeforeExecution()
    {
        string actorId = new('a', GameSessionRules.MaximumActorIdLength + 1);

        GameSessionActionBatchResult result = Apply(
            CreateSession(),
            [GameSessionAction.MoveActor(1, actorId, new GridPosition(1, 0))]);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameSessionActionIssueCode.ActorIdTooLong, Assert.Single(result.Issues).Code);
        Assert.Equal(0, result.AppliedActions);
    }

    private GameSessionActionBatchResult Apply(
        GameSessionState session,
        IEnumerable<GameSessionAction?> actions) =>
        processor.Apply(session, actions, OrthogonalGridTopology.Instance, _ => true);

    private static GameSessionState CreateSession() =>
        GameSessionState.Create(
            new GridSize(5, 5),
            ["blue", "red"],
            [
                new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 3),
                new GameActorDefinition("red-actor", "red", new GridPosition(4, 3), 2),
            ]).Session!;
}
