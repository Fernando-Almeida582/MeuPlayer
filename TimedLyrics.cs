using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MeuPlayer;

internal sealed partial class TimedLyrics
{
    internal const int MaxFileBytes = 512 * 1024;
    internal record Line(long Milliseconds, string Text);
    private readonly Line[] lines;

    private TimedLyrics(Line[] lines) => this.lines = lines;
    internal int Count => lines.Length;
    internal string TextAt(int index) => index >= 0 && index < lines.Length ? lines[index].Text : "";

    [GeneratedRegex(@"\G\[(?<minutes>[0-9]{1,5}):(?<seconds>[0-5][0-9])(?:[.:](?<fraction>[0-9]{1,3}))?\]", RegexOptions.CultureInvariant)]
    private static partial Regex Timestamp();

    [GeneratedRegex(@"^\[offset:\s*(?<value>[+-]?[0-9]+)\s*\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Offset();

    internal static string ReadFile(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length > MaxFileBytes) throw new InvalidDataException("O arquivo de letra deve ter até 512 KB.");
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = new char[(int)file.Length + 1];
        int length = reader.ReadBlock(text, 0, text.Length);
        if (length == text.Length) throw new InvalidDataException("O arquivo mudou enquanto estava sendo lido.");
        return new string(text, 0, length);
    }

    internal static TimedLyrics Parse(string text)
    {
        var entries = new List<Line>();
        var times = new List<long>();
        long offset = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } row)
        {
            row = row.Trim();
            var offsetMatch = Offset().Match(row);
            if (offsetMatch.Success && int.TryParse(offsetMatch.Groups["value"].Value, CultureInfo.InvariantCulture, out int value))
            {
                offset = value;
                continue;
            }

            times.Clear();
            int end = 0;
            for (var match = Timestamp().Match(row); match.Success; match = match.NextMatch())
            {
                long minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
                int seconds = int.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture);
                string fraction = match.Groups["fraction"].Value;
                int milliseconds = fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(3, '0'), CultureInfo.InvariantCulture);
                times.Add(minutes * 60_000 + seconds * 1_000 + milliseconds);
                end = match.Index + match.Length;
            }

            if (times.Count == 0) continue;
            string lyric = row[end..].Trim();
            if (entries.Count + times.Count > 10_000)
                throw new InvalidDataException("O arquivo tem mais de 10.000 marcações de tempo.");
            foreach (long time in times)
                entries.Add(new Line(time, lyric));
        }

        return new TimedLyrics(entries.OrderBy(line => line.Milliseconds)
            .GroupBy(line => Math.Max(0, line.Milliseconds - offset))
            .Select(group => new Line(group.Key, string.Join("\n", group.Select(line => line.Text).Distinct())))
            .ToArray());
    }

    internal int IndexAt(double milliseconds)
    {
        int low = 0, high = lines.Length;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (lines[mid].Milliseconds <= milliseconds) low = mid + 1;
            else high = mid;
        }
        return low - 1;
    }
}
