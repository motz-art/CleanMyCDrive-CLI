namespace Cleaners;

public class RoslynCleaner : CleanerBase
{
    /// <summary>
    /// This cleaner is based on https://docs.microsoft.com/en-us/answers/questions/483037/can-i-safely-delete-34appdatalocalmicrosoftvisuals.html
    /// It says:
    /// Q: Can I safely delete "AppData\Local\Microsoft\VisualStudio\Roslyn\Cache\"?
    /// 
    /// A: Deleting this cache folder will not affect Visual Studio, but it may affect the loading speed of .NET Compiler Platform (Roslyn) Analyzers.
    /// </summary>
    /// <returns></returns>
    public override IReadOnlyList<string> GetItemsToClean()
    {
        return new List<string> { @"%LocalAppData%\Microsoft\VisualStudio\Roslyn\Cache\*" };
    }
}