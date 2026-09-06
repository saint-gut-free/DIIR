using System.Collections.ObjectModel;

namespace DisciplesRemaster.Content.Catalog;

public enum ContentCatalogBuildErrorCode
{
    InvalidPackage,
    DuplicatePackageId,
}

public sealed record ContentCatalogBuildIssue(
    ContentCatalogBuildErrorCode Code,
    string PackageId,
    string PropertyPath,
    string Message);

public sealed record ContentCatalogBuildResult(
    ContentCatalog? Catalog,
    IReadOnlyList<ContentCatalogBuildIssue> Issues)
{
    public bool IsSuccess => Catalog is not null && Issues.Count == 0;
}

public sealed class ContentCatalog
{
    private readonly IReadOnlyDictionary<ContentReference, TerrainContentDefinition> terrains;
    private readonly IReadOnlyDictionary<ContentReference, ObjectArchetypeDefinition> objectArchetypes;

    private ContentCatalog(
        IReadOnlyDictionary<ContentReference, TerrainContentDefinition> terrains,
        IReadOnlyDictionary<ContentReference, ObjectArchetypeDefinition> objectArchetypes)
    {
        this.terrains = terrains;
        this.objectArchetypes = objectArchetypes;
    }

    public IReadOnlyDictionary<ContentReference, TerrainContentDefinition> Terrains => terrains;

    public IReadOnlyDictionary<ContentReference, ObjectArchetypeDefinition> ObjectArchetypes => objectArchetypes;

    public bool ContainsTerrain(string reference) =>
        ContentReference.TryParse(reference, out ContentReference key) && terrains.ContainsKey(key);

    public bool ContainsObjectArchetype(string reference) =>
        ContentReference.TryParse(reference, out ContentReference key) && objectArchetypes.ContainsKey(key);

    public static ContentCatalogBuildResult Build(
        IEnumerable<ContentPackageDefinition?> packages,
        IContentPackageValidationService validationService)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(validationService);

        ContentPackageDefinition?[] packageArray = packages.ToArray();
        List<ContentCatalogBuildIssue> issues = [];
        HashSet<string> packageIds = new(StringComparer.Ordinal);
        foreach (ContentPackageDefinition? package in packageArray)
        {
            ContentPackageValidationResult validation = validationService.Validate(package);
            string safePackageId = package?.Id ?? "<missing>";
            issues.AddRange(validation.Issues.Select(issue => new ContentCatalogBuildIssue(
                ContentCatalogBuildErrorCode.InvalidPackage,
                safePackageId,
                issue.PropertyPath,
                $"{issue.Code}: {issue.Message}")));

            if (package is not null && !packageIds.Add(package.Id))
            {
                issues.Add(new ContentCatalogBuildIssue(
                    ContentCatalogBuildErrorCode.DuplicatePackageId,
                    safePackageId,
                    "id",
                    "Package IDs must be unique."));
            }
        }

        ContentCatalogBuildIssue[] sortedIssues = issues
            .OrderBy(issue => issue.PackageId, StringComparer.Ordinal)
            .ThenBy(issue => issue.PropertyPath, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code)
            .ToArray();
        if (sortedIssues.Length > 0)
        {
            return new ContentCatalogBuildResult(null, sortedIssues);
        }

        var terrains = new Dictionary<ContentReference, TerrainContentDefinition>();
        var objects = new Dictionary<ContentReference, ObjectArchetypeDefinition>();
        foreach (ContentPackageDefinition package in packageArray.Cast<ContentPackageDefinition>())
        {
            foreach (TerrainContentDefinition terrain in package.Terrains)
            {
                terrains.Add(ContentReference.Parse($"{package.Id}:{terrain.Id}"), terrain);
            }

            foreach (ObjectArchetypeDefinition archetype in package.ObjectArchetypes)
            {
                objects.Add(ContentReference.Parse($"{package.Id}:{archetype.Id}"), archetype);
            }
        }

        var catalog = new ContentCatalog(
            new ReadOnlyDictionary<ContentReference, TerrainContentDefinition>(terrains),
            new ReadOnlyDictionary<ContentReference, ObjectArchetypeDefinition>(objects));
        return new ContentCatalogBuildResult(catalog, []);
    }
}
