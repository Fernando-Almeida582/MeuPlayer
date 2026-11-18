using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Enumeration;

namespace MeuPlayer;

internal static class UsbHidBattery
{
    private const string HidSelector = "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\"";
    private const string ContainerProperty = "System.Devices.ContainerId";
    private const int Success = 0x00110000, Feature = 2;

    internal record Candidate(Guid Container, string Name, string? Glyph, int? Percent);

    internal static async Task<Candidate[]> Scan()
    {
        var interfaces = await DeviceInformation.FindAllAsync(HidSelector, new[] { ContainerProperty },
            DeviceInformationKind.DeviceInterface).AsTask().ConfigureAwait(false);
        var results = new List<Candidate>();
        foreach (var device in interfaces)
        {
            if (!device.IsEnabled || !device.Id.StartsWith(@"\\?\hid#vid_", StringComparison.OrdinalIgnoreCase) ||
                !device.Properties.TryGetValue(ContainerProperty, out var value) ||
                !Guid.TryParse(value?.ToString(), out Guid id) || id == Guid.Empty) continue;

            results.Add(ReadCollection(device.Id, id, device.Name));
        }
        return results.ToArray();
    }

    private static Candidate ReadCollection(string path, Guid id, string name)
    {
        using var file = CreateFile(path, 0, 3, 0, 3, 0, 0);
        if (file.IsInvalid || !HidD_GetPreparsedData(file, out nint data))
            return new Candidate(id, name, null, null);

        try
        {
            if (HidP_GetCaps(data, out var caps) != Success) return new Candidate(id, name, null, null);

            string? glyph = (caps.UsagePage, caps.Usage) switch
            {
                (1, 2) => "\uE962",
                (1, 4 or 5) => "\uE7FC",
                (1, 6 or 7) => "\uE765",
                _ => null
            };
            if (caps.FeatureReportLength is 0 or > 4096 || caps.FeatureValueCount is 0 or > 1024)
                return new Candidate(id, name, glyph, null);

            var values = new ValueCaps[caps.FeatureValueCount];
            int? percentage = ReadFeature(path, file, data, caps.FeatureReportLength, values, 6, 0x20)
                ?? ReadFeature(path, file, data, caps.FeatureReportLength, values, 0x85, 0x66);
            if (percentage is not null)
            {
                var product = new StringBuilder(128);
                if (HidD_GetProductString(file, product, (uint)(product.Capacity * 2)) && product.Length > 0)
                    name = product.ToString().Trim();
            }
            return new Candidate(id, name, glyph, percentage);
        }
        finally { HidD_FreePreparsedData(data); }
    }

    private static int? ReadFeature(string path, SafeFileHandle file, nint data, int reportLength, ValueCaps[] values,
        ushort page, ushort usage)
    {
        ushort count = (ushort)values.Length;
        if (HidP_GetSpecificValueCaps(Feature, page, 0, usage, values, ref count, data) != Success) return null;

        byte[]? report = null;
        for (int index = 0; index < count; index++)
        {
            var value = values[index];
            if (value.ReportCount != 1 || value.BitSize is 0 or > 32 || value.Min < 0 || value.Max <= value.Min ||
                (page == 0x85 && (value.Min != 0 || value.Max != 100 || value.Units != 0))) continue;

            report ??= new byte[reportLength];
            Array.Clear(report);
            report[0] = value.ReportId;
            if (!HidD_GetFeature(file, report, (uint)report.Length))
            {
                // Some HID drivers require a handle opened for reading for this IOCTL.
                using var readable = CreateFile(path, 0x80000000, 3, 0, 3, 0, 0);
                report[0] = value.ReportId;
                if (readable.IsInvalid || !HidD_GetFeature(readable, report, (uint)report.Length)) continue;
            }

            if (HidP_GetUsageValue(Feature, page, value.Collection, usage, out uint raw, data, report, (uint)report.Length) == Success &&
                Normalize(raw, value.Min, value.Max) is int percentage) return percentage;
        }
        return null;
    }

    internal static int? Normalize(uint raw, int min, int max) =>
        min >= 0 && max > min && raw >= (uint)min && raw <= (uint)max
            ? (int)Math.Round((raw - (double)min) * 100 / (max - (double)min)) : null;

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct HidCaps
    {
        [FieldOffset(0)] public ushort Usage;
        [FieldOffset(2)] public ushort UsagePage;
        [FieldOffset(8)] public ushort FeatureReportLength;
        [FieldOffset(60)] public ushort FeatureValueCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    private struct ValueCaps
    {
        [FieldOffset(2)] public byte ReportId;
        [FieldOffset(6)] public ushort Collection;
        [FieldOffset(18)] public ushort BitSize;
        [FieldOffset(20)] public ushort ReportCount;
        [FieldOffset(36)] public uint Units;
        [FieldOffset(40)] public int Min;
        [FieldOffset(44)] public int Max;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFileW", ExactSpelling = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle file, out nint data);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(nint data);
    [DllImport("hid.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetProductString(SafeFileHandle file, StringBuilder text, uint length);
    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetFeature(SafeFileHandle file, [In, Out] byte[] report, uint length);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(nint data, out HidCaps caps);
    [DllImport("hid.dll")] private static extern int HidP_GetSpecificValueCaps(int type, ushort page, ushort collection,
        ushort usage, [Out] ValueCaps[] values, ref ushort count, nint data);
    [DllImport("hid.dll")] private static extern int HidP_GetUsageValue(int type, ushort page, ushort collection,
        ushort usage, out uint raw, nint data, byte[] report, uint length);
}
