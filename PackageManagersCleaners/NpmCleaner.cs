using Cleaners;

namespace PackageManagersCleaners
{
    public class NpmCleaner : ICleaner
    {
        public IReadOnlyList<string> GetItemsToClean()
        {
            throw new NotImplementedException();
        }

        public void Clean()
        {
            ProcessHelper.Run("cmd.exe", "/c npm.cmd cache clean --force");
        }
    }
}