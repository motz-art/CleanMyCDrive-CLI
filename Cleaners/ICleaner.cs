namespace Cleaners;

public interface ICleaner
{
    IReadOnlyList<string> GetItemsToClean();

    void Clean();
}