namespace DisciplesRemaster.OriginalGame;

public static class OriginalGameFileSignatureDetector
{
    public const int MaximumPrefixLength = 32;

    public static OriginalGameFileSignature Detect(ReadOnlySpan<byte> prefix)
    {
        if (StartsWith(prefix, [0x4D, 0x5A])) return OriginalGameFileSignature.WindowsPortableExecutable;
        if (StartsWith(prefix, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return OriginalGameFileSignature.Png;
        if (StartsWith(prefix, [0xFF, 0xD8, 0xFF])) return OriginalGameFileSignature.Jpeg;
        if (StartsWith(prefix, "GIF87a"u8) || StartsWith(prefix, "GIF89a"u8)) return OriginalGameFileSignature.Gif;
        if (StartsWith(prefix, "BM"u8)) return OriginalGameFileSignature.Bmp;
        if (prefix.Length >= 12 && StartsWith(prefix, "RIFF"u8) && prefix[8..12].SequenceEqual("WAVE"u8)) return OriginalGameFileSignature.Wave;
        if (StartsWith(prefix, "OggS"u8)) return OriginalGameFileSignature.Ogg;
        if (StartsWith(prefix, [0x50, 0x4B, 0x03, 0x04]) || StartsWith(prefix, [0x50, 0x4B, 0x05, 0x06]) || StartsWith(prefix, [0x50, 0x4B, 0x07, 0x08])) return OriginalGameFileSignature.Zip;
        if (StartsWith(prefix, [0x1F, 0x8B])) return OriginalGameFileSignature.Gzip;
        if (StartsWith(prefix, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C])) return OriginalGameFileSignature.SevenZip;
        if (StartsWith(prefix, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07])) return OriginalGameFileSignature.Rar;
        if (StartsWith(prefix, [0xEF, 0xBB, 0xBF])) return OriginalGameFileSignature.Utf8Bom;
        if (StartsWith(prefix, [0xFF, 0xFE])) return OriginalGameFileSignature.Utf16LittleEndianBom;
        if (StartsWith(prefix, [0xFE, 0xFF])) return OriginalGameFileSignature.Utf16BigEndianBom;
        if (IsConservativePlainText(prefix)) return OriginalGameFileSignature.PlainText;
        return OriginalGameFileSignature.Unknown;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> expected) =>
        value.StartsWith(expected);

    private static bool IsConservativePlainText(ReadOnlySpan<byte> prefix)
    {
        if (prefix.IsEmpty)
        {
            return false;
        }

        foreach (byte value in prefix)
        {
            bool isWhitespace = value is 0x09 or 0x0A or 0x0D;
            bool isPrintableAscii = value is >= 0x20 and <= 0x7E;
            if (!isWhitespace && !isPrintableAscii)
            {
                return false;
            }
        }

        return true;
    }
}
