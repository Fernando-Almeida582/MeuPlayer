using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace MeuPlayer;

public partial class PlayerWindow
{
    private readonly PlayerWindow? controller;
    private Native.Display? display;
    private readonly Dictionary<nint, PlayerWindow> additionalDisplays = new();
    private IReadOnlyList<PeripheralBattery.Reading>? batteryReadings;

    private void RefreshDisplays()
    {
        if (floating)
        {
            CloseAdditionalDisplays();
            return;
        }
        UpdateAdditionalDisplays(Native.Displays());
    }

    private void UpdateAdditionalDisplays(IReadOnlyList<Native.Display> displays)
    {
        if (displays.Count == 0) return;
        var secondary = displays.Where(item => !item.Primary).ToDictionary(item => item.Handle);
        foreach (nint removed in additionalDisplays.Keys.Where(handle => !secondary.ContainsKey(handle)).ToArray())
        {
            additionalDisplays.Remove(removed, out var copy);
            copy!.Close();
        }
        foreach (var (handle, target) in secondary)
        {
            if (!additionalDisplays.TryGetValue(handle, out var copy))
            {
                copy = new PlayerWindow(this, target);
                additionalDisplays.Add(handle, copy);
                copy.Closed += (_, _) =>
                {
                    seekFlyout?.CloseIfTarget(copy.OpenSource);
                    additionalDisplays.Remove(handle);
                };
                copy.Show();
            }
            copy.display = target;
            copy.CopyDisplayState();
            copy.Place();
        }
    }

    private void CloseAdditionalDisplays()
    {
        var copies = additionalDisplays.Values.ToArray();
        additionalDisplays.Clear();
        foreach (var copy in copies) copy.Close();
    }

    private void CopyDisplaySettings()
    {
        mediaVisible = controller!.mediaVisible;
        batteryVisible = controller.batteryVisible;
        ApplyMenu();
    }

    private void CopyDisplayState()
    {
        var source = controller!;
        trackTitle = source.trackTitle;
        if (lyricLine != source.lyricLine) ShowLyricLine(source.lyricLine);
        Track.Text = source.Track.Text;
        Artist.Text = source.Artist.Text;
        OpenSource.IsEnabled = source.OpenSource.IsEnabled;
        OpenSource.ToolTip = source.OpenSource.ToolTip;
        Previous.IsEnabled = source.Previous.IsEnabled;
        Next.IsEnabled = source.Next.IsEnabled;
        Play.IsEnabled = source.Play.IsEnabled;
        Play.Content = source.Play.Content;
        Play.ToolTip = source.Play.ToolTip;
        AutomationProperties.SetName(Play, (string?)source.Play.ToolTip ?? "Reproduzir");
        bool paused = source.Play.ReadLocalValue(Control.BackgroundProperty) != DependencyProperty.UnsetValue;
        bool highlighted = Play.ReadLocalValue(Control.BackgroundProperty) != DependencyProperty.UnsetValue;
        if (paused && !highlighted)
            Play.SetResourceReference(Control.BackgroundProperty, "PlayerHover");
        else if (!paused && highlighted)
            Play.ClearValue(Control.BackgroundProperty);
        SetArtwork(source.Cover.Source as BitmapSource);

        if (batteryUnavailable != source.batteryUnavailable || !ReferenceEquals(batteryReadings, source.batteryReadings))
        {
            batteryUnavailable = source.batteryUnavailable;
            ShowBatteries(source.batteryReadings ?? Array.Empty<PeripheralBattery.Reading>());
        }
    }
}
