namespace CleanMyCDrive;

public static class SizeFormatter
{
    public static readonly string[] UnitsShort = { "B", "kB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    public static string ToDecimalSizeString(this long size)
    {
        var negative = size < 0;
        if (negative) size = -size;
        var dSize = (decimal)size;
        var p = 0;
        while (dSize > 1000 && p < UnitsShort.Length)
        {
            dSize = Math.Round(dSize) / 1000;
            p++;
        }

        return (negative ? "-":"") + dSize.ToString("N") + " " + UnitsShort[p];
    }
}