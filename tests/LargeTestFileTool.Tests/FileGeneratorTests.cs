using System;
using System.IO;
using System.Text;
using LargeTestFileTool.Features.FileGeneration;
using Xunit;

namespace LargeTestFileTool.Tests;

public sealed class FileGeneratorTests
{
    [Fact]
    public void Generate_CreatesFileAndHasDuplicates()
    {
        string workDir = CreateTempDir();
        string output = Path.Combine(workDir, "gen.txt");

        // Small size to keep tests fast.
        const long targetBytes = 64 * 1024;

        var generator = new FileGenerator();
        generator.Generate(output, targetBytes);

        Assert.True(File.Exists(output));

        // If your generator guarantees exact size, keep this:
        // Assert.Equal(targetBytes, new FileInfo(output).Length);
        //
        // If it only guarantees >= target, use this instead:
        Assert.True(new FileInfo(output).Length >= targetBytes);

        // Check format and duplicates (by string part).
        var lines = File.ReadAllLines(output, new UTF8Encoding(false));
        Assert.NotEmpty(lines);

        var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        bool hasDuplicate = false;

        foreach (var line in lines)
        {
            int dot = line.IndexOf('.');
            Assert.True(dot > 0);

            // Verify number part is parseable.
            var numPart = line.Substring(0, dot);
            Assert.True(long.TryParse(numPart, out _));

            // Extract text part.
            int start = dot + 1;
            while (start < line.Length && line[start] == ' ')
                start++;

            string text = start < line.Length ? line[start..] : string.Empty;

            if (!seen.Add(text))
                hasDuplicate = true;
        }

        Assert.True(hasDuplicate);

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
