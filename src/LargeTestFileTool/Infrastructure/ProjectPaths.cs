using System.IO;

namespace LargeTestFileTool.Infrastructure;

internal static class ProjectPaths
{
    public static string GetFilesDir()
        => Path.Combine(Directory.GetCurrentDirectory(), "files");

    public static string GetDefaultInputPath()
        => Path.Combine(GetFilesDir(), "input_data.txt");

    public static string GetDefaultSortedOutputPath()
        => Path.Combine(GetFilesDir(), "sorted_output.txt");

    public static string GetTempDir()
        => Path.Combine(GetFilesDir(), ".tmp_sort");
}
