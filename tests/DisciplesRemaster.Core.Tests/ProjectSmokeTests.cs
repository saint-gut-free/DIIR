namespace DisciplesRemaster.Core.Tests;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void TestAssemblyLoads() => Assert.NotNull(typeof(ProjectSmokeTests).Assembly);
}

