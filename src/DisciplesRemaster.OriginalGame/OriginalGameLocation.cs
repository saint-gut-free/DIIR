namespace DisciplesRemaster.OriginalGame;

/// <summary>A normalized and metadata-accessible research input directory.</summary>
/// <param name="FullPath">The normalized absolute path for internal use.</param>
/// <param name="DisplayPath">A redacted path safe for ordinary output.</param>
public sealed record OriginalGameLocation(string FullPath, string DisplayPath)
{
    /// <summary>Indicates that directory metadata was read successfully.</summary>
    public bool IsAccessible => true;

    /// <summary>Returns the redacted representation to avoid accidental path disclosure.</summary>
    public override string ToString() => DisplayPath;
}
