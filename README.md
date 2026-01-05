# LargeFilesProcessing (LargeTestFileTool)

A small .NET CLI tool to:
- **Generate** a large test file with lines like: `<number>. <text>`
- **Sort** that file using **external sorting** (works when the file doesn’t fit into RAM)

**Sorting rule:**  
1) sort by `<text>` (ordinal string compare)  
2) then by `<number>` (ascending)

---

## Requirements

- **.NET SDK 10** (project targets `net10.0`)

---

## Project layout

- `src/LargeTestFileTool` — CLI app
- `tests/LargeTestFileTool.Tests` — unit tests
- `files/` — generated input/output files (created automatically)

---

## Default paths

The tool uses paths relative to **your current working directory**:

- Input: `./files/input_data.txt`
- Output: `./files/sorted_output.txt`
- Temp dir: `./files/.tmp_sort`

---

## Build

From repository root:

```bash
dotnet build
```

---

## Run

### Option A — Run from repository root (recommended)

Generate a 2GB file:
```bash
dotnet run --project src/LargeTestFileTool -- fg 2GB
```

Sort the default generated file:
```bash
dotnet run --project src/LargeTestFileTool -- sf
```

Sort a custom input file:
```bash
dotnet run --project src/LargeTestFileTool -- sf "C:\data\big.txt"
```

---

### Option B — Run from the project folder

```bash
cd src/LargeTestFileTool
```

Generate:
```bash
dotnet run -- fg 500MB
```

Sort:
```bash
dotnet run -- sf
```

---

## Commands

### `fg <size>`
Generates `./files/input_data.txt` with approximately the requested size.

Supported size formats:
- `KB`, `MB`, `GB`, `TB` (case-insensitive), e.g. `500MB`, `2GB`
- raw bytes, e.g. `123456`

Examples:
```bash
dotnet run --project src/LargeTestFileTool -- fg 2GB
dotnet run --project src/LargeTestFileTool -- fg 500MB
dotnet run --project src/LargeTestFileTool -- fg 123456
```

---

### `sf [inputPath]`
Sorts the input file (default: `./files/input_data.txt`) into:
- `./files/sorted_output.txt`

Examples:
```bash
dotnet run --project src/LargeTestFileTool -- sf
dotnet run --project src/LargeTestFileTool -- sf "C:\data\big.txt"
```

---

## Help / Usage

Run without arguments to see usage:
```bash
dotnet run --project src/LargeTestFileTool
```

---

## Tests

From repository root:
```bash
dotnet test
```

---

## Notes

- Sorting uses temporary chunk files in `./files/.tmp_sort` — make sure you have enough **free disk space** for:
  - input file
  - temp chunk files
  - output file
- Generated lines are written with `\n` to keep size deterministic across OS.
