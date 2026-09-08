using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;

namespace DisciplesRemaster.Game.Tests;

public sealed class NativeProjectSessionControllerTests
{
    [Fact]
    public void SelectActor_NonActiveOwner_AllowsInspectionButCoreRejectsMovement()
    {
        NativeProjectSceneData original = CreateProject();
        NativeProjectSessionController controller = CreateController(original);

        ProjectSessionInteractionResult selection = controller.SelectActor("red-actor");
        ProjectSessionInteractionResult movement = controller.PreviewMovement(new GridPosition(3, 3));

        Assert.True(selection.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.ActorSelected, selection.Status);
        Assert.Equal("red-actor", controller.SelectedActorId);
        Assert.False(movement.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.MovementRejected, movement.Status);
        Assert.Equal(nameof(GameSessionMovementStatus.ActorNotControlledByActiveParticipant), movement.DetailCode);
        Assert.Null(movement.Plan);
        Assert.Null(controller.Preview);
        Assert.Same(original, controller.CurrentProject);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("missing")]
    [InlineData("BLUE-ACTOR")]
    public void SelectActor_InvalidId_LeavesExistingSelectionAndPreviewIntact(string actorId)
    {
        NativeProjectSessionController controller = CreateController();
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(1, 0));
        ProjectMovementPreview previous = controller.Preview!;
        NativeProjectSceneData original = controller.CurrentProject;

        ProjectSessionInteractionResult result = controller.SelectActor(actorId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.ActorNotFound, result.Status);
        Assert.Equal("blue-actor", controller.SelectedActorId);
        Assert.Same(previous, controller.Preview);
        Assert.Same(original, controller.CurrentProject);
    }

    [Fact]
    public void PreviewMovement_ValidDestination_DoesNotChangeCheckpointOrSpendMovement()
    {
        NativeProjectSceneData original = CreateProject();
        NativeProjectSessionController controller = CreateController(original);
        controller.SelectActor("blue-actor");

        ProjectSessionInteractionResult result = controller.PreviewMovement(new GridPosition(2, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.MovementPreviewReady, result.Status);
        Assert.Equal(2, result.Plan!.RequiredSteps);
        Assert.Equal(2, result.Plan.RemainingMovement);
        Assert.Equal(new GridPosition(0, 0), result.Plan.Path[0]);
        Assert.Equal(new GridPosition(2, 0), result.Plan.Path[^1]);
        Assert.Equal("blue-actor", controller.Preview!.ActorId);
        Assert.Same(result.Plan, controller.Preview.Plan);
        Assert.Same(original, controller.CurrentProject);
        Assert.Equal(new GridPosition(0, 0), original.Session!.Actors["blue-actor"].Position);
        Assert.Equal(4, original.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Fact]
    public void ConfirmMovement_ValidPreview_AppliesNewCheckpointExactlyOnce()
    {
        NativeProjectSceneData original = CreateProject();
        GameActorState originalActor = original.Session!.Actors["blue-actor"];
        NativeProjectSessionController controller = CreateController(original);
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(2, 0));

        ProjectSessionInteractionResult result = controller.ConfirmMovement();
        NativeProjectSceneData afterMove = controller.CurrentProject;
        ProjectSessionInteractionResult repeatedConfirmation = controller.ConfirmMovement();

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.MovementApplied, result.Status);
        Assert.NotSame(original, afterMove);
        Assert.NotSame(original.Session, afterMove.Session);
        Assert.Same(original.Scene, afterMove.Scene);
        Assert.Same(original.ContentPackageIds, afterMove.ContentPackageIds);
        Assert.Equal(original.ProjectId, afterMove.ProjectId);
        Assert.Equal(new GridPosition(2, 0), afterMove.Session!.Actors["blue-actor"].Position);
        Assert.Equal(2, afterMove.Session.Actors["blue-actor"].RemainingMovement);
        Assert.Equal(original.Session.Actors["red-actor"], afterMove.Session.Actors["red-actor"]);
        Assert.Same(originalActor, original.Session.Actors["blue-actor"]);
        Assert.Equal(new GridPosition(0, 0), originalActor.Position);
        Assert.Equal(4, originalActor.RemainingMovement);
        Assert.Equal("blue-actor", controller.SelectedActorId);
        Assert.Null(controller.Preview);
        Assert.Equal(ProjectSessionInteractionStatus.NoMovementPreview, repeatedConfirmation.Status);
        Assert.False(repeatedConfirmation.IsSuccess);
        Assert.Same(afterMove, controller.CurrentProject);
    }

    [Fact]
    public void ConfirmMovement_PolicyNowBlocksDestination_RejectsWithoutChangingCheckpoint()
    {
        var destination = new GridPosition(2, 0);
        bool allowDestination = true;
        NativeProjectSceneData original = CreateProject();
        NativeProjectSessionController controller = CreateController(
            original,
            position => allowDestination || position != destination);
        controller.SelectActor("blue-actor");
        Assert.True(controller.PreviewMovement(destination).IsSuccess);
        allowDestination = false;

        ProjectSessionInteractionResult result = controller.ConfirmMovement();

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.MovementRejected, result.Status);
        Assert.Equal(nameof(MovementPlanStatus.DestinationBlocked), result.DetailCode);
        Assert.Same(original, controller.CurrentProject);
        Assert.Null(controller.Preview);
        Assert.Equal("blue-actor", controller.SelectedActorId);
        Assert.Equal(ProjectSessionInteractionStatus.NoMovementPreview, controller.ConfirmMovement().Status);
    }

    [Fact]
    public void ConfirmMovement_PolicyChangesRoute_RecalculatesPathAndActualCost()
    {
        bool blockDirectRoute = false;
        NativeProjectSessionController controller = CreateController(
            canEnter: position => !blockDirectRoute || position != new GridPosition(1, 0));
        controller.SelectActor("blue-actor");
        ProjectSessionInteractionResult preview = controller.PreviewMovement(new GridPosition(2, 0));
        blockDirectRoute = true;

        ProjectSessionInteractionResult result = controller.ConfirmMovement();

        Assert.Equal(2, preview.Plan!.RequiredSteps);
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Plan!.RequiredSteps);
        Assert.DoesNotContain(new GridPosition(1, 0), result.Plan.Path);
        Assert.Equal(0, controller.CurrentProject.Session!.Actors["blue-actor"].RemainingMovement);
        Assert.Equal(new GridPosition(2, 0), controller.CurrentProject.Session.Actors["blue-actor"].Position);
    }

    [Fact]
    public void PreviewMovement_NewDestination_ReplacesOldPreview()
    {
        NativeProjectSessionController controller = CreateController();
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(1, 0));

        ProjectSessionInteractionResult latest = controller.PreviewMovement(new GridPosition(0, 2));
        controller.ConfirmMovement();

        Assert.True(latest.IsSuccess);
        Assert.Equal(new GridPosition(0, 2), controller.CurrentProject.Session!.Actors["blue-actor"].Position);
        Assert.Equal(2, controller.CurrentProject.Session.Actors["blue-actor"].RemainingMovement);
    }

    [Theory]
    [InlineData(-1, 0, MovementPlanStatus.DestinationOutsideGrid)]
    [InlineData(6, 0, MovementPlanStatus.DestinationOutsideGrid)]
    [InlineData(5, 0, MovementPlanStatus.MovementBudgetExceeded)]
    public void PreviewMovement_RejectedNewDestination_ClearsStalePreviewAndPreservesCheckpoint(
        int x,
        int y,
        MovementPlanStatus expectedDetail)
    {
        NativeProjectSessionController controller = CreateController();
        NativeProjectSceneData original = controller.CurrentProject;
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(1, 0));

        ProjectSessionInteractionResult result = controller.PreviewMovement(new GridPosition(x, y));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.MovementRejected, result.Status);
        Assert.Equal(expectedDetail.ToString(), result.DetailCode);
        Assert.Null(controller.Preview);
        Assert.Same(original, controller.CurrentProject);
        Assert.Equal(ProjectSessionInteractionStatus.NoMovementPreview, controller.ConfirmMovement().Status);
    }

    [Theory]
    [InlineData("blue-actor")]
    [InlineData("red-actor")]
    public void SelectActor_ValidSelection_DiscardsPreviousPreview(string actorId)
    {
        NativeProjectSessionController controller = CreateController();
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(1, 0));
        NativeProjectSceneData original = controller.CurrentProject;

        ProjectSessionInteractionResult result = controller.SelectActor(actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(actorId, controller.SelectedActorId);
        Assert.Null(controller.Preview);
        Assert.Same(original, controller.CurrentProject);
        Assert.Equal(ProjectSessionInteractionStatus.NoMovementPreview, controller.ConfirmMovement().Status);
    }

    [Fact]
    public void ClearSelection_CancelsPreviewAndRequiresSelectionForNextMovement()
    {
        NativeProjectSessionController controller = CreateController();
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(1, 0));
        NativeProjectSceneData original = controller.CurrentProject;

        ProjectSessionInteractionResult result = controller.ClearSelection();
        ProjectSessionInteractionResult preview = controller.PreviewMovement(new GridPosition(1, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.SelectionCleared, result.Status);
        Assert.Null(controller.SelectedActorId);
        Assert.Null(controller.Preview);
        Assert.False(preview.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.NoActorSelected, preview.Status);
        Assert.Equal(ProjectSessionInteractionStatus.NoMovementPreview, controller.ConfirmMovement().Status);
        Assert.Same(original, controller.CurrentProject);
        Assert.True(controller.ClearSelection().IsSuccess);
    }

    [Fact]
    public void AdvanceTurn_ClearsSelectionAndPreviewAndResetsOnlyIncomingParticipantMovement()
    {
        NativeProjectSceneData original = CreateProject();
        GameSessionState checkpoint = GameSessionState.Restore(
            original.Scene.Size,
            ["blue", "red"],
            activeParticipantIndex: 0,
            roundNumber: 4,
            [
                new GameActorState("blue-actor", "blue", new GridPosition(0, 0), 4, 2),
                new GameActorState("red-actor", "red", new GridPosition(4, 3), 3, 1),
            ]).Session!;
        original = original with { Session = checkpoint };
        NativeProjectSessionController controller = CreateController(original);
        controller.SelectActor("blue-actor");
        controller.PreviewMovement(new GridPosition(1, 0));

        ProjectSessionInteractionResult result = controller.AdvanceTurn();
        GameSessionState redTurn = controller.CurrentProject.Session!;

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.TurnAdvanced, result.Status);
        Assert.Equal("red", redTurn.Turn.ActiveParticipantId);
        Assert.Equal(4, redTurn.Turn.RoundNumber);
        Assert.Equal(3, redTurn.Actors["red-actor"].RemainingMovement);
        Assert.Equal(2, redTurn.Actors["blue-actor"].RemainingMovement);
        Assert.Equal(1, checkpoint.Actors["red-actor"].RemainingMovement);
        Assert.Null(controller.SelectedActorId);
        Assert.Null(controller.Preview);
        Assert.Equal(ProjectSessionInteractionStatus.NoMovementPreview, controller.ConfirmMovement().Status);

        controller.AdvanceTurn();

        Assert.Equal("blue", controller.CurrentProject.Session!.Turn.ActiveParticipantId);
        Assert.Equal(5, controller.CurrentProject.Session.Turn.RoundNumber);
        Assert.Equal(4, controller.CurrentProject.Session.Actors["blue-actor"].RemainingMovement);
        Assert.Equal(2, redTurn.Actors["blue-actor"].RemainingMovement);
        Assert.Same(checkpoint, original.Session);
    }

    [Fact]
    public void AdvanceTurn_RoundOverflow_ReturnsStructuredFailureWithoutChangingState()
    {
        NativeProjectSceneData original = CreateProject();
        GameSessionState checkpoint = GameSessionState.Restore(
            original.Scene.Size,
            ["blue", "red"],
            activeParticipantIndex: 1,
            roundNumber: long.MaxValue,
            original.Session!.Actors.Values).Session!;
        original = original with { Session = checkpoint };
        NativeProjectSessionController controller = CreateController(original);
        controller.SelectActor("red-actor");
        controller.PreviewMovement(new GridPosition(3, 3));
        ProjectMovementPreview preview = controller.Preview!;

        ProjectSessionInteractionResult result = controller.AdvanceTurn();

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.RoundLimitReached, result.Status);
        Assert.Same(original, controller.CurrentProject);
        Assert.Equal(long.MaxValue, controller.CurrentProject.Session!.Turn.RoundNumber);
        Assert.Equal("red-actor", controller.SelectedActorId);
        Assert.Same(preview, controller.Preview);
    }

    [Fact]
    public void SessionUnavailable_AllRuntimeInteractionsReturnStructuredFailure()
    {
        NativeProjectSceneData original = CreateProject() with { Session = null };
        NativeProjectSessionController controller = CreateController(original);

        ProjectSessionInteractionResult[] results =
        [
            controller.SelectActor("blue-actor"),
            controller.PreviewMovement(new GridPosition(1, 0)),
            controller.ConfirmMovement(),
            controller.AdvanceTurn(),
        ];

        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(ProjectSessionInteractionStatus.SessionUnavailable, result.Status);
            Assert.Null(result.Plan);
        });
        Assert.True(controller.ClearSelection().IsSuccess);
        Assert.Null(controller.SelectedActorId);
        Assert.Null(controller.Preview);
        Assert.Same(original, controller.CurrentProject);
    }

    [Fact]
    public void Constructor_MismatchedScenarioAndCheckpoint_RejectsProgrammerError()
    {
        NativeProjectSceneData original = CreateProject();
        NativeProjectSceneData mismatched = original with
        {
            Scene = original.Scene with { Size = new GridSize(7, 5) },
        };

        ArgumentException error = Assert.Throws<ArgumentException>(() => CreateController(mismatched));

        Assert.Equal("project", error.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveSearchLimit_RejectsProgrammerError(int maximumVisitedPositions)
    {
        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateController(maximumVisitedPositions: maximumVisitedPositions));

        Assert.Equal("maximumVisitedPositions", error.ParamName);
    }

    [Fact]
    public void PreviewMovement_SearchLimit_ReportsBoundedCoreFailureWithoutCheckpointChange()
    {
        NativeProjectSessionController controller = CreateController(maximumVisitedPositions: 2);
        NativeProjectSceneData original = controller.CurrentProject;
        controller.SelectActor("blue-actor");

        ProjectSessionInteractionResult result = controller.PreviewMovement(new GridPosition(2, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(ProjectSessionInteractionStatus.MovementRejected, result.Status);
        Assert.Equal(nameof(MovementPlanStatus.SearchLimitExceeded), result.DetailCode);
        Assert.Equal(2, result.Plan!.VisitedPositions);
        Assert.Empty(result.Plan.Path);
        Assert.Null(controller.Preview);
        Assert.Same(original, controller.CurrentProject);
    }

    [Fact]
    public void RepeatActions_FromSameCheckpoint_ProducesIdenticalPlansAndRuntimeState()
    {
        NativeProjectSceneData original = CreateProject();
        NativeProjectSessionController first = CreateController(original);
        NativeProjectSessionController second = CreateController(original);

        foreach (string actorId in new[] { "blue-actor", "red-actor" })
        {
            var destination = actorId == "blue-actor" ? new GridPosition(1, 1) : new GridPosition(3, 2);
            Assert.Equal(first.SelectActor(actorId), second.SelectActor(actorId));
            ProjectSessionInteractionResult firstPreview = first.PreviewMovement(destination);
            ProjectSessionInteractionResult secondPreview = second.PreviewMovement(destination);
            Assert.Equal(firstPreview.Status, secondPreview.Status);
            Assert.Equal(firstPreview.Plan!.Path, secondPreview.Plan!.Path);
            Assert.Equal(firstPreview.Plan.RequiredSteps, secondPreview.Plan.RequiredSteps);
            Assert.Equal(firstPreview.Plan.VisitedPositions, secondPreview.Plan.VisitedPositions);
            Assert.True(first.ConfirmMovement().IsSuccess);
            Assert.True(second.ConfirmMovement().IsSuccess);
            Assert.Equal(first.AdvanceTurn(), second.AdvanceTurn());
        }

        GameSessionState firstFinal = first.CurrentProject.Session!;
        GameSessionState secondFinal = second.CurrentProject.Session!;
        Assert.Equal(firstFinal.Actors.Values, secondFinal.Actors.Values);
        Assert.Equal(firstFinal.Turn.ActiveParticipantId, secondFinal.Turn.ActiveParticipantId);
        Assert.Equal(firstFinal.Turn.RoundNumber, secondFinal.Turn.RoundNumber);
        Assert.Equal(2, firstFinal.Turn.RoundNumber);
        Assert.Equal(new GridPosition(1, 1), firstFinal.Actors["blue-actor"].Position);
        Assert.Equal(new GridPosition(3, 2), firstFinal.Actors["red-actor"].Position);
        Assert.Equal(new GridPosition(0, 0), original.Session!.Actors["blue-actor"].Position);
        Assert.Equal(new GridPosition(4, 3), original.Session.Actors["red-actor"].Position);
        Assert.Equal(1, original.Session.Turn.RoundNumber);
    }

    private static NativeProjectSessionController CreateController(
        NativeProjectSceneData? project = null,
        Func<GridPosition, bool>? canEnter = null,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions) =>
        new(
            project ?? CreateProject(),
            new GameSessionService(new MovementPlanner(new GridPathfinder())),
            OrthogonalGridTopology.Instance,
            canEnter ?? (_ => true),
            maximumVisitedPositions);

    private static NativeProjectSceneData CreateProject()
    {
        var size = new GridSize(6, 5);
        GameSessionState session = GameSessionState.Create(
            size,
            ["blue", "red"],
            [
                new GameActorDefinition("blue-actor", "blue", new GridPosition(0, 0), 4),
                new GameActorDefinition("red-actor", "red", new GridPosition(4, 3), 3),
            ]).Session!;
        var scene = new ScenarioSceneData(
            "synthetic-scenario",
            "Synthetic controller fixture",
            size,
            "synthetic:plain",
            [],
            []);
        return new NativeProjectSceneData("synthetic-project", scene, ["synthetic"], session);
    }
}
