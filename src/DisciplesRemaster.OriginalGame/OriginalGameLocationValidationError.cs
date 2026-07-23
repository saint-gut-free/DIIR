namespace DisciplesRemaster.OriginalGame;

/// <summary>Stable failure codes for original-game directory configuration.</summary>
public enum OriginalGameLocationValidationError
{
    None = 0,
    EnvironmentVariableMissing,
    EnvironmentVariableEmpty,
    InvalidPath,
    DirectoryNotFound,
    PathIsNotDirectory,
    DirectoryInaccessible,
    UnexpectedError,
}
