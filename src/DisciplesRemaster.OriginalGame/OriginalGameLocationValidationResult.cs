namespace DisciplesRemaster.OriginalGame;

/// <summary>The structured result of validating the configured research directory.</summary>
public sealed record OriginalGameLocationValidationResult
{
    private OriginalGameLocationValidationResult(
        OriginalGameLocation? location,
        OriginalGameLocationValidationError error,
        string message,
        string displayPath)
    {
        Location = location;
        Error = error;
        Message = message;
        DisplayPath = displayPath;
    }

    public bool IsSuccess => Error == OriginalGameLocationValidationError.None;

    public OriginalGameLocation? Location { get; }

    public OriginalGameLocationValidationError Error { get; }

    public string Message { get; }

    public string DisplayPath { get; }

    public static OriginalGameLocationValidationResult Success(OriginalGameLocation location) =>
        new(location, OriginalGameLocationValidationError.None, "The configured directory is accessible.", location.DisplayPath);

    public static OriginalGameLocationValidationResult Failure(
        OriginalGameLocationValidationError error,
        string message,
        string displayPath) =>
        new(null, error, message, displayPath);
}
