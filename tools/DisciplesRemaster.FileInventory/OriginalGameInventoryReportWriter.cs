using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DisciplesRemaster.OriginalGame;

namespace DisciplesRemaster.FileInventory;

public sealed class OriginalGameInventoryReportWriter : IOriginalGameInventoryReportWriter
{
    private const int MaximumSummaryItems = 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public OriginalGameInventoryOutputValidationResult ValidateOutputPath(
        string outputPath,
        OriginalGameLocation location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(location);

        try
        {
            string normalizedOutput = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputPath));
            string normalizedSource = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location.FullPath));
            string resolvedOutput = ResolveExistingLinks(normalizedOutput);
            string resolvedSource = ResolveExistingLinks(normalizedSource);

            if (IsSameOrDescendant(normalizedOutput, normalizedSource) ||
                IsSameOrDescendant(resolvedOutput, resolvedSource))
            {
                return new OriginalGameInventoryOutputValidationResult(
                    false,
                    null,
                    OriginalGameInventoryWarningCode.OutputInsideOriginalGameDirectory,
                    "The output directory must not be the original-game directory or one of its descendants.");
            }

            return new OriginalGameInventoryOutputValidationResult(
                true,
                normalizedOutput,
                null,
                "The output directory is outside the original-game directory.");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return new OriginalGameInventoryOutputValidationResult(
                false,
                null,
                OriginalGameInventoryWarningCode.InvalidOutputPath,
                "The output directory path is invalid or cannot be safely resolved.");
        }
    }

    public OriginalGameInventoryReportPaths WriteReports(
        OriginalGameInventory inventory,
        string outputPath)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Directory.CreateDirectory(outputPath);
        string jsonPath = Path.Combine(outputPath, "inventory.json");
        string markdownPath = Path.Combine(outputPath, "inventory-summary.md");
        File.WriteAllText(jsonPath, SerializeJson(inventory), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(markdownPath, CreateMarkdown(inventory), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new OriginalGameInventoryReportPaths(jsonPath, markdownPath);
    }

    public static string SerializeJson(OriginalGameInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return JsonSerializer.Serialize(inventory, JsonOptions) + "\n";
    }

    public static string CreateMarkdown(OriginalGameInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var builder = new StringBuilder();
        builder.AppendLine("# Original game installation inventory");
        builder.AppendLine();
        builder.AppendLine($"- Source: `{EscapeInline(inventory.SourceLocation)}`");
        builder.AppendLine($"- Files processed: {inventory.Summary.FilesProcessed}");
        builder.AppendLine($"- Files skipped: {inventory.Summary.FilesSkipped}");
        builder.AppendLine($"- Total size: {inventory.Summary.TotalSizeBytes} bytes");
        builder.AppendLine();

        AppendCountTable(builder, "Files by extension", inventory.Summary.Extensions, "Extension");
        AppendCountTable(builder, "Files by category", inventory.Summary.Categories, "Category");

        AppendEntryTable(
            builder,
            "Largest files",
            inventory.Files.Where(entry => entry.IsReadSuccessful)
                .OrderByDescending(entry => entry.SizeBytes)
                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Take(MaximumSummaryItems));

        builder.AppendLine("## Duplicate groups");
        builder.AppendLine();
        OriginalGameInventoryDuplicateGroup[] duplicateGroups = inventory.Summary.DuplicateGroups
            .Where(group => group.SizeBytes > 0)
            .Take(MaximumSummaryItems)
            .ToArray();
        if (duplicateGroups.Length == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
        }
        else
        {
            foreach (OriginalGameInventoryDuplicateGroup group in duplicateGroups)
            {
                builder.AppendLine($"- `{group.Sha256}` ({group.SizeBytes} bytes)");
                foreach (string path in group.RelativePaths)
                {
                    builder.AppendLine($"  - `{EscapeInline(path)}`");
                }
            }

            builder.AppendLine();
        }

        AppendEntryTable(builder, "Possible executables", EntriesByCategory(inventory, OriginalGameFileCategory.Executable));
        AppendEntryTable(builder, "Possible libraries", EntriesByCategory(inventory, OriginalGameFileCategory.Library));
        AppendEntryTable(builder, "Map or scenario candidates", EntriesByCategory(inventory, OriginalGameFileCategory.MapOrScenarioCandidate));
        AppendEntryTable(builder, "Save candidates", EntriesByCategory(inventory, OriginalGameFileCategory.SaveCandidate));
        AppendEntryTable(builder, "Archives", EntriesByCategory(inventory, OriginalGameFileCategory.Archive));
        AppendEntryTable(
            builder,
            "Unknown extensions",
            inventory.Files.Where(entry => entry.IsReadSuccessful && entry.Category == OriginalGameFileCategory.Unknown)
                .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Take(MaximumSummaryItems));

        builder.AppendLine("## Warnings");
        builder.AppendLine();
        if (inventory.Warnings.Count == 0)
        {
            builder.AppendLine("None.");
        }
        else
        {
            foreach (OriginalGameInventoryWarning warning in inventory.Warnings.Take(MaximumSummaryItems))
            {
                builder.AppendLine($"- `{warning.Code}` at `{EscapeInline(warning.RelativePath)}`: {warning.Message}");
            }

            if (inventory.Warnings.Count > MaximumSummaryItems)
            {
                builder.AppendLine($"- {inventory.Warnings.Count - MaximumSummaryItems} additional warning(s); see `inventory.json`.");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Analysis limitations");
        builder.AppendLine();
        builder.AppendLine("This report contains technical metadata, conservative common-format signatures, and SHA-256 values only. It does not parse proprietary formats, extract content, verify the game version, or establish game mechanics. Candidate classifications are heuristic.");
        return builder.ToString();
    }

    private static IEnumerable<OriginalGameInventoryEntry> EntriesByCategory(
        OriginalGameInventory inventory,
        OriginalGameFileCategory category) =>
        inventory.Files.Where(entry => entry.IsReadSuccessful && entry.Category == category)
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .Take(MaximumSummaryItems);

    private static void AppendCountTable(
        StringBuilder builder,
        string title,
        IReadOnlyList<OriginalGameInventoryCount> counts,
        string label)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        builder.AppendLine($"| {label} | Count |");
        builder.AppendLine("|---|---:|");
        foreach (OriginalGameInventoryCount count in counts)
        {
            string name = string.IsNullOrEmpty(count.Name) ? "(none)" : count.Name;
            builder.AppendLine($"| {EscapeTable(name)} | {count.Count} |");
        }

        builder.AppendLine();
    }

    private static void AppendEntryTable(
        StringBuilder builder,
        string title,
        IEnumerable<OriginalGameInventoryEntry> entries)
    {
        builder.AppendLine($"## {title}");
        builder.AppendLine();
        builder.AppendLine("| Relative path | Size | Signature |");
        builder.AppendLine("|---|---:|---|");
        foreach (OriginalGameInventoryEntry entry in entries)
        {
            builder.AppendLine($"| {EscapeTable(entry.RelativePath)} | {entry.SizeBytes} | {entry.Signature} |");
        }

        builder.AppendLine();
    }

    private static string EscapeInline(string value) => value.Replace("`", "'", StringComparison.Ordinal);

    private static string EscapeTable(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal).Replace("`", "'", StringComparison.Ordinal);

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(candidate, root, comparison))
        {
            return true;
        }

        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(rootWithSeparator, comparison);
    }

    private static string ResolveExistingLinks(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        string current = root;
        string remainder = fullPath[root.Length..];
        foreach (string segment in remainder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current))
            {
                continue;
            }

            var info = new DirectoryInfo(current);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null)
                {
                    current = Path.GetFullPath(target.FullName);
                }
            }
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }
}
