using System.Globalization;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;

namespace DisciplesRemaster.Godot;

/// <summary>
/// An interactive in-memory host for the explicit project-owned open-grid policy.
/// TextReader/TextWriter injection permits complete synthetic transcript tests.
/// </summary>
public static class InteractiveProjectCommand
{
    public const int MaximumCommandLength = 1_024;
    public const int MaximumListedActors = 200;
    public const int MaximumPreviewPositions = 32;

    public static int Run(
        IReadOnlyList<string> arguments,
        NativeProjectSceneLoader loader,
        GameSessionService sessionService,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(sessionService);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (arguments.Count != 2 || arguments[0] != "play-open-grid" || string.IsNullOrWhiteSpace(arguments[1]))
        {
            error.WriteLine("Usage: play-open-grid <project-manifest>");
            return HeadlessGameCommand.UsageErrorExitCode;
        }

        try
        {
            NativeProjectSceneLoadResult load = loader.Load(arguments[1]);
            if (!load.IsSuccess || load.Project is null)
            {
                return HeadlessProjectCommand.WriteFailure(load, arguments[1], error);
            }

            if (load.Project.Session is null)
            {
                error.WriteLine("Code: SessionUnavailable");
                error.WriteLine("Interactive play requires a project with a runtime checkpoint.");
                return HeadlessGameCommand.ValidationErrorExitCode;
            }

            var controller = new NativeProjectSessionController(
                load.Project,
                sessionService,
                OrthogonalGridTopology.Instance,
                _ => true);
            var renderer = new NativeProjectTextRenderer();
            var viewport = new NativeProjectTextRenderOptions();
            output.WriteLine("Interactive native project: explicit open-grid movement.");
            output.WriteLine("Changes stay in memory. Input documents are read only; quit or EOF ends the session.");
            WriteHelp(output);
            output.Write(renderer.Render(controller.CurrentProject, viewport).Text);
            WriteStatus(controller, output);

            while (true)
            {
                output.Write("> ");
                output.Flush();
                string? line = ReadBoundedLine(input, out bool tooLong);
                if (line is null)
                {
                    output.WriteLine("Session ended.");
                    return HeadlessGameCommand.SuccessExitCode;
                }

                if (tooLong)
                {
                    controller.CancelMovementPreview();
                    error.WriteLine("Code: CommandTooLong");
                    error.WriteLine("The previous movement preview was cancelled.");
                    continue;
                }

                string command = line.Trim();
                if (command.Length == 0)
                {
                    continue;
                }

                if (command == "quit")
                {
                    output.WriteLine("Session ended.");
                    return HeadlessGameCommand.SuccessExitCode;
                }

                if (command.StartsWith("select ", StringComparison.Ordinal))
                {
                    WriteInteraction(controller.SelectActor(command[7..].Trim()), output);
                    WriteStatus(controller, output);
                    continue;
                }

                string[] tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                switch (tokens[0])
                {
                    case "help" when tokens.Length == 1:
                        WriteHelp(output);
                        break;
                    case "status" when tokens.Length == 1:
                        WriteStatus(controller, output);
                        break;
                    case "actors" when tokens.Length == 1:
                        WriteActors(controller, output);
                        break;
                    case "clear" when tokens.Length == 1:
                        WriteInteraction(controller.ClearSelection(), output);
                        break;
                    case "preview" when tokens.Length == 3 &&
                        TryInteger(tokens[1], out int x) && TryInteger(tokens[2], out int y):
                        WriteInteraction(controller.PreviewMovement(new GridPosition(x, y)), output);
                        break;
                    case "preview":
                        controller.CancelMovementPreview();
                        error.WriteLine("Code: InvalidInteractiveCommand");
                        error.WriteLine("Usage: preview <x> <y>. The previous preview was cancelled.");
                        break;
                    case "confirm" when tokens.Length == 1:
                        WriteInteraction(controller.ConfirmMovement(), output);
                        WriteStatus(controller, output);
                        break;
                    case "end-turn" when tokens.Length == 1:
                        WriteInteraction(controller.AdvanceTurn(), output);
                        WriteStatus(controller, output);
                        break;
                    case "show" when tokens.Length == 1:
                        output.Write(renderer.Render(controller.CurrentProject, viewport).Text);
                        break;
                    case "show" when tokens.Length == 5 &&
                        TryInteger(tokens[1], out int originX) && TryInteger(tokens[2], out int originY) &&
                        TryInteger(tokens[3], out int width) && TryInteger(tokens[4], out int height):
                        var requestedViewport = new NativeProjectTextRenderOptions(originX, originY, width, height);
                        NativeProjectTextRenderResult rendered = renderer.Render(controller.CurrentProject, requestedViewport);
                        if (rendered.IsSuccess)
                        {
                            viewport = requestedViewport;
                            output.Write(rendered.Text);
                        }
                        else
                        {
                            foreach (NativeProjectTextRenderIssue issue in rendered.Issues)
                            {
                                error.WriteLine($"Code: {issue.Code}");
                            }
                        }

                        break;
                    default:
                        error.WriteLine("Code: InvalidInteractiveCommand");
                        WriteHelp(error);
                        break;
                }
            }
        }
        catch (Exception)
        {
            error.WriteLine("Interactive host failed unexpectedly.");
            error.WriteLine("Code: UnexpectedError");
            return HeadlessGameCommand.SoftwareErrorExitCode;
        }
    }

    private static void WriteHelp(TextWriter writer)
    {
        writer.WriteLine("Commands: actors | select <actor-id> | preview <x> <y> | confirm | clear | end-turn");
        writer.WriteLine("          show [x y width height] | status | help | quit");
    }

    private static void WriteStatus(NativeProjectSessionController controller, TextWriter writer)
    {
        GameSessionState session = controller.CurrentProject.Session!;
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Round: {session.Turn.RoundNumber}; active participant: {SafeText(session.Turn.ActiveParticipantId)}"));
        if (controller.SelectedActorId is null)
        {
            writer.WriteLine("Selected actor: none");
        }
        else
        {
            WriteActor(session.Actors[controller.SelectedActorId], writer);
        }
    }

    private static void WriteActors(NativeProjectSessionController controller, TextWriter writer)
    {
        GameSessionState session = controller.CurrentProject.Session!;
        foreach (GameActorState actor in session.Actors.Values
            .OrderBy(actor => actor.Id, StringComparer.Ordinal).Take(MaximumListedActors))
        {
            WriteActor(actor, writer);
        }

        if (session.Actors.Count > MaximumListedActors)
        {
            writer.WriteLine("Actor list truncated. Any known actor ID can still be selected.");
        }
    }

    private static void WriteActor(GameActorState actor, TextWriter writer) =>
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Actor: {SafeText(actor.Id)}; owner: {SafeText(actor.OwnerParticipantId)}; position: ({actor.Position.X}, {actor.Position.Y}); movement: {actor.RemainingMovement}/{actor.MovementAllowance}"));

    private static void WriteInteraction(ProjectSessionInteractionResult result, TextWriter writer)
    {
        writer.WriteLine($"Code: {result.Status}");
        writer.WriteLine(result.Message);
        if (result.DetailCode is not null)
        {
            writer.WriteLine($"Detail: {result.DetailCode}");
        }

        if (result.Plan is not null)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Steps: {result.Plan.RequiredSteps}; budget: {result.Plan.MovementBudget}"));
            if (result.Plan.Path.Count > 0)
            {
                writer.WriteLine("Route: " + string.Join(" -> ", result.Plan.Path.Take(MaximumPreviewPositions)
                    .Select(position => string.Create(CultureInfo.InvariantCulture, $"({position.X}, {position.Y})"))));
                if (result.Plan.Path.Count > MaximumPreviewPositions)
                {
                    writer.WriteLine("Route display truncated.");
                }
            }
        }
    }

    private static string? ReadBoundedLine(TextReader reader, out bool tooLong)
    {
        var builder = new System.Text.StringBuilder();
        tooLong = false;
        int value;
        bool readAny = false;
        while ((value = reader.Read()) != -1)
        {
            readAny = true;
            if (value == '\n')
            {
                break;
            }

            if (value == '\r')
            {
                if (reader.Peek() == '\n')
                {
                    reader.Read();
                }

                break;
            }

            if (builder.Length < MaximumCommandLength)
            {
                builder.Append((char)value);
            }
            else
            {
                tooLong = true;
            }
        }

        return readAny ? builder.ToString() : null;
    }

    private static bool TryInteger(string value, out int number) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

    private static string SafeText(string value) =>
        string.Concat(value.Select(character => char.IsControl(character) ? '?' : character));
}
