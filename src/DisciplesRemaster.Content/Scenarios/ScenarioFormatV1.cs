namespace DisciplesRemaster.Content.Scenarios;

/// <summary>
/// Defines limits of the project-owned scenario format. These limits do not
/// describe or imply constraints of any original game format.
/// </summary>
public static class ScenarioFormatV1
{
    public const int Version = 1;
    public const int MaximumDimension = 4096;
    public const long MaximumCellCount = 4_194_304;
    public const int MaximumIdLength = 64;
    public const int MaximumTitleLength = 160;
    public const int MaximumDescriptionLength = 8192;
    public const int MaximumContentReferenceLength = 128;
    public const int MaximumObjectCount = 100_000;
}
