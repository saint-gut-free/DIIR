namespace DisciplesRemaster.Tools.Tests;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void TestAssemblyLoads() => Assert.NotNull(typeof(ProjectSmokeTests).Assembly);
}

