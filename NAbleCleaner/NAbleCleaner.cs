using System.ServiceProcess;
using Cleaners;
using Serilog;

namespace NAbleCleaner;

/// <summary>
/// This cleaner is based on https://documentation.n-able.com/remote-management/troubleshooting/Content/kb/How-to-clear-the-CacheService-s-cache-files.htm
/// It says:
/// Q: The cache files under C:\ProgramData\MspPlatform\File.Cache.Service.Agent\cache are taking up a lot of space
///
/// A: To safely clear the CacheService:
///     Go to C:\ProgramData\MspPlatform\File.Cache.Service.Agent\cache
///       * Remove all files in that folder
///       * Open services.msc
///       * Restart the File Cache Service Agent
/// </summary>
public class NAbleCleaner : CleanerBase
{

    /// <returns></returns>
    public override IReadOnlyList<string> GetItemsToClean()
    {
        return new List<string> { @"C:\ProgramData\MspPlatform\File.Cache.Service.Agent\cache\*" };
    }

    public override void Clean()
    {
        var servicePresent = StopService();
        if (servicePresent)
        {
            base.Clean();
            StartService();
        }
    }

    private bool StopService()
    {
        Log.Verbose("Looking for services.");
        var services = ServiceController.GetServices();
        foreach (var service in services)
        {
            Log.Verbose("{serviceName}, {serviceDisplayName}, {startType}, {status}", service.ServiceName, service.DisplayName, service.StartType, service.Status);
        }
        return false;
    }

    private void StartService()
    {
        throw new NotImplementedException();
    }
}