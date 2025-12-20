namespace DirectorySync.Engine;

/// <summary>
/// Centralized format strings and formatting utilities for consistent output across the application.
/// </summary>
/// <remarks>
/// Provides constants for duration, percentage, byte size, and hex formatting.
/// All members are static and thread-safe.
/// </remarks>
public static class FormatStrings
{
    /// <summary>
    /// Format string for durations without milliseconds (hh:mm:ss). Example: "01:23:45".
    /// </summary>
    public const string DurationFormat = @"hh\:mm\:ss";

    /// <summary>
    /// Format string for durations with milliseconds (hh:mm:ss.fff). Example: "01:23:45.678".
    /// </summary>
    public const string DurationFormatWithMilliseconds = @"hh\:mm\:ss\.fff";

    /// <summary>
    /// Format string for byte sizes with units. Placeholder 0 is the numeric value, 1 is the unit string.
    /// </summary>
    public const string ByteSizeFormat = "{0:0.##} {1}";

    /// <summary>
    /// Format string for percentages with one decimal place (e.g., "45.7%").
    /// </summary>
    public const string PercentageOneDecimal = "{0:F1}%";

    /// <summary>
    /// Format string for percentages with two decimal places (e.g., "45.73%").
    /// </summary>
    public const string PercentageTwoDecimals = "{0:F2}%";

    /// <summary>
    /// Format string for hexadecimal HRESULTs (e.g., "0x80070005").
    /// </summary>
    public const string HexFormat = "0x{0:X}";

    /// <summary>
    /// Format string for progress display: current/total (percentage%). Example: "50/100 (50.0%)".
    /// </summary>
    public const string ProgressFormat = "{0}/{1} ({2:F1}%)";

    /// <summary>
    /// Byte size unit suffixes in ascending order: B, KB, MB, GB, TB, PB.
    /// Used by <see cref="FormatBytes"/> for human-readable size formatting.
    /// </summary>
    public static readonly string[] ByteSizeUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    /// <summary>
    /// Formats a byte count into a human-readable string with appropriate units.
    /// </summary>
    /// <param name="bytes">The number of bytes to format. Can be any non-negative value; negative values are not validated.</param>
    /// <returns>
    /// A formatted string with up to 2 decimal places and a unit suffix (e.g., "1.50 GB", "512 B", "2.00 TB").
    /// Uses 1024-based units (binary, not decimal).
    /// </returns>
    /// <remarks>
    /// Conversion uses powers of 1024 (KiB, MiB, etc.) but labels as KB, MB for simplicity.
    /// The largest unit is PB (petabytes); larger values will display as PB.
    /// </remarks>
    public static string FormatBytes(long bytes)
    {
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < ByteSizeUnits.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return string.Format(ByteSizeFormat, len, ByteSizeUnits[order]);
    }
}
