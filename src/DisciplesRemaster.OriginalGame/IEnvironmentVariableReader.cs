namespace DisciplesRemaster.OriginalGame;

/// <summary>Reads environment variables without coupling validation logic to process-global state.</summary>
public interface IEnvironmentVariableReader
{
    /// <summary>Gets the value of an environment variable, or <see langword="null"/> when it is absent.</summary>
    string? GetEnvironmentVariable(string variableName);
}
