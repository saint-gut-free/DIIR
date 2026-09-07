using System.Globalization;
using System.Text;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Godot;

public static class NativeProjectTextRenderRules
{
    public const int DefaultViewportWidth = 40;
    public const int DefaultViewportHeight = 20;
    public const int MaximumViewportWidth = 120;
    public const int MaximumViewportHeight = 60;
    public const int MaximumVisibleDetails = 200;
}

public sealed record NativeProjectTextRenderOptions(
    int OriginX = 0,
    int OriginY = 0,
    int ViewportWidth = NativeProjectTextRenderRules.DefaultViewportWidth,
    int ViewportHeight = NativeProjectTextRenderRules.DefaultViewportHeight);

public enum NativeProjectTextRenderIssueCode
{
    InvalidOrigin,
    InvalidViewportWidth,
    InvalidViewportHeight,
    OriginOutsideMap,
}

public sealed record NativeProjectTextRenderIssue(
    NativeProjectTextRenderIssueCode Code,
    string PropertyPath,
    string Message);

public sealed record NativeProjectTextRenderResult(
    string? Text,
    int RenderedWidth,
    int RenderedHeight,
    bool DetailsTruncated,
    IReadOnlyList<NativeProjectTextRenderIssue> Issues)
{
    public bool IsSuccess => Text is not null && Issues.Count == 0;
}

/// <summary>
/// Produces a bounded diagnostic viewport from validated project-owned scene data.
/// Symbols describe adapter layers only and do not imply original-game rendering.
/// </summary>
public sealed class NativeProjectTextRenderer
{
    public NativeProjectTextRenderResult Render(
        NativeProjectSceneData project,
        NativeProjectTextRenderOptions options)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);

        IReadOnlyList<NativeProjectTextRenderIssue> issues = Validate(project, options);
        if (issues.Count > 0)
        {
            return new NativeProjectTextRenderResult(null, 0, 0, false, issues);
        }

        int width = Math.Min(options.ViewportWidth, project.Scene.Size.Width - options.OriginX);
        int height = Math.Min(options.ViewportHeight, project.Scene.Size.Height - options.OriginY);
        Dictionary<GridPosition, int> terrainCounts = CountPositions(
            project.Scene.TerrainOverrides.Select(item => item.Position));
        Dictionary<GridPosition, int> objectCounts = CountPositions(
            project.Scene.Objects.Select(item => item.Position));
        Dictionary<GridPosition, int> actorCounts = CountPositions(
            project.Session?.Actors.Values.Select(item => item.Position) ?? []);

        var builder = new StringBuilder();
        builder.AppendLine("Native project diagnostic viewport");
        builder.Append("Project: ").AppendLine(Sanitize(project.ProjectId));
        builder.Append("Scenario: ").AppendLine(Sanitize(project.Scene.ScenarioId));
        builder.Append("Title: ").AppendLine(Sanitize(project.Scene.Title));
        builder.Append("Map: ")
            .Append(project.Scene.Size.Width.ToString(CultureInfo.InvariantCulture))
            .Append(" x ")
            .AppendLine(project.Scene.Size.Height.ToString(CultureInfo.InvariantCulture));
        builder.Append("Origin: ")
            .Append(options.OriginX.ToString(CultureInfo.InvariantCulture))
            .Append(',')
            .AppendLine(options.OriginY.ToString(CultureInfo.InvariantCulture));
        builder.Append("Viewport: ")
            .Append(width.ToString(CultureInfo.InvariantCulture))
            .Append(" x ")
            .AppendLine(height.ToString(CultureInfo.InvariantCulture));
        builder.AppendLine("Legend: . default terrain; T terrain override; O inert object; A runtime actor; * layered elements");
        AppendGrid(builder, options, width, height, terrainCounts, objectCounts, actorCounts);

        int detailsWritten = 0;
        bool detailsTruncated = false;
        AppendDetails(
            builder,
            "Visible terrain overrides",
            project.Scene.TerrainOverrides
                .Where(item => IsVisible(item.Position, options, width, height))
                .OrderBy(item => item.Position.Y)
                .ThenBy(item => item.Position.X)
                .ThenBy(item => item.ContentReference, StringComparer.Ordinal)
                .Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"({item.Position.X}, {item.Position.Y}) {item.ContentReference}")),
            ref detailsWritten,
            ref detailsTruncated);
        AppendDetails(
            builder,
            "Visible inert objects",
            project.Scene.Objects
                .Where(item => IsVisible(item.Position, options, width, height))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.Id} at ({item.Position.X}, {item.Position.Y}) [{item.ArchetypeReference}]")),
            ref detailsWritten,
            ref detailsTruncated);
        AppendDetails(
            builder,
            "Visible runtime actors",
            (project.Session?.Actors.Values ?? [])
                .Where(item => IsVisible(item.Position, options, width, height))
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.Id} at ({item.Position.X}, {item.Position.Y}) [owner {item.OwnerParticipantId}]")),
            ref detailsWritten,
            ref detailsTruncated);

        if (detailsTruncated)
        {
            builder.Append("Visible detail list truncated at ")
                .Append(NativeProjectTextRenderRules.MaximumVisibleDetails.ToString(CultureInfo.InvariantCulture))
                .AppendLine(" entries.");
        }

        builder.AppendLine("This is a project-owned diagnostic view, not a reconstruction of original-game graphics or mechanics.");
        string text = builder.ToString().Replace(Environment.NewLine, "\n", StringComparison.Ordinal);
        return new NativeProjectTextRenderResult(text, width, height, detailsTruncated, []);
    }

    private static IReadOnlyList<NativeProjectTextRenderIssue> Validate(
        NativeProjectSceneData project,
        NativeProjectTextRenderOptions options)
    {
        List<NativeProjectTextRenderIssue> issues = [];
        if (options.OriginX < 0 || options.OriginY < 0)
        {
            issues.Add(Issue(
                NativeProjectTextRenderIssueCode.InvalidOrigin,
                "origin",
                "Viewport origin cannot be negative."));
        }
        else if (options.OriginX >= project.Scene.Size.Width || options.OriginY >= project.Scene.Size.Height)
        {
            issues.Add(Issue(
                NativeProjectTextRenderIssueCode.OriginOutsideMap,
                "origin",
                "Viewport origin must be inside the map."));
        }

        if (options.ViewportWidth < 1 ||
            options.ViewportWidth > NativeProjectTextRenderRules.MaximumViewportWidth)
        {
            issues.Add(Issue(
                NativeProjectTextRenderIssueCode.InvalidViewportWidth,
                "viewportWidth",
                $"Viewport width must be between 1 and {NativeProjectTextRenderRules.MaximumViewportWidth}."));
        }

        if (options.ViewportHeight < 1 ||
            options.ViewportHeight > NativeProjectTextRenderRules.MaximumViewportHeight)
        {
            issues.Add(Issue(
                NativeProjectTextRenderIssueCode.InvalidViewportHeight,
                "viewportHeight",
                $"Viewport height must be between 1 and {NativeProjectTextRenderRules.MaximumViewportHeight}."));
        }

        return issues
            .OrderBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ToArray();
    }

    private static void AppendGrid(
        StringBuilder builder,
        NativeProjectTextRenderOptions options,
        int width,
        int height,
        IReadOnlyDictionary<GridPosition, int> terrainCounts,
        IReadOnlyDictionary<GridPosition, int> objectCounts,
        IReadOnlyDictionary<GridPosition, int> actorCounts)
    {
        int lastY = options.OriginY + height - 1;
        int rowLabelWidth = Math.Max(1, lastY.ToString(CultureInfo.InvariantCulture).Length);
        builder.Append(' ', rowLabelWidth + 1);
        for (int offsetX = 0; offsetX < width; offsetX++)
        {
            builder.Append((options.OriginX + offsetX) % 10);
        }

        builder.AppendLine();
        for (int offsetY = 0; offsetY < height; offsetY++)
        {
            int y = options.OriginY + offsetY;
            builder.Append(y.ToString(CultureInfo.InvariantCulture).PadLeft(rowLabelWidth)).Append(' ');
            for (int offsetX = 0; offsetX < width; offsetX++)
            {
                var position = new GridPosition(options.OriginX + offsetX, y);
                builder.Append(SymbolAt(position, terrainCounts, objectCounts, actorCounts));
            }

            builder.AppendLine();
        }
    }

    private static char SymbolAt(
        GridPosition position,
        IReadOnlyDictionary<GridPosition, int> terrainCounts,
        IReadOnlyDictionary<GridPosition, int> objectCounts,
        IReadOnlyDictionary<GridPosition, int> actorCounts)
    {
        terrainCounts.TryGetValue(position, out int terrains);
        objectCounts.TryGetValue(position, out int objects);
        actorCounts.TryGetValue(position, out int actors);
        int layers = terrains + objects + actors;
        if (layers > 1)
        {
            return '*';
        }

        if (actors == 1)
        {
            return 'A';
        }

        if (objects == 1)
        {
            return 'O';
        }

        return terrains == 1 ? 'T' : '.';
    }

    private static Dictionary<GridPosition, int> CountPositions(IEnumerable<GridPosition> positions) =>
        positions
            .GroupBy(position => position)
            .ToDictionary(group => group.Key, group => group.Count());

    private static bool IsVisible(
        GridPosition position,
        NativeProjectTextRenderOptions options,
        int width,
        int height) =>
        position.X >= options.OriginX &&
        position.X < options.OriginX + width &&
        position.Y >= options.OriginY &&
        position.Y < options.OriginY + height;

    private static void AppendDetails(
        StringBuilder builder,
        string heading,
        IEnumerable<string> details,
        ref int detailsWritten,
        ref bool truncated)
    {
        builder.AppendLine(heading + ":");
        bool wroteAny = false;
        foreach (string detail in details)
        {
            if (detailsWritten == NativeProjectTextRenderRules.MaximumVisibleDetails)
            {
                truncated = true;
                break;
            }

            builder.Append("- ").AppendLine(Sanitize(detail));
            detailsWritten++;
            wroteAny = true;
        }

        if (!wroteAny && !truncated)
        {
            builder.AppendLine("- none");
        }
    }

    private static NativeProjectTextRenderIssue Issue(
        NativeProjectTextRenderIssueCode code,
        string propertyPath,
        string message) =>
        new(code, propertyPath, message);

    private static string Sanitize(string value) =>
        string.Concat(value.Select(character => char.IsControl(character) ? '?' : character));
}
