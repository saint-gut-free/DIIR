using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Persistence.Projects;

namespace DisciplesRemaster.Godot;

/// <summary>
/// Loads a complete native scenario bundle and exposes scene data only when all
/// project-owned content references resolve.
/// </summary>
public sealed class ValidatedScenarioSceneLoader
{
    private readonly IScenarioBundleLoader bundleLoader;
    private readonly IScenarioValidationService scenarioValidation;

    public ValidatedScenarioSceneLoader(
        IScenarioBundleLoader bundleLoader,
        IScenarioValidationService scenarioValidation)
    {
        this.bundleLoader = bundleLoader ?? throw new ArgumentNullException(nameof(bundleLoader));
        this.scenarioValidation = scenarioValidation ?? throw new ArgumentNullException(nameof(scenarioValidation));
    }

    public ValidatedScenarioSceneLoadResult Load(
        string scenarioPath,
        IReadOnlyList<string> contentPackagePaths)
    {
        ScenarioBundleLoadResult load = bundleLoader.Load(scenarioPath, contentPackagePaths);
        if (!load.IsSuccess || load.Bundle is null)
        {
            return new ValidatedScenarioSceneLoadResult(false, null, [], load.Issues);
        }

        ScenarioRuntimeView runtime = ScenarioRuntimeView.Create(load.Bundle.Scenario, scenarioValidation);
        ScenarioSceneData scene = ScenarioSceneProjection.Project(runtime);
        return new ValidatedScenarioSceneLoadResult(
            true,
            scene,
            load.Bundle.ContentPackageIds,
            []);
    }
}
