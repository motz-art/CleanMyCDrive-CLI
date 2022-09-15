// See https://aka.ms/new-console-template for more information

using Cleaners;
using CleanMyCDrive;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

Console.WriteLine("Clean My C: Drive v1.0 Beta!");

var logLevel = args.Contains("-v") ? LogEventLevel.Verbose :
    args.Contains("-d") ? LogEventLevel.Debug :
    LogEventLevel.Information;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(logLevel)
    .WriteTo.Async(a => a.File(new CompactJsonFormatter(), "run.log"))
    .WriteTo.Async(a => a.Console())
    .CreateLogger();

try
{
    if (args.Length > 0 && args[0].Equals("snapshot", StringComparison.OrdinalIgnoreCase))
    {
        return SnapshotRunner.Run(args);
    }

    if (args.Length > 0 && args[0].Equals("compare", StringComparison.OrdinalIgnoreCase))
    {
        return SnapshotRunner.Compare(args);
    }

    Log.Information("Start Cleaning");

    var cleaners = new List<ICleaner>
    {
        new RoslynCleaner(),
        new ReSharperCleaner()
    };

    foreach (var cleaner in cleaners)
    {
        cleaner.Clean();
    }

    Log.Information("All done!");
}
catch (Exception e)
{
    Log.Fatal(e, "Unhandled exception.");
    return -1;
}
finally
{
    Log.CloseAndFlush();
}
return 0;