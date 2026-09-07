using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Sessions;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Godot;

public sealed record NativeProjectSceneData(
    string ProjectId,
    ScenarioSceneData Scene,
    IReadOnlyList<string> ContentPackageIds,
    GameSessionState? Session);

public sealed record NativeProjectSceneLoadResult(
    NativeProjectSceneData? Project,
    IReadOnlyList<NativeProjectLoadIssue> Issues)
{
    public bool IsSuccess => Project is not null && Issues.Count == 0;
}

/// <summary>
/// The composition boundary between portable project persistence and a future
/// Godot scene. No engine objects are created here.
/// </summary>
public sealed class NativeProjectSceneLoader
{
    private readonly INativeProjectLoader projectLoader;
    private readonly IScenarioValidationService scenarioValidation;

    public NativeProjectSceneLoader(
        INativeProjectLoader projectLoader,
        IScenarioValidationService scenarioValidation)
    {
        this.projectLoader = projectLoader ?? throw new ArgumentNullException(nameof(projectLoader));
        this.scenarioValidation = scenarioValidation ?? throw new ArgumentNullException(nameof(scenarioValidation));
    }

    public NativeProjectSceneLoadResult Load(string manifestPath)
    {
        NativeProjectLoadResult load = projectLoader.Load(manifestPath);
        if (!load.IsSuccess || load.Project is null)
        {
            return new NativeProjectSceneLoadResult(null, load.Issues);
        }

        ScenarioRuntimeView runtime = ScenarioRuntimeView.Create(
            load.Project.ScenarioBundle.Scenario,
            scenarioValidation);
        var project = new NativeProjectSceneData(
            load.Project.Manifest.Id,
            ScenarioSceneProjection.Project(runtime),
            load.Project.ScenarioBundle.ContentPackageIds.ToArray(),
            load.Project.Session);
        return new NativeProjectSceneLoadResult(project, []);
    }
}
