using System.Security;
using Serilog;

namespace CleanMyCDrive.Snapshots;

public class SnapshotReporter
{
    private long emptyFileCount = 0L;
    private long dirScanErrors = 0L;

    private SnapshotReporter()
    {

    }

    public static SnapshotNode GenerateReport(string path)
    {
        var snapshotReport = new SnapshotReporter();
        var node = snapshotReport.CreateSnapshot(new DirectoryInfo(path));
        return node;
    }

    private SnapshotNode CreateSnapshot(DirectoryInfo directory)
    {
        var subItems = new List<SnapshotNode>();
        var totalSize = 0L;
        var totalFiles = 0L;
        var lastModified = directory.LastWriteTimeUtc;

        try
        {
            foreach (var subDirectory in directory.EnumerateDirectories())
            {
                var subNode = CreateSnapshot(subDirectory);

                totalSize += subNode.TotalSize;
                totalFiles += subNode.TotalFilesCount;

                if (lastModified < subNode.LastModifiedUtc)
                {
                    lastModified = subNode.LastModifiedUtc;
                }

                subItems.Add(subNode);
            }
        }
        catch (DirectoryNotFoundException e)
        {
            HandleSubDirectoryScanException(directory, e);
        }
        catch (UnauthorizedAccessException e)
        {
            HandleSubDirectoryScanException(directory, e);
        }
        catch (SecurityException e)
        {
            HandleSubDirectoryScanException(directory, e);
        }

        try
        {
            foreach (var fileInfo in directory.EnumerateFiles())
            {
                var subNode = new SnapshotNode
                {
                    IsDirectory = false,
                    Name = fileInfo.Name,
                    LastModifiedUtc = fileInfo.LastWriteTimeUtc,
                    TotalFilesCount = 1,
                    TotalSize = fileInfo.Length,
                };

                if (fileInfo.Length == 0)
                {
                    emptyFileCount++;
                }

                if (lastModified < subNode.LastModifiedUtc)
                {
                    lastModified = subNode.LastModifiedUtc;
                }

                totalFiles++;
                totalSize += subNode.TotalSize;

                subItems.Add(subNode);
            }
        }
        catch (DirectoryNotFoundException e)
        {
            HandleFilesScanException(directory, e);
        }
        catch (UnauthorizedAccessException e)
        {
            HandleFilesScanException(directory, e);
        }
        catch (SecurityException e)
        {
            HandleFilesScanException(directory, e);
        }

        subItems.Sort((a, b) =>
        {
            return (b.TotalSize - a.TotalSize) switch
            {
                > 0 => 1,
                < 0 => -1,
                _ => 0
            };
        });
        
        return new SnapshotNode
        {
            IsDirectory = true,
            Name = directory.Name,
            LastModifiedUtc = lastModified,
            TotalSize = totalSize,
            TotalFilesCount = totalFiles,
            SubItems = subItems.ToArray()
        };
    }

    private void HandleSubDirectoryScanException(DirectoryInfo directory, Exception e)
    {
        Log.Error("Can't scan sub directories of {path}. {msg}", directory.FullName, e.Message);
        dirScanErrors++;
    }

    private void HandleFilesScanException(DirectoryInfo directory, Exception e)
    {
        Log.Error("Can't scan files in {path} directory. {msg}", directory.FullName, e.Message);
        dirScanErrors++;
    }

    public static SnapshotCompareReportNode CompareAndReduce(SnapshotNode? old, SnapshotNode? current)
    {
        var result = Compare(old, current);

        return Reduce(result);
    }

    private static SnapshotCompareReportNode Reduce(SnapshotCompareReportNode node)
    {
        if (!node.IsDirectory || node.SubItems == null)
        {
            return node;
        }

        var hasChanges = false;

        var subItems = node.SubItems.ToList();
        for (int i = subItems.Count - 1; i >= 0; i--)
        {
            var item = subItems[i];
            if (item.IsDirectory)
            {
                var nItem = Reduce(item);
                if (nItem.Status == SnapshotCompareReportNodeStatus.Unchanged)
                {
                    subItems.RemoveAt(i);
                }
                else
                {
                    subItems[i] = nItem;
                    hasChanges = true;
                }
            }
            else
            {
                if (item.TotalSizeDiff == 0)
                {
                    subItems.RemoveAt(i);
                }
                else
                {
                    hasChanges = true;
                }
            }
        }

        var status = node.Status;

        if (hasChanges)
        {
            if (status == SnapshotCompareReportNodeStatus.Unchanged)
            {
                status = SnapshotCompareReportNodeStatus.Changed;
            }
        }
        else
        {
            status = SnapshotCompareReportNodeStatus.Unchanged;
        }

        return new SnapshotCompareReportNode
        {
            Status = status,
            IsDirectory = true,
            SubItems = subItems,
            Name = node.Name,
            LastModifiedUtc = node.LastModifiedUtc,
            TotalSize = node.TotalSize,
            TotalSizeDiff = node.TotalSizeDiff,
            TotalFilesCount = node.TotalFilesCount,
            TotalAddFilesCount = node.TotalAddFilesCount,
            TotalRemovedFilesCount = node.TotalRemovedFilesCount
        };
    }


    public static SnapshotCompareReportNode Compare(SnapshotNode? old, SnapshotNode? current)
    {
        if (old == null && current == null) throw new ArgumentException($"Both {nameof(old)} and {nameof(current)} should not be null.");

        var existing = current ?? old!;

        if (old == null)
        {
            return new SnapshotCompareReportNode
            {
                Status = SnapshotCompareReportNodeStatus.New,
                IsDirectory = existing.IsDirectory,
                LastModifiedUtc = existing.LastModifiedUtc,
                Name = existing.Name,
                TotalFilesCount = existing.TotalFilesCount,
                TotalAddFilesCount = existing.TotalFilesCount,
                TotalRemovedFilesCount = 0,
                TotalSize = existing.TotalSize,
                TotalSizeDiff = existing.TotalSize,
                SubItems = existing.SubItems?.Select(x => Compare(null, x)).ToList(),
            };
        }

        if (current == null)
        {
            return new SnapshotCompareReportNode
            {
                Status = SnapshotCompareReportNodeStatus.Removed,
                IsDirectory = existing.IsDirectory,
                LastModifiedUtc = existing.LastModifiedUtc,
                Name = existing.Name,
                TotalFilesCount = existing.TotalFilesCount,
                TotalAddFilesCount = 0,
                TotalRemovedFilesCount = existing.TotalFilesCount,
                TotalSize = 0,
                TotalSizeDiff = -existing.TotalSize,
                SubItems = existing.SubItems?.Select(x => Compare(x, null)).ToList(),
            };
        }

        if (old.IsDirectory != current.IsDirectory)
            throw new InvalidOperationException("Can't compare Directory and File.");

        if (old.IsDirectory)
        {
            if (old.SubItems == null)
                throw new ArgumentException($"Old node {nameof(old.SubItems)} should not be null.");

            if (current.SubItems == null)
                throw new ArgumentException($"Current node {nameof(current.SubItems)} should not be null.");
        }

        var subItems = old.SubItems != null && current.SubItems != null
            ? CompareItems(old.SubItems, current.SubItems)
            : null;

        var addFilesCount = subItems?.Sum(x => x.TotalAddFilesCount) ?? current.SubItems?.Sum(x => x.TotalFilesCount) ?? 0;
        var removedFilesCount = subItems?.Sum(x => x.TotalRemovedFilesCount) ?? old.SubItems?.Sum(x => x.TotalFilesCount) ?? 0;
        
        return new SnapshotCompareReportNode
        {
            Status = old.LastModifiedUtc == current.LastModifiedUtc
                ? SnapshotCompareReportNodeStatus.Changed
                : SnapshotCompareReportNodeStatus.Unchanged,
            IsDirectory = existing.IsDirectory,
            LastModifiedUtc = current.LastModifiedUtc,
            Name = current.Name,
            TotalFilesCount = existing.TotalFilesCount,
            TotalAddFilesCount = addFilesCount,
            TotalRemovedFilesCount = removedFilesCount,
            TotalSize = existing.TotalSize,
            TotalSizeDiff = current.TotalSize-old.TotalSize,
            SubItems = subItems,
        };
    }

    private static IReadOnlyList<SnapshotCompareReportNode> CompareItems(IReadOnlyList<SnapshotNode> old, IReadOnlyList<SnapshotNode> current)
    {
        var oldByName = old.ToDictionary(x => x.Name);

        var mappedOldItems = new HashSet<SnapshotNode>();

        var pairs = new List<(SnapshotNode? old, SnapshotNode? current)>(Math.Max(current.Count, old.Count));

        foreach (var cNode in current)
        {
            if (oldByName.TryGetValue(cNode.Name, out var oNode))
            {
                pairs.Add((oNode, cNode));
                mappedOldItems.Add(oNode);
            }
            else
            {
                pairs.Add((null, cNode));
            }
        }

        foreach (var oNode in old)
        {
            if (!mappedOldItems.Contains(oNode))
            {
                pairs.Add((oNode, null));
            }
        }

        return pairs.Select(x => Compare(x.old, x.current)).ToList();
    }
}