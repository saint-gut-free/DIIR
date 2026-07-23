namespace DisciplesRemaster.OriginalGame;

public interface IOriginalGameInventoryService
{
    OriginalGameInventory CreateInventory(
        OriginalGameLocation location,
        OriginalGameInventoryOptions options);
}
