using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.FileInventory;

public interface IOriginalGameInventoryReportWriter
{
    OriginalGameInventoryOutputValidationResult ValidateOutputPath(
        string outputPath,
        OriginalGameLocation location);

    OriginalGameInventoryReportPaths WriteReports(
        OriginalGameInventory inventory,
        string outputPath);
}

public sealed record OriginalGameInventoryOutputValidationResult(
    bool IsValid,
    string? NormalizedOutputPath,
    OriginalGameInventoryWarningCode? ErrorCode,
    string Message);

public sealed record OriginalGameInventoryReportPaths(string JsonPath, string MarkdownPath);
