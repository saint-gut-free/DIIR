using System.Reflection;
using DisciplesRemaster.BinaryDiff;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;
using DisciplesRemaster.Editor;
using DisciplesRemaster.FileInventory;
using DisciplesRemaster.Godot;
using DisciplesRemaster.MapInspector;
using DisciplesRemaster.OriginalGame;
using DisciplesRemaster.Persistence.Scenarios;

namespace DisciplesRemaster.Architecture.Tests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void ProductionAssemblies_ReferenceOnlyAllowedProjectLayers()
    {
        ProjectDependencyRule[] rules =
        [
            Rule(typeof(GridSize).Assembly),
            Rule(typeof(ScenarioDefinition).Assembly, "DisciplesRemaster.Core"),
            Rule(
                typeof(ScenarioFileStore).Assembly,
                "DisciplesRemaster.Core",
                "DisciplesRemaster.Content"),
            Rule(
                typeof(OriginalGameLocation).Assembly,
                "DisciplesRemaster.Core",
                "DisciplesRemaster.Content"),
            Rule(
                typeof(ScenarioSceneLoader).Assembly,
                "DisciplesRemaster.Core",
                "DisciplesRemaster.Content",
                "DisciplesRemaster.Persistence"),
            Rule(
                typeof(ScenarioEditorCommand).Assembly,
                "DisciplesRemaster.Core",
                "DisciplesRemaster.Content",
                "DisciplesRemaster.Persistence"),
            Rule(typeof(BinaryDiffCommand).Assembly, "DisciplesRemaster.OriginalGame"),
            Rule(typeof(FileInventoryCommand).Assembly, "DisciplesRemaster.OriginalGame"),
            Rule(typeof(MapExperimentValidationCommand).Assembly, "DisciplesRemaster.OriginalGame"),
        ];

        foreach (ProjectDependencyRule rule in rules)
        {
            string[] actual = rule.Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(name => name?.StartsWith("DisciplesRemaster.", StringComparison.Ordinal) == true)
                .Cast<string>()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            string[] unexpected = actual
                .Except(rule.AllowedReferences, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                unexpected.Length == 0,
                $"{rule.Assembly.GetName().Name} has forbidden project references: {string.Join(", ", unexpected)}");
        }
    }

    [Fact]
    public void ProductionAssemblies_DoNotReferenceTestAssemblies()
    {
        Assembly[] assemblies =
        [
            typeof(GridSize).Assembly,
            typeof(ScenarioDefinition).Assembly,
            typeof(ScenarioFileStore).Assembly,
            typeof(OriginalGameLocation).Assembly,
            typeof(ScenarioSceneLoader).Assembly,
            typeof(ScenarioEditorCommand).Assembly,
            typeof(BinaryDiffCommand).Assembly,
            typeof(FileInventoryCommand).Assembly,
            typeof(MapExperimentValidationCommand).Assembly,
        ];

        foreach (Assembly assembly in assemblies)
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name?.EndsWith(".Tests", StringComparison.Ordinal) == true);
        }
    }

    private static ProjectDependencyRule Rule(Assembly assembly, params string[] allowedReferences) =>
        new(assembly, allowedReferences.ToHashSet(StringComparer.Ordinal));

    private sealed record ProjectDependencyRule(
        Assembly Assembly,
        IReadOnlySet<string> AllowedReferences);
}
