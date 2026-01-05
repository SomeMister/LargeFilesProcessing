using System;
using System.IO;
using System.Threading.Tasks;
using LargeTestFileTool.Features.FileGeneration;
using LargeTestFileTool.Features.FileSorting;
using LargeTestFileTool.Infrastructure;

namespace LargeTestFileTool.Cli;

internal static class CommandDispatcher
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            string command = args[0].Trim().ToLowerInvariant();

            switch (command)
            {
                case "fg":
                    return RunFileGeneration(args);

                case "sf":
                    return await RunFileSortingAsync(args).ConfigureAwait(false);

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error:");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunFileGeneration(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Missing size argument for fg.");
            Console.Error.WriteLine("Example: fg 10GB");
            return 1;
        }

        long targetBytes = SizeParser.ParseToBytes(args[1]);

        string filesDir = ProjectPaths.GetFilesDir();
        Directory.CreateDirectory(filesDir);

        string outputPath = ProjectPaths.GetDefaultInputPath();

        var generator = new FileGenerator();
        generator.Generate(outputPath, targetBytes);

        Console.WriteLine($"Generated: {outputPath}");
        return 0;
    }

    private static async Task<int> RunFileSortingAsync(string[] args)
    {
        string filesDir = ProjectPaths.GetFilesDir();
        Directory.CreateDirectory(filesDir);

        string inputPath = args.Length >= 2 && !string.IsNullOrWhiteSpace(args[1])
            ? args[1]
            : ProjectPaths.GetDefaultInputPath();

        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        string outputPath = ProjectPaths.GetDefaultSortedOutputPath();
        string tempDir = ProjectPaths.GetTempDir();

        Directory.CreateDirectory(tempDir);

        var sorter = new ExternalFileSorter();
        await sorter.SortAsync(inputPath, outputPath, tempDir).ConfigureAwait(false);

        Console.WriteLine($"Sorted: {outputPath}");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- fg <size>            // Generates files/input_data.txt");
        Console.WriteLine("  dotnet run -- sf [inputPath]       // Sorts into files/sorted_output.txt");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- fg 2GB");
        Console.WriteLine("  dotnet run -- sf");
        Console.WriteLine("  dotnet run -- sf C:\\data\\big.txt");
    }
}
