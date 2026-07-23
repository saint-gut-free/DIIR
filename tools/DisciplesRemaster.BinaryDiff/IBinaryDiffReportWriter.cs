using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.BinaryDiff;

public interface IBinaryDiffReportWriter
{
    BinaryDiffOutputValidationResult ValidateOutputPath(
        string outputPath,
        string fileAPath,
        string fileBPath,
        OriginalGameLocation? originalGameLocation);

    BinaryDiffReportPaths WriteReports(
        BinaryDiffReport report,
        string outputPath,
        BinaryDiffOutputFormat format);
}

public sealed record BinaryDiffOutputValidationResult(
    bool IsValid,
    string? NormalizedOutputPath,
    BinaryDiffWarningCode? ErrorCode,
    string Message);

public sealed record BinaryDiffReportPaths(string? JsonPath, string? TextPath);
