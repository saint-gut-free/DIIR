using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Movement;
using DisciplesRemaster.Core.Navigation;
using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Godot;

public enum ProjectSessionInteractionStatus
{
    ActorSelected,
    SelectionCleared,
    MovementPreviewReady,
    MovementApplied,
    TurnAdvanced,
    SessionUnavailable,
    ActorNotFound,
    NoActorSelected,
    NoMovementPreview,
    MovementRejected,
    RoundLimitReached,
}

public sealed record ProjectSessionInteractionResult(
    ProjectSessionInteractionStatus Status,
    string Message,
    MovementPlan? Plan = null,
    string? DetailCode = null)
{
    public bool IsSuccess => Status is ProjectSessionInteractionStatus.ActorSelected or
        ProjectSessionInteractionStatus.SelectionCleared or
        ProjectSessionInteractionStatus.MovementPreviewReady or
        ProjectSessionInteractionStatus.MovementApplied or
        ProjectSessionInteractionStatus.TurnAdvanced;
}

public sealed record ProjectMovementPreview(string ActorId, GridPosition Destination, MovementPlan Plan);

/// <summary>
/// Single-threaded outer-host interaction state for a validated native project.
/// Runtime rules are delegated to Core; no files or engine objects are accessed.
/// </summary>
public sealed class NativeProjectSessionController
{
    private readonly GameSessionService sessionService;
    private readonly IGridTopology topology;
    private readonly Func<GridPosition, bool> canEnter;
    private readonly int maximumVisitedPositions;

    public NativeProjectSessionController(
        NativeProjectSceneData project,
        GameSessionService sessionService,
        IGridTopology topology,
        Func<GridPosition, bool> canEnter,
        int maximumVisitedPositions = GridPathfinder.DefaultMaximumVisitedPositions)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(canEnter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumVisitedPositions);
        if (project.Session is not null && project.Scene.Size != project.Session.MapSize)
        {
            throw new ArgumentException("The runtime checkpoint must match the scenario dimensions.", nameof(project));
        }

        CurrentProject = project;
        this.sessionService = sessionService;
        this.topology = topology;
        this.canEnter = canEnter;
        this.maximumVisitedPositions = maximumVisitedPositions;
    }

    public NativeProjectSceneData CurrentProject { get; private set; }

    public string? SelectedActorId { get; private set; }

    public ProjectMovementPreview? Preview { get; private set; }

    public ProjectSessionInteractionResult SelectActor(string actorId)
    {
        if (CurrentProject.Session is null)
        {
            return SessionUnavailable();
        }

        if (string.IsNullOrWhiteSpace(actorId) || !CurrentProject.Session.Actors.ContainsKey(actorId))
        {
            return new(ProjectSessionInteractionStatus.ActorNotFound, "Actor was not found.");
        }

        SelectedActorId = actorId;
        Preview = null;
        return new(ProjectSessionInteractionStatus.ActorSelected, "Actor selected.");
    }

    public ProjectSessionInteractionResult ClearSelection()
    {
        SelectedActorId = null;
        Preview = null;
        return new(ProjectSessionInteractionStatus.SelectionCleared, "Selection cleared.");
    }

    public void CancelMovementPreview() => Preview = null;

    public ProjectSessionInteractionResult PreviewMovement(GridPosition destination)
    {
        Preview = null;
        GameSessionState? session = CurrentProject.Session;
        if (session is null)
        {
            return SessionUnavailable();
        }

        if (SelectedActorId is null)
        {
            return new(ProjectSessionInteractionStatus.NoActorSelected, "Select an actor before planning movement.");
        }

        // Core returns a new immutable session. Discard it during preview so no
        // movement is committed until the caller explicitly confirms the action.
        GameSessionMovementResult movement = Move(session, SelectedActorId, destination);
        if (!movement.IsSuccess || movement.Plan is null)
        {
            return MovementRejected(movement);
        }

        Preview = new ProjectMovementPreview(SelectedActorId, destination, movement.Plan);
        return new(ProjectSessionInteractionStatus.MovementPreviewReady, "Movement preview is ready.", movement.Plan);
    }

    public ProjectSessionInteractionResult ConfirmMovement()
    {
        GameSessionState? session = CurrentProject.Session;
        if (session is null)
        {
            return SessionUnavailable();
        }

        ProjectMovementPreview? preview = Preview;
        if (preview is null)
        {
            return new(ProjectSessionInteractionStatus.NoMovementPreview, "Preview a movement before confirming it.");
        }

        // Re-evaluate the caller-supplied traversal policy at confirmation time.
        GameSessionMovementResult movement = Move(session, preview.ActorId, preview.Destination);
        Preview = null;
        if (!movement.IsSuccess || movement.Session is null)
        {
            return MovementRejected(movement);
        }

        CurrentProject = CurrentProject with { Session = movement.Session };
        return new(ProjectSessionInteractionStatus.MovementApplied, "Movement applied.", movement.Plan);
    }

    public ProjectSessionInteractionResult AdvanceTurn()
    {
        if (CurrentProject.Session is null)
        {
            return SessionUnavailable();
        }

        GameSessionState next;
        try
        {
            next = sessionService.AdvanceTurn(CurrentProject.Session);
        }
        catch (OverflowException)
        {
            return new(ProjectSessionInteractionStatus.RoundLimitReached, "The round counter cannot advance further.");
        }

        CurrentProject = CurrentProject with { Session = next };
        SelectedActorId = null;
        Preview = null;
        return new(ProjectSessionInteractionStatus.TurnAdvanced, "Turn advanced.");
    }

    private GameSessionMovementResult Move(GameSessionState session, string actorId, GridPosition destination) =>
        sessionService.MoveActor(session, actorId, destination, topology, canEnter, maximumVisitedPositions);

    private static ProjectSessionInteractionResult SessionUnavailable() =>
        new(ProjectSessionInteractionStatus.SessionUnavailable, "This project has no runtime checkpoint.");

    private static ProjectSessionInteractionResult MovementRejected(GameSessionMovementResult movement) =>
        new(
            ProjectSessionInteractionStatus.MovementRejected,
            movement.Message ?? "Movement was rejected.",
            movement.Plan,
            movement.Plan?.Status.ToString() ?? movement.Status.ToString());
}
