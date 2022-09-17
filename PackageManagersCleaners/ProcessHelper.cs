using System.Diagnostics;
using Serilog;

namespace PackageManagersCleaners;

public class ProcessHelper
{
    public static void Run(string fileName, string arguments)
    {
        using var process = new Process();
            
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
            
        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += HandleOutput;
        process.StartInfo.RedirectStandardError = true;
        process.ErrorDataReceived += HandleError;

        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;

        Log.Information("Starting {fileName} {arguments}", fileName, arguments);
        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        var code = process.ExitCode;
        if (code == 0)
        {
            Log.Information("Process exited with code: {code}", code);
        }
        else
        {
            Log.Error("Process exited with code indicating error: {code}", code);
        }
    }

    private static void HandleError(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
            Log.Error("Error: {data}", e.Data);
        else
            Log.Debug("Errors reading finished.");
    }

    private static void HandleOutput(object sender, DataReceivedEventArgs e)
    {
        if (e.Data != null)
            Log.Information("Output: {data}", e.Data);
        else
            Log.Debug("Output reading finished.");
    }

    public static IReadOnlyList<string> ReadAllLines(string fileName, string arguments)
    {
        var result = new List<string>();
        using var process = new Process();

        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;

        process.StartInfo.RedirectStandardOutput = true;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                result.Add(e.Data);
                Log.Debug("Output: {data}", e.Data);
            }
            else
            {
                Log.Debug("Output read finished.");
            }
        };
        process.StartInfo.RedirectStandardError = true;
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                result.Add(e.Data);
                Log.Error("Error: {data}", e.Data);
            }
            else
            {
                Log.Debug("Error reads finished.");
            }
        };

        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;

        Log.Information("Starting {fileName} {arguments}", fileName, arguments);
        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();

        var code = process.ExitCode;
        if (code == 0)
        {
            Log.Information("Process exited with code: {code}", code);
        }
        else
        {
            Log.Error("Process exited with code indicating error: {code}", code);
        }

        return result;
    }
}