using DisciplesRemaster.Content.Catalog;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Godot;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Game.Tests;

public sealed class NativeProjectSceneLoaderTests
{
    [Fact]
    public void Load_ValidatedProject_ProjectsSceneAndPreservesRuntimeCheckpoint()
    {
        NativeProject source = CreateProject(includeSession: true);
        var loader = new NativeProjectSceneLoader(
            new StubProjectLoader(new NativeProjectLoadResult(source, [])),
            new ScenarioValidationService());

        NativeProjectSceneLoadResult result = loader.Load("project.json");

        Assert.True(result.IsSuccess);
        Assert.Equal("synthetic-project", result.Project!.ProjectId);
        Assert.Equal("synthetic-scenario", result.Project.Scene.ScenarioId);
        Assert.Equal(["synthetic"], result.Project.ContentPackageIds);
        Assert.Same(source.Session, result.Project.Session);
    }

    [Fact]
    public void Load_ProjectFailure_DoesNotExposeSceneData()
    {
        var issue = new NativeProjectLoadIssue(
            NativeProjectLoadIssueCode.ManifestLoadFailed,
            "manifest",
            "$",
            "InvalidJson",
            "Invalid manifest.");
        var loader = new NativeProjectSceneLoader(
            new StubProjectLoader(new NativeProjectLoadResult(null, [issue])),
            new ScenarioValidationService());

        NativeProjectSceneLoadResult result = loader.Load("project.json");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Project);
        Assert.Equal(issue, Assert.Single(result.Issues));
    }

    internal static NativeProject CreateProject(bool includeSession)
    {
        var package = new ContentPackageDefinition(
            ContentPackageFormatV1.Version,
            "synthetic",
            "Synthetic",
            [new TerrainContentDefinition("plain", "Plain")],
            []);
        ContentCatalog catalog = ContentCatalog.Build(
            [package],
            new ContentPackageValidationService()).Catalog!;
        var scenario = new ScenarioDefinition(
            ScenarioFormatV1.Version,
            "synthetic-scenario",
            "Synthetic scenario",
            null,
            new ScenarioMapDefinition(8, 6, "synthetic:plain", []));
        GameSessionState? session = includeSession
            ? GameSessionState.Create(new GridSize(8, 6), ["blue"], []).Session
            : null;
        var manifest = new NativeProjectManifest(
            NativeProjectManifestFormatV1.Version,
            "synthetic-project",
            "scenarios/scenario.json",
            ["content/synthetic.json"],
            includeSession ? "sessions/session.json" : null);
        return new NativeProject(manifest, new ScenarioBundle(scenario, catalog, ["synthetic"]), session);
    }

    internal sealed class StubProjectLoader(NativeProjectLoadResult result) : INativeProjectLoader
    {
        public NativeProjectLoadResult Load(string manifestPath) => result;
    }
}
