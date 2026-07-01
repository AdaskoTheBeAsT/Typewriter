using System.Globalization;
using System.Text;

namespace Typewriter.Engine;

public static class UnifiedDiffBuilder
{
    private const int DefaultContextLines = 3;
    private const long MaxDiffCells = 4_000_000;

    private enum DiffOperationKind
    {
        Equal,
        Delete,
        Insert,
    }

    public static string Build(
        string path,
        string oldContent,
        string newContent)
    {
        ArgumentNullException.ThrowIfNull(argument: path);
        ArgumentNullException.ThrowIfNull(argument: oldContent);
        ArgumentNullException.ThrowIfNull(argument: newContent);

        var oldLines = SplitLines(content: oldContent);
        var newLines = SplitLines(content: newContent);
        var operations = ComputeOperations(oldLines: oldLines, newLines: newLines);
        if (operations.TrueForAll(match: operation => operation.Kind == DiffOperationKind.Equal))
        {
            return string.Empty;
        }

        var hunks = BuildHunks(operations: operations, contextLines: DefaultContextLines);
        return FormatDiff(path: path, hunks: hunks);
    }

    private static DiffSourceLine[] SplitLines(string content)
    {
        if (content.Length == 0)
        {
            return [];
        }

        var lines = new List<DiffSourceLine>();
        var lineStart = 0;
        var index = 0;
        while (index < content.Length)
        {
            var terminatorLength = content[index] switch
            {
                '\n' => 1,
                '\r' when index + 1 < content.Length && content[index + 1] == '\n' => 2,
                '\r' => 1,
                _ => 0,
            };
            if (terminatorLength == 0)
            {
                index++;
                continue;
            }

            lines.Add(
                item: new DiffSourceLine(
                    Text: content.Substring(startIndex: lineStart, length: index - lineStart),
                    Terminator: content.Substring(startIndex: index, length: terminatorLength)));
            index += terminatorLength;
            lineStart = index;
        }

        if (lineStart < content.Length)
        {
            lines.Add(item: new DiffSourceLine(Text: content[lineStart..], Terminator: string.Empty));
        }

        return lines.ToArray();
    }

    private static List<DiffOperation> ComputeOperations(
        DiffSourceLine[] oldLines,
        DiffSourceLine[] newLines)
    {
        var oldCount = oldLines.Length;
        var newCount = newLines.Length;
        if ((long)(oldCount + 1) * (newCount + 1) > MaxDiffCells)
        {
            return ComputeReplaceAllOperations(oldLines: oldLines, newLines: newLines);
        }

        var lengths = new int[oldCount + 1, newCount + 1];
        for (var i = oldCount - 1; i >= 0; i--)
        {
            for (var j = newCount - 1; j >= 0; j--)
            {
                lengths[i, j] = oldLines[i] == newLines[j]
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(val1: lengths[i + 1, j], val2: lengths[i, j + 1]);
            }
        }

        var operations = new List<DiffOperation>(capacity: oldCount + newCount);
        var oldIndex = 0;
        var newIndex = 0;
        while (oldIndex < oldCount && newIndex < newCount)
        {
            if (oldLines[oldIndex] == newLines[newIndex])
            {
                operations.Add(item: new DiffOperation(Kind: DiffOperationKind.Equal, Line: oldLines[oldIndex]));
                oldIndex++;
                newIndex++;
            }
            else if (lengths[oldIndex + 1, newIndex] >= lengths[oldIndex, newIndex + 1])
            {
                operations.Add(item: new DiffOperation(Kind: DiffOperationKind.Delete, Line: oldLines[oldIndex]));
                oldIndex++;
            }
            else
            {
                operations.Add(item: new DiffOperation(Kind: DiffOperationKind.Insert, Line: newLines[newIndex]));
                newIndex++;
            }
        }

        while (oldIndex < oldCount)
        {
            operations.Add(item: new DiffOperation(Kind: DiffOperationKind.Delete, Line: oldLines[oldIndex]));
            oldIndex++;
        }

        while (newIndex < newCount)
        {
            operations.Add(item: new DiffOperation(Kind: DiffOperationKind.Insert, Line: newLines[newIndex]));
            newIndex++;
        }

        return operations;
    }

    private static List<DiffOperation> ComputeReplaceAllOperations(
        DiffSourceLine[] oldLines,
        DiffSourceLine[] newLines)
    {
        var operations = new List<DiffOperation>(capacity: oldLines.Length + newLines.Length);
        operations.AddRange(collection: oldLines.Select(selector: line => new DiffOperation(Kind: DiffOperationKind.Delete, Line: line)));
        operations.AddRange(collection: newLines.Select(selector: line => new DiffOperation(Kind: DiffOperationKind.Insert, Line: line)));
        return operations;
    }

    private static List<DiffHunk> BuildHunks(
        IReadOnlyList<DiffOperation> operations,
        int contextLines)
    {
        var oldLineAt = new int[operations.Count];
        var newLineAt = new int[operations.Count];
        var oldCursor = 1;
        var newCursor = 1;
        for (var i = 0; i < operations.Count; i++)
        {
            oldLineAt[i] = oldCursor;
            newLineAt[i] = newCursor;
            if (operations[i].Kind != DiffOperationKind.Insert)
            {
                oldCursor++;
            }

            if (operations[i].Kind != DiffOperationKind.Delete)
            {
                newCursor++;
            }
        }

        var ranges = FindHunkRanges(operations: operations, contextLines: contextLines);
        var hunks = new List<DiffHunk>(capacity: ranges.Count);
        foreach (var range in ranges)
        {
            var lines = new List<DiffLine>(capacity: range.End - range.Start + 1);
            var oldCount = 0;
            var newCount = 0;
            for (var i = range.Start; i <= range.End; i++)
            {
                var operation = operations[i];
                var marker = operation.Kind switch
                {
                    DiffOperationKind.Delete => '-',
                    DiffOperationKind.Insert => '+',
                    _ => ' ',
                };
                lines.Add(item: new DiffLine(Marker: marker, Source: operation.Line));
                if (operation.Kind != DiffOperationKind.Insert)
                {
                    oldCount++;
                }

                if (operation.Kind != DiffOperationKind.Delete)
                {
                    newCount++;
                }
            }

            hunks.Add(item: new DiffHunk(
                OldStart: oldCount == 0 ? 0 : oldLineAt[range.Start],
                OldCount: oldCount,
                NewStart: newCount == 0 ? 0 : newLineAt[range.Start],
                NewCount: newCount,
                Lines: lines));
        }

        return hunks;
    }

    private static List<(int Start, int End)> FindHunkRanges(
        IReadOnlyList<DiffOperation> operations,
        int contextLines)
    {
        var count = operations.Count;
        var rawRanges = new List<(int Start, int End)>();
        var index = 0;
        while (index < count)
        {
            if (operations[index].Kind == DiffOperationKind.Equal)
            {
                index++;
                continue;
            }

            var runStart = index;
            while (index < count && operations[index].Kind != DiffOperationKind.Equal)
            {
                index++;
            }

            var runEnd = index - 1;
            rawRanges.Add(item: (Start: Math.Max(val1: 0, val2: runStart - contextLines), End: Math.Min(val1: count - 1, val2: runEnd + contextLines)));
        }

        if (rawRanges.Count == 0)
        {
            return rawRanges;
        }

        var merged = new List<(int Start, int End)> { rawRanges[0] };
        for (var i = 1; i < rawRanges.Count; i++)
        {
            var last = merged[^1];
            var current = rawRanges[i];
            if (current.Start <= last.End + 1)
            {
                merged[^1] = (Start: last.Start, End: Math.Max(val1: last.End, val2: current.End));
            }
            else
            {
                merged.Add(item: current);
            }
        }

        return merged;
    }

    private static string FormatDiff(
        string path,
        IReadOnlyList<DiffHunk> hunks)
    {
        var builder = new StringBuilder();
        builder.Append(value: "--- ").Append(value: path).Append(value: '\n');
        builder.Append(value: "+++ ").Append(value: path).Append(value: '\n');
        foreach (var hunk in hunks)
        {
            builder.Append(value: "@@ -")
                .Append(value: hunk.OldStart.ToString(provider: CultureInfo.InvariantCulture))
                .Append(value: ',')
                .Append(value: hunk.OldCount.ToString(provider: CultureInfo.InvariantCulture))
                .Append(value: " +")
                .Append(value: hunk.NewStart.ToString(provider: CultureInfo.InvariantCulture))
                .Append(value: ',')
                .Append(value: hunk.NewCount.ToString(provider: CultureInfo.InvariantCulture))
                .Append(value: " @@\n");
            foreach (var line in hunk.Lines)
            {
                builder.Append(value: line.Marker).Append(value: line.Source.Text).Append(value: '\n');
                if (line.Source.Terminator.Length == 0)
                {
                    builder.Append(value: "\\ No newline at end of file\n");
                }
            }
        }

        return builder.ToString().TrimEnd(trimChar: '\n');
    }

    private readonly record struct DiffSourceLine(string Text, string Terminator);

    private readonly record struct DiffOperation(DiffOperationKind Kind, DiffSourceLine Line);

    private readonly record struct DiffLine(char Marker, DiffSourceLine Source);

    private sealed record DiffHunk(int OldStart, int OldCount, int NewStart, int NewCount, IReadOnlyList<DiffLine> Lines);
}
