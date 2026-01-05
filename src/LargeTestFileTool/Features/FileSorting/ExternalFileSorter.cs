using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace LargeTestFileTool.Features.FileSorting;

internal sealed class ExternalFileSorter
{
    // Constants kept internal (assignment does not require exposing tuning knobs).
    private const int ChunkSizeMb = 1024;
    private const int MaxOpenFiles = 128;
    private const int MaxParallelism = 4;

    // Use '\n' for deterministic output across OSes.
    private const string NewLine = "\n";

    // Log time period.
    private static readonly TimeSpan LogEvery = TimeSpan.FromSeconds(2);

    public async Task SortAsync(string inputPath, string outputPath, string tempDir)
    {
        var totalSw = Stopwatch.StartNew();

        Console.WriteLine($"[sf] Input : {inputPath}");
        Console.WriteLine($"[sf] Output: {outputPath}");
        Console.WriteLine($"[sf] Temp  : {tempDir}");
        Console.WriteLine($"[sf] Chunk : {ChunkSizeMb} MB | Parallel: {MaxParallelism} | MaxOpen: {MaxOpenFiles}");
        Console.WriteLine();

        var chunkSw = Stopwatch.StartNew();
        var tempFiles = await CreateSortedChunksAsync(inputPath, tempDir).ConfigureAwait(false);
        Console.WriteLine($"[sf] Chunking done: {tempFiles.Count} chunk(s) | {chunkSw.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine();

        if (tempFiles.Count == 0)
        {
            File.WriteAllText(outputPath, string.Empty, new UTF8Encoding(false));
            TryCleanupEmptyDir(tempDir);
            Console.WriteLine("[sf] Done (empty input).");
            return;
        }

        var mergeSw = Stopwatch.StartNew();
        string finalFile = await MultiPassMergeAsync(tempFiles, tempDir, outputPath).ConfigureAwait(false);
        Console.WriteLine($"[sf] Merge done | {mergeSw.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine();

        if (!string.Equals(finalFile, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            File.Move(finalFile, outputPath);
        }

        TryCleanupEmptyDir(tempDir);

        Console.WriteLine($"[sf] Total time: {totalSw.Elapsed:hh\\:mm\\:ss}");
    }

    private static async Task<List<string>> CreateSortedChunksAsync(string inputPath, string tempDir)
    {
        long chunkBytesTarget = (long)ChunkSizeMb * 1024 * 1024;

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var fs = new FileStream(
            inputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan);

        using var reader = new StreamReader(
            fs,
            encoding,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024 * 1024);

        var tasks = new List<Task<string>>();
        var semaphore = new SemaphoreSlim(MaxParallelism);

        long totalLines = 0;
        int chunkIndex = 0;

        var sw = Stopwatch.StartNew();
        var lastLog = TimeSpan.Zero;

        Console.WriteLine("[sf] Chunking started...");

        while (true)
        {
            var records = new List<Record>(capacity: 200_000);
            long approxChunkBytes = 0;

            while (approxChunkBytes < chunkBytesTarget)
            {
                string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                    break;

                if (!TryParseLine(line, out long number, out string text))
                    throw new FormatException($"Invalid line format near input line {totalLines + 1}.");

                records.Add(new Record(number, text));

                // Approximate only; this is chunk sizing, not memory accounting.
                approxChunkBytes += encoding.GetByteCount(text) + 32;
                totalLines++;

                var elapsed = sw.Elapsed;
                if (elapsed - lastLog >= LogEvery)
                {
                    lastLog = elapsed;

                    // fs.Position is approximate due to StreamReader buffering, but good enough for progress logs.
                    long readBytesApprox = fs.Position;
                    double mbps = readBytesApprox / 1024d / 1024d / Math.Max(0.001, elapsed.TotalSeconds);

                    Console.WriteLine(
                        $"[sf] Chunking: read ~{FormatBytes(readBytesApprox)} | lines {totalLines:N0} | chunks queued {chunkIndex:N0} | ~{mbps:F1} MB/s");
                }
            }

            if (records.Count == 0)
                break;

            await semaphore.WaitAsync().ConfigureAwait(false);
            int localIndex = chunkIndex++;

            tasks.Add(Task.Run(() =>
            {
                try
                {
                    string chunkPath = Path.Combine(tempDir, $"chunk_{localIndex:D6}.txt");
                    WriteSortedChunk(records, chunkPath);
                    return chunkPath;
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        // Wait for all chunk writers.
        var chunkFiles = new List<string>(await Task.WhenAll(tasks).ConfigureAwait(false));

        Console.WriteLine($"[sf] Chunking: completed | lines {totalLines:N0} | chunks {chunkFiles.Count:N0} | time {sw.Elapsed:hh\\:mm\\:ss}");
        return chunkFiles;
    }

    private static void WriteSortedChunk(List<Record> records, string path)
    {
        records.Sort(RecordComparer.Instance);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var fs = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            options: FileOptions.SequentialScan);

        using var writer = new StreamWriter(fs, encoding, bufferSize: 1024 * 1024)
        {
            NewLine = NewLine
        };

        foreach (var r in records)
        {
            writer.Write(r.Number);
            writer.Write(". ");
            writer.WriteLine(r.Text);
        }
    }

    private static async Task<string> MultiPassMergeAsync(List<string> files, string tempDir, string outputPath)
    {
        Console.WriteLine("[sf] Merge started...");

        int pass = 0;

        while (files.Count > 1)
        {
            pass++;
            Console.WriteLine($"[sf] Merge pass {pass}: {files.Count} file(s)");

            var nextFiles = new List<string>();
            int batchIndex = 0;
            int totalBatches = (files.Count + MaxOpenFiles - 1) / MaxOpenFiles;

            for (int i = 0; i < files.Count; i += MaxOpenFiles)
            {
                var batch = files.GetRange(i, Math.Min(MaxOpenFiles, files.Count - i));

                bool isFinalSingleBatch = (batch.Count == files.Count) && (batch.Count <= MaxOpenFiles);
                string mergedPath = isFinalSingleBatch
                    ? outputPath
                    : Path.Combine(tempDir, $"merge_p{pass:D2}_{batchIndex:D4}.txt");

                Console.WriteLine($"[sf]  pass {pass} | batch {batchIndex + 1}/{totalBatches} | merging {batch.Count} file(s) -> {Path.GetFileName(mergedPath)}");

                await MergeFilesAsync(batch, mergedPath, pass, batchIndex + 1, totalBatches).ConfigureAwait(false);

                foreach (var f in batch)
                    SafeDelete(f); // Best effort.

                nextFiles.Add(mergedPath);
                batchIndex++;
            }

            files = nextFiles;
        }

        return files[0];
    }

    private static async Task MergeFilesAsync(List<string> inputFiles, string outputFile, int pass, int batch, int totalBatches)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var readers = new StreamReader[inputFiles.Count];

        var sw = Stopwatch.StartNew();
        var lastLog = TimeSpan.Zero;

        long outLines = 0;
        long outBytesApprox = 0;

        try
        {
            for (int i = 0; i < inputFiles.Count; i++)
            {
                var fs = new FileStream(
                    inputFiles[i],
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    options: FileOptions.SequentialScan);

                readers[i] = new StreamReader(fs, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 1024);
            }

            using var outFs = new FileStream(
                outputFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 1024 * 1024,
                options: FileOptions.SequentialScan);

            using var writer = new StreamWriter(outFs, encoding, bufferSize: 1024 * 1024)
            {
                NewLine = NewLine
            };

            var pq = new PriorityQueue<MergeItem, MergeKey>(MergeKeyComparer.Instance);

            for (int i = 0; i < readers.Length; i++)
            {
                string? line = await readers[i].ReadLineAsync().ConfigureAwait(false);
                if (line is null) continue;

                if (!TryParseLine(line, out long number, out string text))
                    throw new FormatException($"Invalid line in chunk file: {inputFiles[i]}");

                pq.Enqueue(new MergeItem(i, number, text), new MergeKey(text, number));
            }

            while (pq.Count > 0)
            {
                pq.TryDequeue(out var item, out _);

                writer.Write(item.Number);
                writer.Write(". ");
                writer.WriteLine(item.Text);

                outLines++;

                // Approximate output bytes for logging (no need to be exact).
                outBytesApprox += item.Text.Length + 32;

                var elapsed = sw.Elapsed;
                if (elapsed - lastLog >= LogEvery)
                {
                    lastLog = elapsed;
                    double lps = outLines / Math.Max(0.001, elapsed.TotalSeconds);
                    double mbps = outBytesApprox / 1024d / 1024d / Math.Max(0.001, elapsed.TotalSeconds);

                    Console.WriteLine($"[sf]   pass {pass} | batch {batch}/{totalBatches} | merged lines {outLines:N0} | ~{lps:F0} lines/s | ~{mbps:F1} MB/s");
                }

                string? nextLine = await readers[item.SourceIndex].ReadLineAsync().ConfigureAwait(false);
                if (nextLine is null) continue;

                if (!TryParseLine(nextLine, out long n2, out string t2))
                    throw new FormatException($"Invalid line in chunk file: {inputFiles[item.SourceIndex]}");

                pq.Enqueue(new MergeItem(item.SourceIndex, n2, t2), new MergeKey(t2, n2));
            }
        }
        finally
        {
            for (int i = 0; i < readers.Length; i++)
                readers[i]?.Dispose();
        }
    }

    private static bool TryParseLine(string line, out long number, out string text)
    {
        // Expected format: "<Number>. <String>"
        int dot = line.IndexOf('.');
        if (dot <= 0)
        {
            number = 0;
            text = string.Empty;
            return false;
        }

        if (!long.TryParse(line.AsSpan(0, dot), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            text = string.Empty;
            return false;
        }

        int start = dot + 1;
        while (start < line.Length && line[start] == ' ')
            start++;

        text = start < line.Length ? line[start..] : string.Empty;
        return true;
    }

    private static void SafeDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* Best effort. */ }
    }

    private static void TryCleanupEmptyDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any() == false)
                Directory.Delete(dir);
        }
        catch
        {
            // Best effort.
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double K = 1024.0;
        const double M = K * 1024.0;
        const double G = M * 1024.0;
        const double T = G * 1024.0;

        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < (long)M => $"{bytes / K:F2} KB",
            < (long)G => $"{bytes / M:F2} MB",
            < (long)T => $"{bytes / G:F2} GB",
            _ => $"{bytes / T:F2} TB"
        };
    }

    private readonly record struct Record(long Number, string Text);

    private sealed class RecordComparer : IComparer<Record>
    {
        public static readonly RecordComparer Instance = new();

        public int Compare(Record x, Record y)
        {
            int c = StringComparer.Ordinal.Compare(x.Text, y.Text);
            if (c != 0) return c;
            return x.Number.CompareTo(y.Number);
        }
    }

    private readonly record struct MergeItem(int SourceIndex, long Number, string Text);

    private readonly record struct MergeKey(string Text, long Number);

    private sealed class MergeKeyComparer : IComparer<MergeKey>
    {
        public static readonly MergeKeyComparer Instance = new();

        public int Compare(MergeKey x, MergeKey y)
        {
            int c = StringComparer.Ordinal.Compare(x.Text, y.Text);
            if (c != 0) return c;
            return x.Number.CompareTo(y.Number);
        }
    }
}
