using System.Security;
using Serilog;
using Serilog.Events;

namespace CleanMyCDrive.Cleaners;

public abstract class CleanerBase : ICleaner
{
    /// <summary>
    /// This cleaner is based on https://docs.microsoft.com/en-us/answers/questions/483037/can-i-safely-delete-34appdatalocalmicrosoftvisuals.html
    /// It says:
    /// Q: Can I safely delete "AppData\Local\Microsoft\VisualStudio\Roslyn\Cache\"?
    /// 
    /// A: Deleting this cache folder will not affect Visual Studio, but it may affect the loading speed of .NET Compiler Platform (Roslyn) Analyzers.
    /// </summary>
    /// <returns></returns>
    public abstract IReadOnlyList<string> GetItemsToClean();

    public void Clean()
    {
        var items = GetItemsToClean();
        foreach (var item in items)
        {
            var path = Environment.ExpandEnvironmentVariables(item);

            if (path.EndsWith("*") || (File.GetAttributes(path) & FileAttributes.Directory) != 0)
            {
                CleanDirectory(path);
            }
        }
    }

    private void CleanDirectory(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));

        try
        {
            bool shouldRemoveRoot = true;

            if (path.EndsWith('*'))
            {
                path = path.Substring(0, path.Length - 1);

                shouldRemoveRoot = false;
            }

            var directory = new DirectoryInfo(path);
            var removed = CleanDirectoryContent(directory);
            
            if (removed)
            {
                if (shouldRemoveRoot)
                {
                    TryDeleteEmptyDirectory(path);
                }
            }

            else
            {
                Log.Write(shouldRemoveRoot ? LogEventLevel.Information : LogEventLevel.Warning, "{path} already removed.", path);
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Unhandled exception while cleaning ");
        }
    }

    private bool CleanDirectoryContent(DirectoryInfo directory)
    {
        if (!directory.Exists) return false;

        var allDone = true;
        var filesSizeCleaned = 0L;
        var filesCount = 0L;
        var dirCount = 0L;
        
        Log.Verbose("Cleaning content of {path}.", directory.Name);
        foreach (var subDirectory in directory.EnumerateDirectories())
        {
            var path = subDirectory.FullName;

            if (CleanDirectoryContent(subDirectory))
            {

                try
                {
                    subDirectory.Delete();
                    Log.Debug("Directory {path} removed.", path);
                    dirCount++;
                }
                catch (IOException e)
                {
                    Log.Error(e, "Can't remove {path}", path);
                    allDone = false;
                }
                catch (SecurityException e)
                {
                    Log.Error(e, "No permissions to delete {path}", path);
                    allDone = false;
                }
            }
            else
            {
                Log.Warning("Can't clean {path} as not all files were removed.", path);
            }
        }

        foreach (var fileInfo in directory.EnumerateFiles())
        {
            var size = fileInfo.Length;
            var attrs = fileInfo.Attributes;
            var path = fileInfo.Name;

            try
            {
                fileInfo.Delete();
                Log.Debug("File {path}, {attrs}, {size} removed.", path, attrs, size);
                filesSizeCleaned += size;
                filesCount++;
            }
            catch (SecurityException e)
            {
                Log.Error(e, "No permissions to delete {file}.", path);
                allDone = false;
            }
            catch (UnauthorizedAccessException e)
            {
                Log.Fatal(e, "Is {file} directory?", path);
                allDone = false;
            }
            catch (IOException e)
            {
                Log.Fatal(e, "Can't remove {path}.", path);
                allDone = false;
            }
        }

        Log.Verbose("Successfully cleaned {path}. Removed {dirCount} directories. Removed {fileCount} files with total size {fileSize} bytes.", directory.Name, dirCount, filesCount, filesSizeCleaned);
        return allDone;
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            Directory.Delete(path);
        }
        catch (DirectoryNotFoundException e)
        {
            Log.Warning(e, "Directory does not exists: {path}.", path);
        }
        catch (UnauthorizedAccessException e)
        {
            Log.Error(e, "No permission to delete: {path}.", path);
        }
        catch (IOException e)
        {
            Log.Error(e, "Can't delete: {path}", path);
        }
    }
}