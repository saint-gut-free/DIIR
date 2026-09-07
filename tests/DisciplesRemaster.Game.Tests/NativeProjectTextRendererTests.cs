using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;

namespace DisciplesRemaster.Game.Tests;

public sealed class NativeProjectTextRendererTests
{
    [Fact]
    public void Render_ValidatedLayers_ProducesDeterministicBoundedViewport()
    {
        NativeProjectSceneData project = CreateProject();
        var renderer = new NativeProjectTextRenderer();

        NativeProjectTextRenderResult first = renderer.Render(
            project,
            new NativeProjectTextRenderOptions(ViewportWidth: 5, ViewportHeight: 4));
        NativeProjectTextRenderResult second = renderer.Render(
            project,
            new NativeProjectTextRenderOptions(ViewportWidth: 5, ViewportHeight: 4));

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Text, second.Text);
        Assert.Equal(5, first.RenderedWidth);
        Assert.Equal(4, first.RenderedHeight);
        Assert.False(first.DetailsTruncated);
        Assert.Contains("  01234\n0 .....\n1 .T*..\n2 ...*.\n3 ....A\n", first.Text, StringComparison.Ordinal);
        Assert.Contains("synthetic:forest", first.Text, StringComparison.Ordinal);
        Assert.Contains("landmark at (2, 1)", first.Text, StringComparison.Ordinal);
        Assert.Contains("blue-actor at (3, 2)", first.Text, StringComparison.Ordinal);
        Assert.Contains("not a reconstruction", first.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', first.Text!);
    }

    [Fact]
    public void Render_ViewportBeyondMap_ClampsToRemainingCells()
    {
        NativeProjectTextRenderResult result = new NativeProjectTextRenderer().Render(
            CreateProject(),
            new NativeProjectTextRenderOptions(OriginX: 3, OriginY: 2, ViewportWidth: 10, ViewportHeight: 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.RenderedWidth);
        Assert.Equal(2, result.RenderedHeight);
        Assert.Contains("Origin: 3,2", result.Text, StringComparison.Ordinal);
        Assert.Contains("Viewport: 2 x 2", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic:forest", result.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1, 0, 1, 1, NativeProjectTextRenderIssueCode.InvalidOrigin)]
    [InlineData(5, 0, 1, 1, NativeProjectTextRenderIssueCode.OriginOutsideMap)]
    [InlineData(0, 0, 0, 1, NativeProjectTextRenderIssueCode.InvalidViewportWidth)]
    [InlineData(0, 0, 1, 0, NativeProjectTextRenderIssueCode.InvalidViewportHeight)]
    [InlineData(0, 0, NativeProjectTextRenderRules.MaximumViewportWidth + 1, 1, NativeProjectTextRenderIssueCode.InvalidViewportWidth)]
    [InlineData(0, 0, 1, NativeProjectTextRenderRules.MaximumViewportHeight + 1, NativeProjectTextRenderIssueCode.InvalidViewportHeight)]
    public void Render_InvalidViewport_ReturnsStructuredIssue(
        int x,
        int y,
        int width,
        int height,
        NativeProjectTextRenderIssueCode expectedCode)
    {
        NativeProjectTextRenderResult result = new NativeProjectTextRenderer().Render(
            CreateProject(),
            new NativeProjectTextRenderOptions(x, y, width, height));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Text);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void Render_ManyVisibleItems_TruncatesDetailsButNotGrid()
    {
        NativeProjectSceneData source = CreateProject();
        ScenarioSceneObject[] objects = Enumerable.Range(0, NativeProjectTextRenderRules.MaximumVisibleDetails + 1)
            .Select(index => new ScenarioSceneObject(
                $"object-{index:000}",
                "synthetic:marker",
                new GridPosition(0, 0)))
            .ToArray();
        NativeProjectSceneData project = source with
        {
            Scene = source.Scene with { Objects = objects },
            Session = null,
        };

        NativeProjectTextRenderResult result = new NativeProjectTextRenderer().Render(
            project,
            new NativeProjectTextRenderOptions(ViewportWidth: 1, ViewportHeight: 1));

        Assert.True(result.IsSuccess);
        Assert.True(result.DetailsTruncated);
        Assert.Contains("0 *", result.Text, StringComparison.Ordinal);
        Assert.Contains("object-199", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("object-200", result.Text, StringComparison.Ordinal);
        Assert.Contains("truncated at 200 entries", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ControlCharactersInMetadata_AreNeutralized()
    {
        NativeProjectSceneData source = CreateProject();
        NativeProjectSceneData project = source with
        {
            Scene = source.Scene with { Title = "Line one\nLine two\u001b" },
        };

        NativeProjectTextRenderResult result = new NativeProjectTextRenderer().Render(
            project,
            new NativeProjectTextRenderOptions(ViewportWidth: 1, ViewportHeight: 1));

        Assert.True(result.IsSuccess);
        Assert.Contains("Title: Line one?Line two?\n", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', result.Text!);
    }

    private static NativeProjectSceneData CreateProject()
    {
        var size = new GridSize(5, 4);
        GameSessionState session = GameSessionState.Create(
            size,
            ["blue"],
            [
                new GameActorDefinition("blue-actor", "blue", new GridPosition(3, 2), 4),
                new GameActorDefinition("edge-actor", "blue", new GridPosition(4, 3), 4),
            ]).Session!;
        var scene = new ScenarioSceneData(
            "synthetic-scenario",
            "Synthetic scenario",
            size,
            "synthetic:plain",
            [
                new ScenarioSceneTerrain(new GridPosition(1, 1), "synthetic:forest"),
                new ScenarioSceneTerrain(new GridPosition(2, 1), "synthetic:water"),
            ],
            [
                new ScenarioSceneObject("landmark", "synthetic:landmark", new GridPosition(2, 1)),
                new ScenarioSceneObject("overlapped", "synthetic:landmark", new GridPosition(3, 2)),
            ]);
        return new NativeProjectSceneData("synthetic-project", scene, ["synthetic"], session);
    }
}
