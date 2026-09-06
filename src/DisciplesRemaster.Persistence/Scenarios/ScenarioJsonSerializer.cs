using System.Text.Json;
using System.Text.Json.Serialization;
using DisciplesRemaster.Content.Scenarios;
using DisciplesRemaster.Core.Geometry;

namespace DisciplesRemaster.Persistence.Scenarios;

public sealed class ScenarioJsonSerializer : IScenarioSerializer
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

    private readonly IScenarioValidationService validationService;

    public ScenarioJsonSerializer(IScenarioValidationService validationService)
    {
        this.validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    public ScenarioSerializationResult Serialize(ScenarioDefinition? scenario)
    {
        ScenarioValidationResult validation = validationService.Validate(scenario);
        if (!validation.IsValid || scenario is null)
        {
            return new ScenarioSerializationResult(
                false,
                null,
                ScenarioPersistenceErrorCode.ValidationFailed,
                validation.Issues,
                "Scenario validation failed.");
        }

        ScenarioDto dto = ToDto(scenario);
        byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        byte[] data = new byte[serialized.Length + 1];
        serialized.CopyTo(data, 0);
        data[^1] = (byte)'\n';

        return new ScenarioSerializationResult(
            true,
            data,
            ScenarioPersistenceErrorCode.None,
            validation.Issues,
            null);
    }

    public ScenarioDeserializationResult Deserialize(ReadOnlySpan<byte> data)
    {
        try
        {
            ScenarioDto? dto = JsonSerializer.Deserialize<ScenarioDto>(data, JsonOptions);
            if (dto is null)
            {
                return InvalidJson("Scenario JSON contains no document.");
            }

            if (dto.Map?.Terrain?.Any(placement => placement is null) == true)
            {
                return InvalidJson("Scenario terrain collection contains a null entry.");
            }

            ScenarioDefinition scenario = FromDto(dto);
            ScenarioValidationResult validation = validationService.Validate(scenario);
            if (!validation.IsValid)
            {
                return new ScenarioDeserializationResult(
                    false,
                    scenario,
                    ScenarioPersistenceErrorCode.ValidationFailed,
                    validation.Issues,
                    "Scenario validation failed.");
            }

            return new ScenarioDeserializationResult(
                true,
                scenario,
                ScenarioPersistenceErrorCode.None,
                validation.Issues,
                null);
        }
        catch (JsonException)
        {
            return InvalidJson("Scenario document is not valid JSON for native format version 1.");
        }
        catch (NotSupportedException)
        {
            return InvalidJson("Scenario document contains unsupported JSON values.");
        }
    }

    private static ScenarioDto ToDto(ScenarioDefinition scenario) =>
        new(
            scenario.FormatVersion,
            scenario.Id,
            scenario.Title,
            scenario.Description,
            new ScenarioMapDto(
                scenario.Map.Width,
                scenario.Map.Height,
                scenario.Map.DefaultTerrain,
                scenario.Map.Terrain
                    .OrderBy(placement => placement.Position.Y)
                    .ThenBy(placement => placement.Position.X)
                    .ThenBy(placement => placement.Terrain, StringComparer.Ordinal)
                    .Select(placement => new TerrainPlacementDto(
                        placement.Position.X,
                        placement.Position.Y,
                        placement.Terrain))
                    .ToArray()));

    private static ScenarioDefinition FromDto(ScenarioDto dto)
    {
        ScenarioMapDto? map = dto.Map;
        return new ScenarioDefinition(
            dto.FormatVersion,
            dto.Id ?? string.Empty,
            dto.Title ?? string.Empty,
            dto.Description,
            map is null
                ? null!
                : new ScenarioMapDefinition(
                    map.Width,
                    map.Height,
                    map.DefaultTerrain ?? string.Empty,
                    (map.Terrain ?? [])
                        .Select(placement => new TerrainPlacement(
                            new GridPosition(placement!.X, placement.Y),
                            placement.Terrain ?? string.Empty))
                        .ToArray()));
    }

    private static ScenarioDeserializationResult InvalidJson(string message) =>
        new(
            false,
            null,
            ScenarioPersistenceErrorCode.InvalidJson,
            [],
            message);

    private sealed record ScenarioDto(
        int FormatVersion,
        string? Id,
        string? Title,
        string? Description,
        ScenarioMapDto? Map);

    private sealed record ScenarioMapDto(
        int Width,
        int Height,
        string? DefaultTerrain,
        IReadOnlyList<TerrainPlacementDto?>? Terrain);

    private sealed record TerrainPlacementDto(int X, int Y, string? Terrain);
}
