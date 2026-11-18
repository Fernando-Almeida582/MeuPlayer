using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Media.Control;

namespace MeuPlayer;

public partial class PlayerWindow
{
    private InlineLyrics? inlineLyrics;
    private string trackTitle = "Sem mídia ativa";
    private string? lyricLine;
    private string? lyricTrackKey;
    private double preferredLyricWidth = 235;

    private void ToggleTrackLyrics()
    {
        preferences.ShowTrackLyrics = !preferences.ShowTrackLyrics;
        if (!preferences.ShowTrackLyrics) StopInlineLyrics();
        SavePreferences();
        BuildMenus();
        _ = RefreshMedia();
    }

    private void StopInlineLyrics()
    {
        inlineLyrics?.Dispose();
        inlineLyrics = null;
        lyricTrackKey = null;
        preferredLyricWidth = 235;
        ShowLyricLine(null);
    }

    private void UpdateLyrics(GlobalSystemMediaTransportControlsSessionMediaProperties? metadata, PlaybackClock? clock)
    {
        if (!preferences.ShowTrackLyrics || !mediaVisible) return;
        if (metadata is null || string.IsNullOrWhiteSpace(metadata.Title) ||
            MediaSource.IsAdvertisement(metadata.Title, metadata.Artist ?? ""))
        {
            inlineLyrics?.Update(null, "", "", "", null);
            return;
        }

        string key = string.Join("\u001f", metadata.Title.Trim(), metadata.Artist?.Trim(), metadata.AlbumTitle?.Trim());
        bool trackChanged = lyricTrackKey != key;
        if (trackChanged)
        {
            lyricTrackKey = key;
            preferredLyricWidth = 235;
        }
        string artist = LimitText(metadata.Artist, "");
        if (artist.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase)) artist = artist[..^8].TrimEnd();
        inlineLyrics ??= new InlineLyrics(ShowLyricLine);
        inlineLyrics.Update(key, LimitText(metadata.Title, ""), artist, LimitText(metadata.AlbumTitle, ""), clock);
        if (trackChanged && lyricLine is not null)
        {
            preferredLyricWidth = MeasureLyricWidth(Track, lyricLine, preferredLyricWidth);
            UpdateTrackWidth();
        }
    }

    private void ShowLyricLine(string? line)
    {
        lyricLine = line;
        Track.Text = line ?? trackTitle;
        if (line is not null) preferredLyricWidth = MeasureLyricWidth(Track, line, preferredLyricWidth);
        UpdateTrackWidth();
    }

    // Keep the widest line seen in this track, avoiding a resize for each verse.
    internal static double MeasureLyricWidth(TextBlock text, string line, double previous)
    {
        var measured = new FormattedText(line, CultureInfo.CurrentUICulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
            text.FontSize, Brushes.White, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        return Math.Clamp(Math.Max(previous, Math.Ceiling(measured.WidthIncludingTrailingWhitespace) + 4), 235, 4096);
    }

    private void UpdateTrackWidth()
    {
        if (floating) MediaText.Width = vertical ? 108 : lyricLine is null ? 143 : 235;
        else Place();
    }
}
