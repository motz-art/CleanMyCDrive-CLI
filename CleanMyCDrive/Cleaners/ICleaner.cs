namespace CleanMyCDrive.Cleaners;

public interface ICleaner
{
    IReadOnlyList<string> GetItemsToClean();

    void Clean();
}