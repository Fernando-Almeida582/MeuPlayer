using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Windows.Media.Control;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace MeuPlayer;

public partial class PlayerWindow
{
    private SeekFlyout? seekFlyout;
    private long seekGeneration;

    internal readonly record struct MediaTimeline(PlaybackClock Clock, TimeSpan Start, double MinSeek, double MaxSeek)
    {
        internal bool CanSeek => MaxSeek > MinSeek;
    }

    private static MediaTimeline? ReadTimeline(Session session, GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback)
    {
        try
        {
            var timeline = session.GetTimelineProperties();
            double duration = (timeline.EndTime - timeline.StartTime).TotalMilliseconds;
            double position = (timeline.Position - timeline.StartTime).TotalMilliseconds;
            double min = Math.Max(0, (timeline.MinSeekTime - timeline.StartTime).TotalMilliseconds);
            double max = (timeline.MaxSeekTime - timeline.StartTime).TotalMilliseconds;
            if (duration <= 0 && position <= 0 && max <= min) return null;
            double limit = duration > 0 ? duration : double.MaxValue;
            double rate = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
                ? playback.PlaybackRate ?? 1 : 0;
            if (!double.IsFinite(rate)) rate = 0;
            if (rate != 0 && timeline.LastUpdatedTime > DateTimeOffset.UnixEpoch)
                position += Math.Max(0, (DateTimeOffset.UtcNow - timeline.LastUpdatedTime).TotalMilliseconds) * rate;
            if (max <= min && duration > min) max = duration;
            if (playback?.Controls.IsPlaybackPositionEnabled != true) max = min;
            else if (duration > 0) max = Math.Min(max, duration);
            var clock = new PlaybackClock(Math.Clamp(position, 0, limit), limit, rate, Stopwatch.GetTimestamp());
            return new MediaTimeline(clock, timeline.StartTime, min, max);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    private void ShowSeekFlyout(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed || ContextMenu?.IsOpen == true) return;
        var shared = controller ?? this;
        try
        {
            if (shared.manager?.GetCurrentSession() is not { } session ||
                session.SourceAppUserModelId != shared.mediaSourceId ||
                ReadTimeline(session, session.GetPlaybackInfo()) is not { CanSeek: true } time) return;
            shared.seekFlyout ??= new SeekFlyout(shared.SeekTo);
            shared.seekFlyout.Open(OpenSource, session.SourceAppUserModelId, shared.mediaSourceTitle, time);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException) { }
    }

    private void ScheduleSeekDismiss(object sender, System.Windows.Input.MouseEventArgs e) =>
        (controller ?? this).seekFlyout?.ScheduleDismiss();

    private async Task SeekTo(double milliseconds, string source, string title)
    {
        if (closed || mediaSourceId != source || mediaSourceTitle != title) return;
        long request = ++seekGeneration;
        try
        {
            if (manager?.GetCurrentSession() is not { } session || session.SourceAppUserModelId != source) return;
            var metadata = await session.TryGetMediaPropertiesAsync();
            if (closed || request != seekGeneration || !string.Equals(metadata.Title, title, StringComparison.Ordinal) ||
                ReadTimeline(session, session.GetPlaybackInfo()) is not { CanSeek: true } range) return;

            var requested = TimeSpan.FromMilliseconds(Math.Clamp(milliseconds, range.MinSeek, range.MaxSeek));
            if (await session.TryChangePlaybackPositionAsync((range.Start + requested).Ticks))
                await RefreshMedia();
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException or OverflowException) { }
    }

    private void UpdateMediaTools(Session? session, GlobalSystemMediaTransportControlsSessionMediaProperties? metadata = null,
        GlobalSystemMediaTransportControlsSessionPlaybackInfo? playback = null)
    {
        if ((!preferences.ShowTrackLyrics || !mediaVisible) && seekFlyout?.IsOpen != true) return;
        MediaTimeline? timeline = session is null ? null : ReadTimeline(session, playback);
        UpdateLyrics(metadata, timeline?.Clock);
        seekFlyout?.Refresh(session?.SourceAppUserModelId, metadata?.Title, timeline);
    }
}

