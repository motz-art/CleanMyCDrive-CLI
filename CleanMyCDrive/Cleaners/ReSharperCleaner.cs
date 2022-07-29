namespace CleanMyCDrive.Cleaners;
public class ReSharperCleaner : CleanerBase
{

    /// <summary>
    /// This cleaner is based on https://resharper-support.jetbrains.com/hc/en-us/articles/360010771119--NET-tools-data-takes-up-a-lot-of-disk-space-what-folders-can-be-safely-removed-#:~:text=We%20can%20guarantee%20that%20only,%25Temp%25%5CJetLogs
    /// It says:
    /// # .NET tools data takes up a lot of disk space, what folders can be safely removed?
    /// 
    /// We can guarantee that only the following folders can be safely removed:
    /// * %LocalAppData%\JetBrains\Transient
    /// * %Temp%\JetBrains
    /// * %Temp%\JetLogs
    /// </summary>
    public override IReadOnlyList<string> GetItemsToClean()
    {
        return new List<string>
        {
            @"%LocalAppData%\JetBrains\Transient\*",
            @"%Temp%\JetBrains\*",
            @"%Temp%\JetLogs\*"
        }.AsReadOnly();
    }
}