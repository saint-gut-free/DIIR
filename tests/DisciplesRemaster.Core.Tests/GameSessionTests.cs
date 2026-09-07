using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Core.Tests;

public sealed class GameSessionTests
{
    private readonly GameSessionService service = new(new MovementPlanner(new GridPathfinder()));

    [Fact]
    public void Create_ValidDefinitions_ReturnsDeterministicImmutableState()
    {
        GameActorDefinition[] definitions =
        [
            new("z-actor", "red", new GridPosition(3, 3), 4),
            new("a-actor", "blue", new GridPosition(0, 0), 5),
        ];

        GameSessionCreationResult result = GameSessionState.Create(new GridSize(5, 5), ["blue", "red"], definitions);
        definitions[1] = definitions[1] with { Id = "changed" };

        Assert.True(result.IsSuccess);
        Assert.Equal(["a-actor", "z-actor"], result.Session!.Actors.Keys);
        Assert.Equal("blue", result.Session.Turn.ActiveParticipantId);
        Assert.Equal(5, result.Session.Actors["a-actor"].RemainingMovement);
    }

    [Fact]
    public void Create_InvalidTurnSequence_MapsDetailedIssues()
    {
        GameSessionCreationResult result = GameSessionState.Create(new GridSize(2, 2), ["blue", "blue"], []);

        GameSessionValidationIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GameSessionValidationCode.TurnSequenceInvalid, issue.Code);
        Assert.Equal("DuplicateParticipantId", issue.DetailCode);
    }

    [Fact]
    public void Create_InvalidActors_ReturnsSortedStructuredIssues()
    {
        GameActorDefinition?[] actors =
        [
            new("same", "missing", new GridPosition(5, 0), -1),
            new("same", "blue", new GridPosition(0, 0), 1),
            null,
        ];

        GameSessionCreationResult result = GameSessionState.Create(new GridSize(2, 2), ["blue"], actors);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionValidationCode.UnknownOwnerParticipant);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionValidationCode.ActorOutsideGrid);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionValidationCode.InvalidMovementAllowance);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionValidationCode.DuplicateActorId);
        Assert.Contains(result.Issues, issue => issue.Code == GameSessionValidationCode.ActorMissing);
        Assert.Equal(
            result.Issues.OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal).ThenBy(issue => issue.Code),
            result.Issues);
    }

    [Fact]
    public void MoveActor_ActiveOwnerAndAffordablePath_UpdatesNewStateOnly()
    {
        GameSessionState initial = CreateSession();

        GameSessionMovementResult result = service.MoveActor(
            initial,
            "blue-actor",
            new GridPosition(2, 0),
            OrthogonalGridTopology.Instance,
            _ => true);

        Assert.True(result.IsSuccess);
        Assert.Equal(new GridPosition(0, 0), initial.Actors["blue-actor"].Position);
        Assert.Equal(3, initial.Actors["blue-actor"].RemainingMovement);
        Assert.Equal(new GridPosition(2, 0), result.Session!.Actors["blue-actor"].Position);
        Assert.Equal(1, result.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Fact]
    public void MoveActor_NonActiveOwner_IsRejectedWithoutPlanning()
    {
        GameSessionMovementResult result = service.MoveActor(
            CreateSession(),
            "red-actor",
            new GridPosition(3, 2),
            OrthogonalGridTopology.Instance,
            _ => true);

        Assert.Equal(GameSessionMovementStatus.ActorNotControlledByActiveParticipant, result.Status);
        Assert.Null(result.Session);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void MoveActor_UnknownActor_IsRejected()
    {
        GameSessionMovementResult result = service.MoveActor(
            CreateSession(),
            "missing",
            new GridPosition(1, 1),
            OrthogonalGridTopology.Instance,
            _ => true);

        Assert.Equal(GameSessionMovementStatus.ActorNotFound, result.Status);
    }

    [Fact]
    public void MoveActor_InsufficientBudget_PreservesExactPlanFailure()
    {
        GameSessionMovementResult result = service.MoveActor(
            CreateSession(),
            "blue-actor",
            new GridPosition(4, 0),
            OrthogonalGridTopology.Instance,
            _ => true);

        Assert.Equal(GameSessionMovementStatus.MovementPlanRejected, result.Status);
        Assert.Equal(MovementPlanStatus.MovementBudgetExceeded, result.Plan!.Status);
        Assert.Null(result.Session);
    }

    [Fact]
    public void AdvanceTurn_SelectsNextParticipantAndResetsOnlyItsActors()
    {
        GameSessionState initial = CreateSession();
        GameSessionState redTurn = service.AdvanceTurn(initial);
        GameSessionMovementResult moved = service.MoveActor(
            redTurn,
            "red-actor",
            new GridPosition(3, 2),
            OrthogonalGridTopology.Instance,
            _ => true);
        GameSessionState blueTurn = service.AdvanceTurn(moved.Session!);
        GameSessionState nextRedTurn = service.AdvanceTurn(blueTurn);

        Assert.Equal("red", redTurn.Turn.ActiveParticipantId);
        Assert.Equal(1, moved.Session!.Actors["red-actor"].RemainingMovement);
        Assert.Equal(3, blueTurn.Actors["blue-actor"].RemainingMovement);
        Assert.Equal(2, nextRedTurn.Actors["red-actor"].RemainingMovement);
        Assert.Equal(2, nextRedTurn.Turn.RoundNumber);
    }

    private static GameSessionState CreateSession() =>
        GameSessionState.Create(
            new GridSize(5, 5),
            ["blue", "red"],
            [
                new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 3),
                new GameActorDefinition("red-actor", "red", new GridPosition(4, 2), 2),
            ]).Session!;
}
