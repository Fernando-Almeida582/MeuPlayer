using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace MeuPlayer;

internal sealed class PeripheralBattery : IDisposable
{
    private const string Connected = "System.Devices.Aep.IsConnected";
    private const string EndpointContainer = "System.Devices.Aep.ContainerId";
    private const string DeviceContainer = "System.Devices.ContainerId";
    private const string Level = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private const string Major = "System.Devices.Aep.Bluetooth.Cod.Major";
    private const string Minor = "System.Devices.Aep.Bluetooth.Cod.Minor";
    private const string Appearance = "System.Devices.Aep.Bluetooth.Le.Appearance";

    internal record Reading(string Name, int? Percent, string Glyph)
    {
        internal string Label => Percent is int value ? $"{value}%" : "—";
    }

    private readonly object gate = new();
    private readonly List<DeviceWatcher> watchers = new();
    private readonly Dictionary<string, DeviceInformation> nodes = new();
    private readonly Dictionary<string, DeviceInformation> endpoints = new();
    private bool changed = true;
    private bool disposed;

    internal void Start()
    {
        string[] properties = [EndpointContainer, Connected, Major, Minor, Appearance];
        Subscribe(BluetoothDevice.GetDeviceSelectorFromPairingState(true), properties, DeviceInformationKind.AssociationEndpoint, endpoints, "classic");
        Subscribe(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), properties, DeviceInformationKind.AssociationEndpoint, endpoints, "le");
        Subscribe("", [DeviceContainer, Level], DeviceInformationKind.Device, nodes, "");
    }

    private void Subscribe(string selector, string[] properties, DeviceInformationKind kind,
        Dictionary<string, DeviceInformation> entries, string prefix)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, properties, kind);
        watcher.Added += (_, device) =>
        {
            lock (gate)
            {
                if (disposed) return;
                entries[prefix + device.Id] = device;
                changed |= kind != DeviceInformationKind.Device || Number(device, Level) is not null;
            }
        };
        watcher.Updated += (_, update) =>
        {
            lock (gate)
            {
                if (!disposed && entries.TryGetValue(prefix + update.Id, out var device))
                {
                    device.Update(update);
                    changed |= kind != DeviceInformationKind.Device || update.Properties.ContainsKey(Level) ||
                        (update.Properties.ContainsKey(DeviceContainer) && Number(device, Level) is not null);
                }
            }
        };
        watcher.Removed += (_, update) =>
        {
            lock (gate)
                if (!disposed && entries.Remove(prefix + update.Id, out var device))
                    changed |= kind != DeviceInformationKind.Device || Number(device, Level) is not null;
        };
        watchers.Add(watcher);
        watcher.Start();
    }

    internal Reading[]? Changes()
    {
        lock (gate)
        {
            if (!changed || disposed) return null;
            changed = false;
            var levels = new Dictionary<Guid, int>();
            foreach (var device in nodes.Values)
            {
                if (Container(device, DeviceContainer) is Guid id && Number(device, Level) is int percent && percent is >= 0 and <= 100)
                    levels.TryAdd(id, percent);
            }

            return endpoints.Values
                .Where(device => device.Properties.TryGetValue(Connected, out var connected) && connected is true)
                .Where(device => Container(device, EndpointContainer).HasValue)
                .GroupBy(device => Container(device, EndpointContainer)!.Value)
                .Select(group => new Reading(
                    group.Select(device => device.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Dispositivo Bluetooth",
                    levels.TryGetValue(group.Key, out int percent) ? percent : null,
                    group.Select(Glyph).FirstOrDefault(glyph => glyph != null) ?? "\uE702"))
                .OrderByDescending(device => device.Percent.HasValue)
                .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
    }

    private static Guid? Container(DeviceInformation device, string property) =>
        device.Properties.TryGetValue(property, out var value) &&
        Guid.TryParse(value?.ToString(), out Guid id) && id != Guid.Empty ? id : null;

    private static int? Number(DeviceInformation device, string property) =>
        device.Properties.TryGetValue(property, out var value) &&
        int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out int number) ? number : null;

    private static string? Glyph(DeviceInformation device)
    {
        int? appearance = Number(device, Appearance);
        switch (appearance)
        {
            case 0x03C1: return "\uE765";
            case 0x03C2: return "\uE962";
            case 0x03C3:
            case 0x03C4: return "\uE7FC";
        }

        int? minor = Number(device, Minor);
        switch (Number(device, Major))
        {
            case 4 when minor == 5: return "\uE7F5";
            case 4 when minor is 1 or 2 or 6 or 7 or 10: return "\uE7F6";
            case 5 when (minor & 15) is 1 or 2: return "\uE7FC";
            case 5 when (minor & 48) == 32: return "\uE962";
            case 5 when (minor & 16) == 16: return "\uE765";
        }

        string name = device.Name.ToLowerInvariant();
        if (Contains(name, "head", "fone", "bud", "airpod", "earphone", "wh-", "wf-")) return "\uE7F6";
        if (Contains(name, "speaker", "alto-falante", "caixa de som")) return "\uE7F5";
        if (Contains(name, "keyboard", "teclado")) return "\uE765";
        if (Contains(name, "mouse")) return "\uE962";
        if (Contains(name, "controller", "controle", "xbox", "dualshock", "dualsense", "joy-con")) return "\uE7FC";
        return null;
    }

    private static bool Contains(string name, params string[] words) => words.Any(name.Contains);

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            nodes.Clear();
            endpoints.Clear();
        }

        foreach (var watcher in watchers)
        {
            try
            {
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                    watcher.Stop();
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
        }
        watchers.Clear();
    }
}
