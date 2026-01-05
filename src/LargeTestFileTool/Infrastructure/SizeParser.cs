using System.Globalization;

namespace LargeTestFileTool.Infrastructure;

internal static class SizeParser
{
    public static long ParseToBytes(string size)
    {
        if (string.IsNullOrWhiteSpace(size))
            throw new ArgumentException("Size is empty.");

        size = size.Trim();

        // Allow raw bytes: "123456"
        if (long.TryParse(size, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawBytes))
        {
            if (rawBytes <= 0) throw new ArgumentException("Size must be > 0.");
            return rawBytes;
        }

        // Allow suffixes: KB, MB, GB, TB (case-insensitive), optionally with spaces: "10 GB"
        size = size.Replace(" ", "", StringComparison.Ordinal);

        long multiplier;
        string numberPart;

        if (size.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L;
            numberPart = size[..^2];
        }
        else if (size.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L;
            numberPart = size[..^2];
        }
        else if (size.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L * 1024L;
            numberPart = size[..^2];
        }
        else if (size.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1024L * 1024L * 1024L * 1024L;
            numberPart = size[..^2];
        }
        else
        {
            throw new ArgumentException("Invalid size. Examples: 500MB, 10GB, 123456");
        }

        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n <= 0)
            throw new ArgumentException($"Invalid size value: {size}");

        double bytes = n * multiplier;
        if (bytes > long.MaxValue) throw new ArgumentException("Size is too large.");

        return (long)bytes;
    }
}
