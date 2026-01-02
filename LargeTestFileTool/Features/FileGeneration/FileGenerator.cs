using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace LargeTestFileTool.Features.FileGeneration;

internal sealed class FileGenerator
{
    private const int WriterBufferSize = 1024 * 1024; // 1 MB
    private const int FlushEveryLines = 50_000;

    // Use '\n' to make size deterministic across OSes.
    private const string NewLine = "\n";

    // Smaller range to avoid huge numbers like 467200176.
    private const long MinNumberInclusive = 1;
    private const long MaxNumberExclusive = 100_000;

    // Base phrases to guarantee duplicates.
    private static readonly string[] BasePhrases =
    {
        "Apple",
        "Banana is yellow",
        "Cherry is the best",
        "Something something something",
        "Memory management is important",
        "Performance optimization is key",
        "Multithreading and concurrency",
        "This is a long test string to increase file size",
        "Repeated string for sorting test",
        "Another repeated string",
        "Senior Developer Test"
    };

    public void Generate(string outputPath, long targetBytes)
    {
        if (targetBytes < 5)
            throw new ArgumentException("Target size is too small. Minimum is 5 bytes.", nameof(targetBytes));

        var rng = new Random();
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

        using var fs = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: WriterBufferSize,
            options: FileOptions.SequentialScan);

        using var writer = new StreamWriter(fs, encoding, bufferSize: WriterBufferSize)
        {
            NewLine = NewLine
        };

        long logicalBytesWritten = 0;
        long linesWritten = 0;

        var sw = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;

        while (logicalBytesWritten < targetBytes)
        {
            for (int i = 0; i < FlushEveryLines && logicalBytesWritten < targetBytes; i++)
            {
                long remaining = targetBytes - logicalBytesWritten;

                // If remaining bytes are too small to safely write a normal line, write a final exact-fill line.
                if (remaining <= 64)
                {
                    WriteExactFinalLine(writer, remaining);
                    logicalBytesWritten = targetBytes;
                    linesWritten++;
                    break;
                }

                long number = rng.NextInt64(MinNumberInclusive, MaxNumberExclusive);
                string text = BasePhrases[rng.Next(BasePhrases.Length)];

                // Exact byte count for the line in UTF-8:
                // "<number>. <text>\n"
                int digits = DigitCount(number);
                int textBytes = encoding.GetByteCount(text);
                long lineBytes = digits + 2 + textBytes + 1; // ". " => 2, "\n" => 1

                long remainderAfter = remaining - lineBytes;

                // Avoid overshooting and avoid leaving an impossible remainder (1..3 bytes).
                if (lineBytes > remaining || remainderAfter is 1 or 2 or 3)
                {
                    WriteExactFinalLine(writer, remaining);
                    logicalBytesWritten = targetBytes;
                    linesWritten++;
                    break;
                }

                writer.Write(number);
                writer.Write(". ");
                writer.WriteLine(text);

                logicalBytesWritten += lineBytes;
                linesWritten++;
            }

            writer.Flush();

            var elapsed = sw.Elapsed;
            if (elapsed - lastReport >= TimeSpan.FromSeconds(2))
            {
                lastReport = elapsed;
                double mbps = logicalBytesWritten / 1024d / 1024d / Math.Max(0.001, elapsed.TotalSeconds);
                Console.WriteLine($"Written: {logicalBytesWritten:N0} bytes | Lines: {linesWritten:N0} | Speed: {mbps:F1} MB/s | Elapsed: {elapsed:hh\\:mm\\:ss}");
            }
        }
    }

    private static void WriteExactFinalLine(StreamWriter writer, long remainingBytes)
    {
        // We craft a valid last line that exactly consumes remainingBytes.
        // Preferred: "1. " + payload + "\n"  (overhead = 4 bytes: '1' + ". " + '\n')
        // Fallback: if remainingBytes == 4 => "1. A" (no newline).

        if (remainingBytes < 4)
            throw new InvalidOperationException($"Cannot fill the last {remainingBytes} bytes with a valid line.");

        const long number = 1;

        if (remainingBytes == 4)
        {
            writer.Write(number);
            writer.Write(". ");
            writer.Write('A');
            return;
        }

        long payloadLen = remainingBytes - 4; // digits(1)=1, ". "=2, "\n"=1
        if (payloadLen < 1)
            throw new InvalidOperationException("Invalid final payload length.");

        writer.Write(number);
        writer.Write(". ");

        writer.Write('A');
        for (long i = 1; i < payloadLen; i++)
            writer.Write(' ');

        writer.Write(NewLine);
    }

    private static int DigitCount(long value)
    {
        return value switch
        {
            < 10 => 1,
            < 100 => 2,
            < 1_000 => 3,
            < 10_000 => 4,
            < 100_000 => 5,
            < 1_000_000 => 6,
            < 10_000_000 => 7,
            < 100_000_000 => 8,
            < 1_000_000_000 => 9,
            _ => 10
        };
    }
}
