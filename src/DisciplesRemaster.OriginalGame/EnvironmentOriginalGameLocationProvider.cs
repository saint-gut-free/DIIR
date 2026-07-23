using System.Security;

namespace DisciplesRemaster.OriginalGame;

/// <summary>Validates a research directory supplied through <c>D2_ORIGINAL_PATH</c>.</summary>
public sealed class EnvironmentOriginalGameLocationProvider : IOriginalGameLocationProvider
{
    public const string EnvironmentVariableName = "D2_ORIGINAL_PATH";

    private const string NotConfiguredDisplayPath = "<not-configured>";
    private const string ConfiguredDisplayPath = "<configured-original-game-directory>";

    private readonly IEnvironmentVariableReader environmentVariableReader;

    public EnvironmentOriginalGameLocationProvider(IEnvironmentVariableReader environmentVariableReader)
    {
        this.environmentVariableReader = environmentVariableReader ??
            throw new ArgumentNullException(nameof(environmentVariableReader));
    }

    public OriginalGameLocationValidationResult Validate()
    {
        string? configuredValue;

        try
        {
            configuredValue = environmentVariableReader.GetEnvironmentVariable(EnvironmentVariableName);
        }
        catch (Exception)
        {
            return Failure(
                OriginalGameLocationValidationError.UnexpectedError,
                "The environment configuration could not be read.",
                NotConfiguredDisplayPath);
        }

        if (configuredValue is null)
        {
            return Failure(
                OriginalGameLocationValidationError.EnvironmentVariableMissing,
                $"Environment variable {EnvironmentVariableName} is not set.",
                NotConfiguredDisplayPath);
        }

        string candidate = configuredValue.Trim();
        if (candidate.Length == 0)
        {
            return Failure(
                OriginalGameLocationValidationError.EnvironmentVariableEmpty,
                $"Environment variable {EnvironmentVariableName} is empty.",
                NotConfiguredDisplayPath);
        }

        candidate = RemoveOuterQuotes(candidate);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return Failure(
                OriginalGameLocationValidationError.EnvironmentVariableEmpty,
                $"Environment variable {EnvironmentVariableName} is empty.",
                NotConfiguredDisplayPath);
        }

        string fullPath;
        string displayPath;

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            displayPath = Redact(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Failure(
                OriginalGameLocationValidationError.InvalidPath,
                "The configured value is not a valid path.",
                ConfiguredDisplayPath);
        }
        catch (Exception)
        {
            return Failure(
                OriginalGameLocationValidationError.UnexpectedError,
                "The configured path could not be normalized.",
                ConfiguredDisplayPath);
        }

        try
        {
            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.Directory) == 0)
            {
                return Failure(
                    OriginalGameLocationValidationError.PathIsNotDirectory,
                    "The configured path does not identify a directory.",
                    displayPath);
            }

            return OriginalGameLocationValidationResult.Success(
                new OriginalGameLocation(fullPath, displayPath));
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Failure(
                OriginalGameLocationValidationError.DirectoryNotFound,
                "The configured directory does not exist.",
                displayPath);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            return Failure(
                OriginalGameLocationValidationError.DirectoryInaccessible,
                "The configured directory metadata cannot be read safely.",
                displayPath);
        }
        catch (Exception)
        {
            return Failure(
                OriginalGameLocationValidationError.UnexpectedError,
                "An unexpected error occurred while validating the configured directory.",
                displayPath);
        }
    }

    private static string RemoveOuterQuotes(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;

    private static string Redact(string fullPath)
    {
        string finalSegment = Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath));
        return string.IsNullOrEmpty(finalSegment)
            ? ConfiguredDisplayPath
            : $"{ConfiguredDisplayPath}/{finalSegment}";
    }

    private static OriginalGameLocationValidationResult Failure(
        OriginalGameLocationValidationError error,
        string message,
        string displayPath) =>
        OriginalGameLocationValidationResult.Failure(error, message, displayPath);
}
