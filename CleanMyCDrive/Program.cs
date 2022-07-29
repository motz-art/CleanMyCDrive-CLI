// See https://aka.ms/new-console-template for more information

using CleanMyCDrive.Cleaners;
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
}
finally
{
    Log.CloseAndFlush();
}