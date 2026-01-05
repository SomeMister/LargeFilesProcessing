using System.Globalization;
using LargeTestFileTool.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        return await CommandDispatcher.RunAsync(args).ConfigureAwait(false);
    }
}
