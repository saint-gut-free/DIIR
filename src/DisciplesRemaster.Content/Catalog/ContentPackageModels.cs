namespace DisciplesRemaster.Content.Catalog;

/// <summary>
/// A project-owned package of content declarations. It contains metadata only;
/// binary assets and runtime behavior are separate concerns.
/// </summary>
public sealed record ContentPackageDefinition(
    int FormatVersion,
    string Id,
    string DisplayName,
    IReadOnlyList<TerrainContentDefinition> Terrains,
    IReadOnlyList<ObjectArchetypeDefinition> ObjectArchetypes);

public sealed record TerrainContentDefinition(string Id, string DisplayName);

public sealed record ObjectArchetypeDefinition(string Id, string DisplayName);

public static class ContentPackageFormatV1
{
    public const int Version = 1;
    public const int MaximumPackageIdLength = 64;
    public const int MaximumLocalIdLength = 96;
    public const int MaximumDisplayNameLength = 160;
    public const int MaximumEntriesPerKind = 100_000;
}
