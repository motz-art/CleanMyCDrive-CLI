using System.Text;
using System.Text.Json;
using CleanMyCDrive.Snapshots;
using Serilog;

namespace CleanMyCDrive;

public static class SnapshotRunner
{
    private const string SnapshotsPath = @"C:\AppData\CleanMyCDrive\";
    private const string SnapshotFileExtension = "ssf";

    public static int Run(string[] args)
    {
        Log.Information("Generating snapshot");

        var index = 1;
        var path = args.Length > index && !args[index].StartsWith("-") ? args[index] : @"C:\";
        if (path.Length == 1)
        {
            path += @":\";
        }

        var snapShot = SnapshotReporter.GenerateReport(path);

        string filePath;
        var resIndex = Array.IndexOf(args, "-r");
        if (resIndex >= 0 && args.Length > resIndex + 1)
        {
            filePath = args[resIndex + 1];
        }
        else
        {
            var time = DateTime.Now;
            filePath = Path.Combine(SnapshotsPath,
                $"{GetSafeFileName(path)}-snapshot-{time.ToString("yyyyMMdd-HHmm")}.{SnapshotFileExtension}");
        }

        snapShot.Write(filePath);
        
        return 0;
    }

    public static int Compare(string[] args)
    {
        if (args.Length < 3)
        {
            Log.Error("2 snapshot file names should be specified as arguments.");
            return -1;
        }

        if (!File.Exists(args[1]))
        {
            Log.Error("{file} does not exists.", args[1]);
            return -1;
        }

        if (!File.Exists(args[2]))
        {
            Log.Error("{file} does not exists.", args[2]);
            return -1;
        }

        var old = SnapshotAccessor.Read(args[1]);
        var current = SnapshotAccessor.Read(args[2]);

        var compareReport = SnapshotReporter.CompareAndReduce(old, current);
        
        var resultsFileName = "CompareResults.json";
        var resIndex = Array.IndexOf(args, "-r");
        
        if (resIndex >= 0 && args.Length > resIndex + 1)
        {
            resultsFileName = args[resIndex + 1];
        }

        WriteCompareReport(compareReport, resultsFileName);

        return 0;
    }

    private static void WriteCompareReport(SnapshotCompareReportNode compareReport, string reportPath)
    {
        using var fs = File.Create(reportPath);
        JsonSerializer.Serialize(fs, compareReport);
    }
    
    private static string GetSafeFileName(string str)
    {
        var sb = new StringBuilder(str.Length);
        
        var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
        var lastValid = false;
        
        foreach (var ch in str)
        {
            if (invalidChars.Contains(ch))
            {
                if (lastValid)
                {
                    sb.Append("-");
                }
                lastValid = false;
            }
            else
            {
                sb.Append(ch);
                lastValid = true;
            }
        }

        if (!lastValid && str.Length > 0)
        {
            sb.Remove(sb.Length - 1, 1);
        }

        return sb.ToString();
    }
}