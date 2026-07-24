namespace DisciplesRemaster.OriginalGame.Research.MapExperiments;

public interface IMapExperimentValidationService
{
    MapExperimentValidationResult LoadAndValidate(string inputPath);

    MapExperimentValidationResult Validate(IEnumerable<MapExperimentRecord> experiments);
}

public sealed class MapExperimentInputException(string safeMessage, Exception? innerException = null)
    : IOException(safeMessage, innerException);
