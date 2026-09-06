using System.Text.Json;
using System.Text.Json.Serialization;
using DisciplesRemaster.Content.Catalog;

namespace DisciplesRemaster.Persistence.Content;

public sealed class ContentPackageJsonSerializer : IContentPackageSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private readonly IContentPackageValidationService validationService;

    public ContentPackageJsonSerializer(IContentPackageValidationService validationService)
    {
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public ContentPackageSerializationResult Serialize(ContentPackageDefinition? package)
    {
        ContentPackageValidationResult validation = validationService.Validate(package);
        if (!validation.IsValid || package is null)
        {
            return new ContentPackageSerializationResult(
                false,
                null,
                ContentPackagePersistenceErrorCode.ValidationFailed,
                validation.Issues,
                "Content package validation failed.");
        }

        var dto = new ContentPackageDto(
            package.FormatVersion,
            package.Id,
            package.DisplayName,
            package.Terrains
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .Select(entry => new ContentEntryDto(entry.Id, entry.DisplayName))
                .ToArray(),
            package.ObjectArchetypes
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .Select(entry => new ContentEntryDto(entry.Id, entry.DisplayName))
                .ToArray());
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        byte[] data = new byte[serialized.Length + 1];
        serialized.CopyTo(data, 0);
        data[^1] = (byte)'\n';

        return new ContentPackageSerializationResult(
            true,
            data,
            ContentPackagePersistenceErrorCode.None,
            validation.Issues,
            null);
    }

    public ContentPackageDeserializationResult Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            ContentPackageDto? dto = JsonSerializer.Deserialize<ContentPackageDto>(data, JsonOptions);
            if (dto is null ||
                dto.Terrains?.Any(entry => entry is null) == true ||
                dto.ObjectArchetypes?.Any(entry => entry is null) == true)
            {
                return InvalidJson("Content package JSON contains a missing document or entry.");
            }

            var package = new ContentPackageDefinition(
                dto.FormatVersion,
                dto.Id ?? string.Empty,
                dto.DisplayName ?? string.Empty,
                (dto.Terrains ?? [])
                    .Select(entry => new TerrainContentDefinition(entry!.Id ?? string.Empty, entry.DisplayName ?? string.Empty))
                    .ToArray(),
                (dto.ObjectArchetypes ?? [])
                    .Select(entry => new ObjectArchetypeDefinition(entry!.Id ?? string.Empty, entry.DisplayName ?? string.Empty))
                    .ToArray());
            ContentPackageValidationResult validation = validationService.Validate(package);
            if (!validation.IsValid)
            {
                return new ContentPackageDeserializationResult(
                    false,
                    package,
                    ContentPackagePersistenceErrorCode.ValidationFailed,
                    validation.Issues,
                    "Content package validation failed.");
            }

            return new ContentPackageDeserializationResult(
                true,
                package,
                ContentPackagePersistenceErrorCode.None,
                validation.Issues,
                null);
        }
        catch (JsonException)
        {
            return InvalidJson("Content package is not valid JSON for native format version 1.");
        }
        catch (NotSupportedException)
        {
            return InvalidJson("Content package contains unsupported JSON values.");
        }
    }

    private static ContentPackageDeserializationResult InvalidJson(string message) =>
        new(false, null, ContentPackagePersistenceErrorCode.InvalidJson, [], message);

    private sealed record ContentPackageDto(
        int FormatVersion,
        string? Id,
        string? DisplayName,
        IReadOnlyList<ContentEntryDto?>? Terrains,
        IReadOnlyList<ContentEntryDto?>? ObjectArchetypes);

    private sealed record ContentEntryDto(string? Id, string? DisplayName);
}
