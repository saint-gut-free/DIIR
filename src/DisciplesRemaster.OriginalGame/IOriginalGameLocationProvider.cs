namespace DisciplesRemaster.OriginalGame;

/// <summary>Provides a validated, read-only research input directory.</summary>
public interface IOriginalGameLocationProvider
{
    /// <summary>Validates the configured original-game research directory.</summary>
    OriginalGameLocationValidationResult Validate();
}
