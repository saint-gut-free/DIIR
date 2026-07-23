using DisciplesRemaster.BinaryDiff;

namespace DisciplesRemaster.Tools.Tests;

internal static class BinaryDiffTestData
{
    public static BinaryDiffReport CreateReport(IReadOnlyList<BinaryDiffWarning>? warnings = null)
    {
        IReadOnlyList<BinaryDiffWarning> reportWarnings = warnings ?? [];
        var fileA = new BinaryFileMetadata("File A", "baseline.bin", 4, new string('a', 64), DateTime.UnixEpoch, true);
        var fileB = new BinaryFileMetadata("File B", "changed.bin", 4, new string('b', 64), DateTime.UnixEpoch, true);
        var range = new BinaryChangedRange(
            0,
            "0x0000000000000000",
            0,
            "0x0000000000000000",
            1,
            1,
            string.Empty,
            string.Empty,
            "0C",
            "0D",
            "00 00 00",
            "00 00 00",
            [new BinaryNumericInterpretation("Int32 little-endian", "12", "13", "Low")],
            [],
            "Low",
            false,
            0);
        return new BinaryDiffReport(
            BinaryDiffReport.CurrentReportFormatVersion,
            fileA,
            fileB,
            new BinaryDiffOptions(16, 200, []),
            new BinaryComparisonSummary(false, true, 0, 4, 1, 1, 0, 3, !reportWarnings.Any(w => w.Code == BinaryDiffWarningCode.FileChangedDuringRead), false),
            [range],
            [],
            ["Numeric interpretations are hypotheses."],
            reportWarnings);
    }
}
