using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LargeTestFileTool.Features.FileSorting;
using Xunit;

namespace LargeTestFileTool.Tests;

public sealed class ExternalFileSorterTests
{
    [Fact]
    public async Task SortAsync_SortsByTextThenNumber()
    {
        string workDir = CreateTempDir();
        string input = Path.Combine(workDir, "input.txt");
        string output = Path.Combine(workDir, "output.txt");
        string temp = Path.Combine(workDir, ".tmp");

        Directory.CreateDirectory(temp);

        // Unsorted sample from the assignment.
        var lines = new[]
        {
            "415. Apple",
            "30432. Something something something",
            "1. Apple",
            "32. Cherry is the best",
            "2. Banana is yellow"
        };

        await File.WriteAllLinesAsync(input, lines, new UTF8Encoding(false));

        var sorter = new ExternalFileSorter();
        await sorter.SortAsync(input, output, temp);

        var sorted = await File.ReadAllLinesAsync(output, new UTF8Encoding(false));

        var expected = new[]
        {
            "1. Apple",
            "415. Apple",
            "2. Banana is yellow",
            "32. Cherry is the best",
            "30432. Something something something"
        };

        Assert.Equal(expected, sorted);

        TryDeleteDirectory(workDir);
    }

    [Fact]
    public async Task SortAsync_HandlesDuplicateTextAndOrdersByNumber()
    {
        string workDir = CreateTempDir();
        string input = Path.Combine(workDir, "input.txt");
        string output = Path.Combine(workDir, "output.txt");
        string temp = Path.Combine(workDir, ".tmp");

        Directory.CreateDirectory(temp);

        var lines = new[]
        {
            "10. Apple",
            "2. Apple",
            "7. Apple",
            "1. Banana",
            "3. Banana"
        };

        await File.WriteAllLinesAsync(input, lines, new UTF8Encoding(false));

        var sorter = new ExternalFileSorter();
        await sorter.SortAsync(input, output, temp);

        var sorted = await File.ReadAllLinesAsync(output, new UTF8Encoding(false));

        var expected = new[]
        {
            "2. Apple",
            "7. Apple",
            "10. Apple",
            "1. Banana",
            "3. Banana"
        };

        Assert.Equal(expected, sorted);

        TryDeleteDirectory(workDir);
    }

    [Fact]
    public async Task SortAsync_EmptyInput_ProducesEmptyOutput()
    {
        string workDir = CreateTempDir();
        string input = Path.Combine(workDir, "input.txt");
        string output = Path.Combine(workDir, "output.txt");
        string temp = Path.Combine(workDir, ".tmp");

        Directory.CreateDirectory(temp);

        await File.WriteAllTextAsync(input, string.Empty, new UTF8Encoding(false));

        var sorter = new ExternalFileSorter();
        await sorter.SortAsync(input, output, temp);

        var sorted = await File.ReadAllTextAsync(output, new UTF8Encoding(false));
        Assert.Equal(string.Empty, sorted);

        TryDeleteDirectory(workDir);
    }

    private static string CreateTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "LargeTestFileToolTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch { /* Best effort. */ }
    }
}
