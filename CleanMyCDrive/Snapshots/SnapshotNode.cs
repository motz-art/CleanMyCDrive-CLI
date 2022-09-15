using System.Text;

namespace CleanMyCDrive.Snapshots;

public class SnapshotNode
{
    public bool IsDirectory { get; init; }
    public string Name { get; init; }
    public DateTime LastModifiedUtc { get; set; }
    public long TotalFilesCount { get; set; }
    public long TotalSize { get; set; }
    public IReadOnlyList<SnapshotNode>? SubItems { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (IsDirectory)
        {
            sb.Append("> ");
        }
        
        sb.Append(Name);

        sb.Append(" ");

        sb.Append(SizeString);

        if (IsDirectory)
        {
            sb.Append(" in ");
            sb.Append(TotalFilesCount.ToString("N0"));
            sb.Append(" files");
        }

        return sb.ToString();
    }

    public string SizeString => TotalSize.ToDecimalSizeString();
}