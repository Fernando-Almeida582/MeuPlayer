using System;
using Windows.Media.Control;

namespace MeuPlayer;

public partial class PlayerWindow
{
    private InlineLyrics? inlineLyrics;
    private string trackTitle = "Sem mídia ativa";
    private string? lyricLine;

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
        string artist = LimitText(metadata.Artist, "");
        if (artist.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase)) artist = artist[..^8].TrimEnd();
        inlineLyrics ??= new InlineLyrics(ShowLyricLine);
        inlineLyrics.Update(key, LimitText(metadata.Title, ""), artist, LimitText(metadata.AlbumTitle, ""), clock);
    }

    private void ShowLyricLine(string? line)
    {
        lyricLine = line;
        Track.Text = line ?? trackTitle;
        UpdateTrackWidth();
    }

    private void UpdateTrackWidth() =>
        MediaText.Width = floating && vertical ? 108 : lyricLine is null ? 143 : 235;
}
