using System.Security;
using System.Security.Cryptography;

namespace DisciplesRemaster.OriginalGame;

public sealed class OriginalGameInventoryService : IOriginalGameInventoryService
{
    private static readonly StringComparer PathComparer = StringComparer.Ordinal;

    public OriginalGameInventory CreateInventory(
        OriginalGameLocation location,
        OriginalGameInventoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum depth must be positive.");
        }

        var entries = new List<OriginalGameInventoryEntry>();
        var warnings = new List<OriginalGameInventoryWarning>();
        int skippedCount = 0;

        VisitDirectory(location.FullPath, relativeDirectory: string.Empty, depth: 0);

        entries.Sort((left, right) => PathComparer.Compare(left.RelativePath, right.RelativePath));
        warnings.Sort(CompareWarnings);
        OriginalGameInventorySummary summary = BuildSummary(entries, skippedCount);

        return new OriginalGameInventory(
            OriginalGameInventory.CurrentReportFormatVersion,
            location.DisplayPath,
            options,
            entries,
            warnings,
            summary);

        void VisitDirectory(string directoryPath, string relativeDirectory, int depth)
        {
            string[] children;
            try
            {
                children = Directory.GetFileSystemEntries(directoryPath);
                Array.Sort(children, (left, right) => PathComparer.Compare(
                    NormalizeRelativePath(Path.GetRelativePath(location.FullPath, left)),
                    NormalizeRelativePath(Path.GetRelativePath(location.FullPath, right))));
            }
            catch (Exception exception) when (IsExpectedIoException(exception))
            {
                warnings.Add(new OriginalGameInventoryWarning(
                    OriginalGameInventoryWarningCode.DirectoryInaccessible,
                    NormalizeRelativePath(relativeDirectory),
                    "Directory entries could not be read."));
                skippedCount++;
                return;
            }

            foreach (string childPath in children)
            {
                string relativePath = NormalizeRelativePath(Path.GetRelativePath(location.FullPath, childPath));
                if (!IsSafeRelativePath(relativePath))
                {
                    warnings.Add(new OriginalGameInventoryWarning(
                        OriginalGameInventoryWarningCode.ReparsePointSkipped,
                        "<outside-root>",
                        "An entry resolving outside the configured root was skipped."));
                    skippedCount++;
                    continue;
                }

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(childPath);
                }
                catch (Exception exception) when (IsExpectedIoException(exception))
                {
                    warnings.Add(new OriginalGameInventoryWarning(
                        OriginalGameInventoryWarningCode.UnsupportedFileMetadata,
                        relativePath,
                        "Entry metadata could not be read."));
                    skippedCount++;
                    continue;
                }

                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                bool isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
                bool isHidden = (attributes & FileAttributes.Hidden) != 0;

                if (isReparsePoint)
                {
                    warnings.Add(new OriginalGameInventoryWarning(
                        OriginalGameInventoryWarningCode.ReparsePointSkipped,
                        relativePath,
                        "A symbolic link, junction, or reparse point was not followed."));
                    skippedCount++;
                    continue;
                }

                if (isHidden && !options.IncludeHidden)
                {
                    warnings.Add(new OriginalGameInventoryWarning(
                        OriginalGameInventoryWarningCode.HiddenItemSkipped,
                        relativePath,
                        "A hidden item was skipped by the current options."));
                    skippedCount++;
                    continue;
                }

                int childDepth = depth + 1;
                if (childDepth > options.MaxDepth)
                {
                    warnings.Add(new OriginalGameInventoryWarning(
                        OriginalGameInventoryWarningCode.MaximumDepthExceeded,
                        relativePath,
                        "The configured maximum depth was exceeded."));
                    skippedCount++;
                    continue;
                }

                if (isDirectory)
                {
                    VisitDirectory(childPath, relativePath, childDepth);
                    continue;
                }

                entries.Add(ProcessFile(childPath, relativePath, childDepth, attributes, isHidden));
            }
        }

        OriginalGameInventoryEntry ProcessFile(
            string fullPath,
            string relativePath,
            int depth,
            FileAttributes attributes,
            bool isHidden)
        {
            var fileInfo = new FileInfo(fullPath);
            long sizeBefore;
            DateTime timestampBefore;

            try
            {
                fileInfo.Refresh();
                sizeBefore = fileInfo.Length;
                timestampBefore = fileInfo.LastWriteTimeUtc;
            }
            catch (Exception exception) when (IsExpectedIoException(exception))
            {
                warnings.Add(new OriginalGameInventoryWarning(
                    OriginalGameInventoryWarningCode.UnsupportedFileMetadata,
                    relativePath,
                    "File metadata could not be read."));
                skippedCount++;
                return FailedEntry(relativePath, depth, attributes, isHidden, "File metadata could not be read.");
            }

            OriginalGameFileSignature signature = OriginalGameFileSignature.Unknown;
            string? hash = null;

            try
            {
                using var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 81920,
                    FileOptions.SequentialScan);

                Span<byte> prefix = stackalloc byte[OriginalGameFileSignatureDetector.MaximumPrefixLength];
                int prefixLength = stream.Read(prefix);
                signature = OriginalGameFileSignatureDetector.Detect(prefix[..prefixLength]);
                stream.Position = 0;
                hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }
            catch (UnauthorizedAccessException)
            {
                return AddReadFailure(OriginalGameInventoryWarningCode.FileInaccessible, "File could not be opened for read-only access.");
            }
            catch (SecurityException)
            {
                return AddReadFailure(OriginalGameInventoryWarningCode.FileInaccessible, "File could not be opened for read-only access.");
            }
            catch (IOException)
            {
                return AddReadFailure(OriginalGameInventoryWarningCode.HashingFailed, "File hashing did not complete.");
            }

            string? entryWarning = null;
            try
            {
                fileInfo.Refresh();
                if (fileInfo.Length != sizeBefore || fileInfo.LastWriteTimeUtc != timestampBefore)
                {
                    entryWarning = "File metadata changed during read; the hash may not represent a stable state.";
                    warnings.Add(new OriginalGameInventoryWarning(
                        OriginalGameInventoryWarningCode.FileChangedDuringRead,
                        relativePath,
                        entryWarning));
                }
            }
            catch (Exception exception) when (IsExpectedIoException(exception))
            {
                entryWarning = "File metadata could not be confirmed after read.";
                warnings.Add(new OriginalGameInventoryWarning(
                    OriginalGameInventoryWarningCode.UnsupportedFileMetadata,
                    relativePath,
                    entryWarning));
            }

            return new OriginalGameInventoryEntry(
                relativePath,
                Path.GetFileName(relativePath),
                NormalizeExtension(relativePath),
                sizeBefore,
                hash,
                timestampBefore,
                depth,
                attributes,
                Classify(relativePath, signature),
                signature,
                isHidden,
                IsReparsePoint: false,
                IsReadSuccessful: true,
                entryWarning);

            OriginalGameInventoryEntry AddReadFailure(
                OriginalGameInventoryWarningCode code,
                string message)
            {
                warnings.Add(new OriginalGameInventoryWarning(code, relativePath, message));
                skippedCount++;
                return FailedEntry(relativePath, depth, attributes, isHidden, message, sizeBefore, timestampBefore);
            }
        }
    }

    private static OriginalGameInventoryEntry FailedEntry(
        string relativePath,
        int depth,
        FileAttributes attributes,
        bool isHidden,
        string warning,
        long sizeBytes = 0,
        DateTime lastWriteTimeUtc = default) =>
        new(
            relativePath,
            Path.GetFileName(relativePath),
            NormalizeExtension(relativePath),
            sizeBytes,
            Sha256: null,
            lastWriteTimeUtc,
            depth,
            attributes,
            Classify(relativePath, OriginalGameFileSignature.Unknown),
            OriginalGameFileSignature.Unknown,
            isHidden,
            IsReparsePoint: false,
            IsReadSuccessful: false,
            warning);

    private static OriginalGameInventorySummary BuildSummary(
        IReadOnlyCollection<OriginalGameInventoryEntry> entries,
        int skippedCount)
    {
        OriginalGameInventoryEntry[] successful = entries.Where(entry => entry.IsReadSuccessful).ToArray();
        IReadOnlyList<OriginalGameInventoryCount> extensions = successful
            .GroupBy(entry => entry.Extension, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new OriginalGameInventoryCount(group.Key, group.LongCount()))
            .ToArray();
        IReadOnlyList<OriginalGameInventoryCount> categories = successful
            .GroupBy(entry => entry.Category)
            .OrderBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group => new OriginalGameInventoryCount(group.Key.ToString(), group.LongCount()))
            .ToArray();
        IReadOnlyList<OriginalGameInventoryDuplicateGroup> duplicates = successful
            .Where(entry => entry.Sha256 is not null)
            .GroupBy(entry => (entry.Sha256!, entry.SizeBytes))
            .Where(group => group.Count() >= 2)
            .OrderBy(group => group.Key.Item1, StringComparer.Ordinal)
            .ThenBy(group => group.Key.SizeBytes)
            .Select(group => new OriginalGameInventoryDuplicateGroup(
                group.Key.Item1,
                group.Key.SizeBytes,
                group.Select(entry => entry.RelativePath).OrderBy(path => path, StringComparer.Ordinal).ToArray()))
            .ToArray();

        return new OriginalGameInventorySummary(
            successful.Length,
            skippedCount + entries.Count - successful.Length,
            successful.Sum(entry => entry.SizeBytes),
            extensions,
            categories,
            duplicates);
    }

    private static int CompareWarnings(OriginalGameInventoryWarning left, OriginalGameInventoryWarning right)
    {
        int pathComparison = PathComparer.Compare(left.RelativePath, right.RelativePath);
        return pathComparison != 0 ? pathComparison : left.Code.CompareTo(right.Code);
    }

    private static bool IsExpectedIoException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SecurityException;

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static bool IsSafeRelativePath(string path) =>
        path.Length > 0 &&
        path != ".." &&
        !path.StartsWith("../", StringComparison.Ordinal) &&
        !Path.IsPathRooted(path);

    private static string NormalizeExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) ? string.Empty : extension.ToLowerInvariant();
    }

    private static OriginalGameFileCategory Classify(string relativePath, OriginalGameFileSignature signature)
    {
        string extension = NormalizeExtension(relativePath);
        if (extension == ".exe") return OriginalGameFileCategory.Executable;
        if (extension == ".dll") return OriginalGameFileCategory.Library;
        if (extension is ".map" or ".scn" or ".scenario") return OriginalGameFileCategory.MapOrScenarioCandidate;
        if (extension == ".sav") return OriginalGameFileCategory.SaveCandidate;
        if (signature is OriginalGameFileSignature.Png or OriginalGameFileSignature.Jpeg or OriginalGameFileSignature.Gif or OriginalGameFileSignature.Bmp || extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp") return OriginalGameFileCategory.Image;
        if (signature is OriginalGameFileSignature.Wave or OriginalGameFileSignature.Ogg || extension is ".wav" or ".ogg" or ".mp3" or ".mid" or ".midi") return OriginalGameFileCategory.Audio;
        if (signature is OriginalGameFileSignature.Zip or OriginalGameFileSignature.Gzip or OriginalGameFileSignature.SevenZip or OriginalGameFileSignature.Rar || extension is ".zip" or ".gz" or ".7z" or ".rar") return OriginalGameFileCategory.Archive;
        if (signature is OriginalGameFileSignature.Utf8Bom or OriginalGameFileSignature.Utf16LittleEndianBom or OriginalGameFileSignature.Utf16BigEndianBom or OriginalGameFileSignature.PlainText || extension is ".txt" or ".md" or ".log") return OriginalGameFileCategory.Text;
        if (extension is ".ini" or ".cfg" or ".conf" or ".xml" or ".json") return OriginalGameFileCategory.Configuration;
        if (extension is ".dat" or ".bin") return OriginalGameFileCategory.Data;
        if (signature == OriginalGameFileSignature.WindowsPortableExecutable) return OriginalGameFileCategory.Executable;
        return OriginalGameFileCategory.Unknown;
    }
}
