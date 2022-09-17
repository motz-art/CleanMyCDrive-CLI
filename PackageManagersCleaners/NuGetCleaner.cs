using Cleaners;

namespace PackageManagersCleaners;

public class NuGetCleaner : ICleaner
{
    public IReadOnlyList<string> GetItemsToClean()
    {
        var strings = ProcessHelper.ReadAllLines("dotnet", "nuget locals all --force-english-output --clear");
        
        return strings
            .Select(x => x.Split(':', 2).Skip(1).First())
            .ToList();
    }

    public void Clean()
    {
        ProcessHelper.Run("dotnet", "nuget locals all --force-english-output --clear");
    }
}