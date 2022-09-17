using System.Text;

namespace CleanMyCDrive.Snapshots;

public class SnapshotCompareReportNode
{
    public SnapshotCompareReportNodeStatus Status { get; set; }
    public bool IsDirectory { get; init; }
    public string Name { get; init; }
    public DateTime LastModifiedUtc { get; set; }
    public long TotalFilesCount { get; set; }
    public long TotalFilesCountDiff { get; set; }
    public long TotalSize { get; set; }
    public long TotalSizeDiff { get; set; }
    public IReadOnlyList<SnapshotCompareReportNode>? SubItems { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        if (IsDirectory)
        {
            sb.Append("> ");
        }

        sb.Append(Name);

        sb.Append(" ");

        sb.Append(TotalSizeString);

        if (TotalSizeDiff != 0)
        {
            sb.Append(" (");
            sb.Append(TotalSizeDiffString);
            sb.Append(" )");
        }

        if (IsDirectory)
        {
            sb.Append(" in ");
            sb.Append(TotalFilesCount.ToString("N0"));

            if (TotalFilesCountDiff != 0)
            {
                sb.Append(" (");
                if (TotalFilesCountDiff > 0) sb.Append("+");
                sb.Append(TotalFilesCountDiff.ToString("N0"));
                sb.Append(") ");
            }

            sb.Append(" files");
        }

        return sb.ToString();
    }

    public string TotalSizeString => TotalSize.ToDecimalSizeString();
    public string TotalSizeDiffString => (TotalSizeDiff > 0 ? "+" : "") + TotalSizeDiff.ToDecimalSizeString();
}