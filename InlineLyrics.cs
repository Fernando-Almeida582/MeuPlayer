using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MeuPlayer;

internal readonly record struct PlaybackClock(double Milliseconds, double Duration, double Rate, long SampledAt)
{
    internal double Position => Math.Clamp(
        Milliseconds + Stopwatch.GetElapsedTime(SampledAt).TotalMilliseconds * Rate, 0, Duration);
}

internal sealed class InlineLyrics : IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly Action<string?> show;
    private CancellationTokenSource? lookup;
    private string? key, displayed;
    private TimedLyrics? lyrics;
    private PlaybackClock? clock;
    private bool requested;

    internal InlineLyrics(Action<string?> show)
    {
        this.show = show;
        timer.Tick += (_, _) => Render();
    }

    internal void Update(string? trackKey, string title, string artist, string album, PlaybackClock? position)
    {
        clock = position;
        if (key != trackKey)
        {
            CancelLookup();
            key = trackKey;
            requested = false;
            lyrics = trackKey is null ? null : LoadLocal(trackKey);
        }

        UpdateTimer();
        if (key is null || clock is null || lyrics?.Count is > 0 || requested || string.IsNullOrWhiteSpace(artist)) return;
        requested = true;
        lookup = new CancellationTokenSource();
        _ = Fetch(title, artist, album, clock.Value.Duration / 1000, lookup);
    }

    private async Task Fetch(string title, string artist, string album, double duration, CancellationTokenSource request)
    {
        try
        {
            var result = await OnlineLyrics.Find(title, artist, album, duration, request.Token);
            if (!ReferenceEquals(lookup, request)) return;
            if (result is { Instrumental: false, Synced: { Length: > 0 and <= TimedLyrics.MaxFileBytes } timed })
                lyrics = TimedLyrics.Parse(timed);
            UpdateTimer();
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or JsonException or InvalidDataException) { }
        finally
        {
            if (ReferenceEquals(lookup, request)) lookup = null;
            request.Dispose();
        }
    }

    private void UpdateTimer()
    {
        Render();
        if (lyrics?.Count is > 0 && clock is { Rate: not 0 }) timer.Start();
        else timer.Stop();
    }

    private void Render()
    {
        string? line = lyrics is not null && clock is { } position
            ? lyrics.TextAt(lyrics.IndexAt(position.Position)) : null;
        if (string.IsNullOrWhiteSpace(line)) line = null;
        if (displayed == line) return;
        displayed = line;
        show(line);
    }

    private static TimedLyrics? LoadLocal(string track)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeuPlayer", "Letras", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(track))) + ".lrc");
        try { return File.Exists(path) ? TimedLyrics.Parse(TimedLyrics.ReadFile(path)) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return null; }
    }

    private void CancelLookup()
    {
        var request = lookup;
        lookup = null;
        request?.Cancel();
    }

    public void Dispose()
    {
        timer.Stop();
        CancelLookup();
    }
}
