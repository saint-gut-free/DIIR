namespace DisciplesRemaster.OriginalGame;

/// <summary>Reads variables from the current process environment.</summary>
public sealed class ProcessEnvironmentVariableReader : IEnvironmentVariableReader
{
    /// <inheritdoc />
    public string? GetEnvironmentVariable(string variableName) =>
        Environment.GetEnvironmentVariable(variableName);
}
