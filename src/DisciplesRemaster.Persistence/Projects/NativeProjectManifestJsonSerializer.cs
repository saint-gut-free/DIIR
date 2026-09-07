using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisciplesRemaster.Persistence.Projects;

public sealed class NativeProjectManifestJsonSerializer : INativeProjectManifestSerializer
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

    private readonly INativeProjectManifestValidationService validationService;

    public NativeProjectManifestJsonSerializer(INativeProjectManifestValidationService validationService)
    {
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public NativeProjectManifestSerializationResult Serialize(NativeProjectManifest? manifest)
    {
        NativeProjectManifestValidationResult validation = validationService.Validate(manifest);
        if (!validation.IsValid || manifest is null)
        {
            return new NativeProjectManifestSerializationResult(
                false,
                null,
                NativeProjectPersistenceErrorCode.ValidationFailed,
                validation.Issues,
                "Native project manifest validation failed.");
        }

        var dto = new NativeProjectManifestDto(
            manifest.FormatVersion,
            manifest.Id,
            Normalize(manifest.Scenario),
            manifest.ContentPackages
                .Select(Normalize)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray(),
            manifest.Session is null ? null : Normalize(manifest.Session));
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        byte[] data = new byte[serialized.Length + 1];
        serialized.CopyTo(data, 0);
        data[^1] = (byte)'\n';
        return new NativeProjectManifestSerializationResult(
            true,
            data,
            NativeProjectPersistenceErrorCode.None,
            validation.Issues,
            null);
    }

    public NativeProjectManifestDeserializationResult Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            NativeProjectManifestDto? dto = JsonSerializer.Deserialize<NativeProjectManifestDto>(data, JsonOptions);
            if (dto is null || dto.ContentPackages?.Any(path => path is null) == true)
            {
                return InvalidJson("Native project manifest contains a missing document or path entry.");
            }

            var manifest = new NativeProjectManifest(
                dto.FormatVersion,
                dto.Id ?? string.Empty,
                dto.Scenario ?? string.Empty,
                (dto.ContentPackages ?? []).Cast<string>().ToArray(),
                dto.Session);
            NativeProjectManifestValidationResult validation = validationService.Validate(manifest);
            if (!validation.IsValid)
            {
                return new NativeProjectManifestDeserializationResult(
                    false,
                    manifest,
                    NativeProjectPersistenceErrorCode.ValidationFailed,
                    validation.Issues,
                    "Native project manifest validation failed.");
            }

            return new NativeProjectManifestDeserializationResult(
                true,
                manifest,
                NativeProjectPersistenceErrorCode.None,
                validation.Issues,
                null);
        }
        catch (JsonException)
        {
            return InvalidJson("Document is not valid JSON for native project manifest version 1.");
        }
        catch (NotSupportedException)
        {
            return InvalidJson("Native project manifest contains unsupported JSON values.");
        }
    }

    private static string Normalize(string path) =>
        NativeProjectManifestValidationService.NormalizeSeparators(path);

    private static NativeProjectManifestDeserializationResult InvalidJson(string message) =>
        new(false, null, NativeProjectPersistenceErrorCode.InvalidJson, [], message);

    private sealed record NativeProjectManifestDto(
        int FormatVersion,
        string? Id,
        string? Scenario,
        IReadOnlyList<string?>? ContentPackages,
        string? Session);
}
