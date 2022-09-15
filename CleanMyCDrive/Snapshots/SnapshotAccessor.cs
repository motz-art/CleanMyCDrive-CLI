using System.Diagnostics;
using System.Text;
using Serilog;

namespace CleanMyCDrive.Snapshots;

public static class SnapshotAccessor
{
    private const byte FileHeader = 0b0100_0000;

    private const byte None = 0b0000_0000;
    private const byte IsDirectory = 0b1000_0000;
    private const byte HasLastModifiedUtc = 0b0100_0000;
    private const byte HasFileSize = 0b0100_0000;
    private const byte HasItemsCount = 0b0010_0000;
    private const byte ItemsCountHeaderByteMask = 0b0001_1111;

    private const byte FileSizeHeaderByteMask = 0b0011_1111;


    public static void Write(this SnapshotNode root, string filePath)
    {
        var directoryName = Path.GetDirectoryName(filePath);
        if (directoryName != null)
        {
            Directory.CreateDirectory(directoryName);
        }

        using var fs = File.Create(filePath);
        using var writer = new BinaryWriter(fs, Encoding.UTF8, true);

        writer.Write(FileHeader);

        var queue = new Queue<SnapshotNode>();
        queue.Enqueue(root);

        while (queue.TryDequeue(out var node))
        {
            if (node.IsDirectory)
            {
                if (node.SubItems == null) throw new InvalidOperationException("SubItems should not be null for Directory node.");

                var itemsCount = node.SubItems.Count;

                var sizeDiff = CalcSizeDiff(node);
                var cntDiff = CalcTotalFilesCountDiff(node);
                var lastModified = CalcLastModifiedDiff(node);

                var f = (byte) (IsDirectory | (itemsCount > ItemsCountHeaderByteMask ? HasItemsCount : None) | (itemsCount & ItemsCountHeaderByteMask) | (lastModified != 0 ? HasLastModifiedUtc : None));

                writer.Write(f);

                itemsCount >>= 5;
                if (itemsCount != 0)
                    Write7BitUInt((ulong) itemsCount, writer);

                if (lastModified != 0)
                {
                    Write7BitUInt(lastModified, writer);
                }

                writer.Write(node.Name);

                foreach (var subItem in node.SubItems)
                {
                    queue.Enqueue(subItem);
                }
            }
            else
            {
                var size = (ulong)node.TotalSize;

                var f = (byte) (size & FileSizeHeaderByteMask);
                size >>= 6;
                if (size > 0)
                {
                    f |= HasFileSize;
                }
                writer.Write(f);

                if (size != 0)
                    Write7BitUInt(size, writer);

                var time = (ulong)(node.LastModifiedUtc - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
                Write7BitUInt(time, writer);

                writer.Write(node.Name);
            }
        }
    }

    private static ulong CalcLastModifiedDiff(SnapshotNode node)
    {
        if (!node.IsDirectory) throw new InvalidOperationException("Node should be Directory.");
        if (node.SubItems == null) throw new NullReferenceException("node.SubItems should not be null.");
        
        var lastUpdate = node.SubItems.Count > 0 ? node.SubItems.Max(x => x.LastModifiedUtc) : DateTime.UnixEpoch;
        var diff = node.LastModifiedUtc.Ticks / TimeSpan.TicksPerSecond - lastUpdate.Ticks / TimeSpan.TicksPerSecond;

        Debug.Assert(diff >= 0);
        return (ulong)diff;
    }


    private static ulong CalcTotalFilesCountDiff(SnapshotNode node)
    {
        if (!node.IsDirectory) throw new InvalidOperationException("Node should be Directory.");
        if (node.SubItems == null) throw new NullReferenceException("node.SubItems should not be null.");

        var result = node.TotalFilesCount;

        foreach (var subItem in node.SubItems)
        {
            if (subItem.IsDirectory)
            {
                result -= subItem.TotalFilesCount;
            }
            else
            {
                result--;
            }
        }

        Debug.Assert(result >= 0);
        return (ulong)result;
    }

    private static ulong CalcSizeDiff(SnapshotNode node)
    {
        if (!node.IsDirectory) throw new InvalidOperationException("Node should be Directory.");
        if (node.SubItems == null) throw new NullReferenceException("node.SubItems should not be null.");

        var size = node.TotalSize - node.SubItems.Sum(x => x.TotalSize);

        Debug.Assert(size >= 0);
        return (ulong)size;
    }

    public static SnapshotNode Read(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var reader = new BinaryReader(fs, Encoding.UTF8, true);

        var fileHeader = reader.ReadByte();
        if (fileHeader != FileHeader) throw new NotSupportedException("Invalid file format!");

        var targetNodesQueue = new Queue<SnapshotNode>();
        
        var root = ReadNode(reader);
        targetNodesQueue.Enqueue(root);
        
        while (targetNodesQueue.TryDequeue(out var dirNode))
        {
            var subItems = (SnapshotNode[]) dirNode.SubItems!;
            
            for (int i = 0; i < subItems.Length; i++)
            {
                var subItem = ReadNode(reader);
                if (subItem.IsDirectory)
                {
                    targetNodesQueue.Enqueue(subItem);
                }

                subItems[i] = subItem;
            }
        }

        FixTotalValues(root);

        return root;
    }

    private static SnapshotNode ReadNode(BinaryReader reader)
    {
        var nodeHeader = reader.ReadByte();
        if ((nodeHeader & IsDirectory) != 0)
        {
            var hasItemsCount = (nodeHeader & HasItemsCount) != 0;
            var hasLastModifiedUtc = (nodeHeader & HasLastModifiedUtc) != 0;

            var count = nodeHeader & ItemsCountHeaderByteMask;
            if (hasItemsCount)
            {
                count += (int)(Read7BitEncodedUInt64(reader) << 5);
            }

            var lastModifiedUtc = new DateTime(0L, DateTimeKind.Utc);

            if (hasLastModifiedUtc)
            {
                var lastUpdatedOffset = Read7BitEncodedUInt64(reader);
                lastModifiedUtc = new DateTime((long)lastUpdatedOffset * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
            }

            var name = reader.ReadString();

            return new SnapshotNode
            {
                IsDirectory = true,
                SubItems = new SnapshotNode[count],
                LastModifiedUtc = lastModifiedUtc,
                Name = name,
            };
        }
        else
        {
            var size = (long)(nodeHeader & FileSizeHeaderByteMask);

            if ((nodeHeader & HasFileSize) != 0)
            {
                size |= (long)(Read7BitEncodedUInt64(reader) << 6);
            }

            var timeNum = Read7BitEncodedUInt64(reader);
            var lastModified = DateTime.UnixEpoch + TimeSpan.FromSeconds(timeNum);

            var name = reader.ReadString();

            return new SnapshotNode
            {
                IsDirectory = false,
                TotalSize = size,
                Name = name,
                LastModifiedUtc = lastModified,
                TotalFilesCount = 1
            };
        }
    }

    private static void FixTotalValues(SnapshotNode node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));
        Debug.Assert(node.IsDirectory);
        Debug.Assert(node.SubItems != null);

        foreach (var subItem in node.SubItems)
        {
            if (subItem.IsDirectory) FixTotalValues(subItem);
        }

        node.TotalSize = node.SubItems.Sum(x => x.TotalSize);
        node.TotalFilesCount = node.SubItems.Sum(x => x.IsDirectory ? x.TotalFilesCount : 1);
        var baseTime = node.SubItems.Count > 0 ? node.SubItems.Max(x => x.LastModifiedUtc) : DateTime.UnixEpoch;
        node.LastModifiedUtc = new DateTime(node.LastModifiedUtc.Ticks + baseTime.Ticks, DateTimeKind.Utc);
    }

    private static ulong Read7BitEncodedUInt64(BinaryReader reader)
    {
        var result = 0UL;
        byte b;
        int offset = 0;
        do
        {
            b = reader.ReadByte();

            result |= (ulong)(b & 0b0111_1111) << offset;
            offset += 7;
        } while ((b & 0b1000_0000) != 0);

        return result;
    }

    private static void Write7BitUInt(ulong value, BinaryWriter writer)
    {
        do
        {
            var b = (byte) (value & 0b0111_1111);
            value >>= 7;
            if (value > 0) b |= 0b1000_0000;

            writer.Write(b);
        } while (value > 0);
    }

    public static void Test()
    {
        for (int i = 0; i < 10; i++)
        {
            var snapShot = new SnapshotNode
            {
                IsDirectory = true,
                SubItems = new SnapshotNode[0],
                TotalFilesCount = i,
                Name = "Test"
            };

            snapShot.Write(@"C:\AppData\CleanMyCDrive\test.ssf");

            var sn = Read(@"C:\AppData\CleanMyCDrive\test.ssf");

            Debug.Assert(snapShot.IsDirectory == sn.IsDirectory);
            Debug.Assert(snapShot.Name == sn.Name);
            Debug.Assert(snapShot.TotalFilesCount == sn.TotalFilesCount);
        }
    }

    public static void CheckEqual(SnapshotNode one, SnapshotNode other, string prefix = "")
    {
        Debug.Assert(one.IsDirectory == other.IsDirectory);
        Debug.Assert(one.Name == other.Name);

        Debug.Assert(one.TotalFilesCount == other.TotalFilesCount);
        Debug.Assert(one.TotalSize == other.TotalSize);
        Debug.Assert(Math.Abs((one.LastModifiedUtc - other.LastModifiedUtc).Ticks) < TimeSpan.TicksPerSecond);

        if (Math.Abs((one.LastModifiedUtc - other.LastModifiedUtc).Ticks) >= TimeSpan.TicksPerSecond)
        {
            Log.Error("{prefix}, {name} {oneLastModifiedUtc} vs {otherLastModifiedUtc} diff {diff:0}", prefix, one.Name, one.LastModifiedUtc, other.LastModifiedUtc, (one.LastModifiedUtc - other.LastModifiedUtc).TotalMilliseconds);
        }

        if (one.IsDirectory)
        {
            Debug.Assert(one.SubItems != null);
            Debug.Assert(other.SubItems != null);

            Debug.Assert(one.SubItems.Count == other.SubItems.Count);

            for (int i = 0; i < one.SubItems.Count; i++)
            {
                var a = one.SubItems[i];
                Debug.Assert(a != null);
                
                var b = other.SubItems[i];
                Debug.Assert(b != null);

                CheckEqual(a, b, prefix + "\\" + one.Name);
            }
        }
    }
}