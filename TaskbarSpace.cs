using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace MeuPlayer;

internal static class TaskbarSpace
{
    internal readonly record struct Slot(int X, int Width);
    private sealed class Entry
    {
        internal long Next;
        internal bool Busy;
        internal (int Left, int Right)[]? Occupied;
    }
    private static readonly Dictionary<nint, Entry> entries = new();

    internal static Slot? Available(nint taskbar, Native.Rect bounds, int requestedWidth, int minimumWidth, int margin, bool right = false)
    {
        if (taskbar == 0) return null;
        if (!entries.TryGetValue(taskbar, out var entry)) entries[taskbar] = entry = new Entry();
        if (!entry.Busy && Environment.TickCount64 >= entry.Next)
        {
            entry.Busy = true;
            entry.Next = Environment.TickCount64 + 3000;
            _ = Read(taskbar, entry);
        }
        return entry.Occupied is { } occupied
            ? ChooseSide(bounds.Left + margin, bounds.Right - margin, occupied, requestedWidth, minimumWidth, margin, right)
            : null;
    }

    private static async Task Read(nint taskbar, Entry entry)
    {
        try
        {
            var occupied = await Task.Run(() =>
            {
                var root = AutomationElement.FromHandle(taskbar);
                var cache = new CacheRequest();
                cache.Add(AutomationElement.BoundingRectangleProperty);
                cache.Add(AutomationElement.IsOffscreenProperty);
                cache.Add(AutomationElement.ProcessIdProperty);
                using var scope = cache.Activate();
                var controls = root.FindAll(TreeScope.Descendants, new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)));
                var intervals = new List<(int Left, int Right)>();
                foreach (AutomationElement control in controls)
                {
                    if (control.Cached.ProcessId == Environment.ProcessId) continue;
                    var rect = control.Cached.BoundingRectangle;
                    if (!control.Cached.IsOffscreen && !rect.IsEmpty && rect.Width > 0)
                        intervals.Add(((int)Math.Floor(rect.Left), (int)Math.Ceiling(rect.Right)));
                }
                return intervals.Count > 0 ? intervals.ToArray() : null;
            });
            entry.Occupied = occupied;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or ElementNotAvailableException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Keep the last complete measurement across a transient accessibility failure.
        }
        finally { entry.Busy = false; }
    }

    internal static Slot? ChooseSide(int left, int right, IEnumerable<(int Left, int Right)> occupied,
        int requested, int minimum, int margin, bool dockRight)
    {
        if (!dockRight) return Choose(left, right, occupied, requested, minimum, margin);
        var reflected = occupied.Select(item => (Left: -item.Right, Right: -item.Left));
        var result = Choose(-right, -left, reflected, requested, minimum, margin);
        return result is { } slot ? new Slot(-slot.X - slot.Width, slot.Width) : null;
    }

    internal static Slot? Choose(int left, int right, IEnumerable<(int Left, int Right)> occupied,
        int requested, int minimum, int margin)
    {
        int cursor = left;
        Slot? best = null;
        foreach (var block in occupied.OrderBy(item => item.Left))
        {
            int end = Math.Clamp(block.Left - margin, left, right);
            int width = end - cursor;
            if (width >= requested) return new Slot(cursor, requested);
            if (width >= minimum) return new Slot(cursor, width);
            cursor = Math.Max(cursor, Math.Min(right, block.Right + margin));
        }
        int remaining = right - cursor;
        if (remaining >= requested) return new Slot(cursor, requested);
        if (remaining >= minimum && (best is null || remaining > best.Value.Width)) best = new Slot(cursor, remaining);
        return best;
    }
}




