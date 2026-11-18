using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MeuPlayer;

internal static class MenuPlacement
{
    internal static void Configure(MenuItem item)
    {
        item.SubmenuOpened += (_, _) =>
        {
            if (item.Template.FindName("PART_Popup", item) is not Popup popup) return;
            popup.PopupAnimation = PopupAnimation.None;
            popup.PlacementTarget = item;
            popup.CustomPopupPlacementCallback = (size, target, _) =>
            {
                var dpi = VisualTreeHelper.GetDpi(item);
                var origin = item.PointToScreen(new Point());
                var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)origin.X, (int)origin.Y));
                var work = screen.WorkingArea;
                bool right = origin.X + (target.Width + size.Width) * dpi.DpiScaleX <= work.Right;
                double x = right ? target.Width - 2 : 2 - size.Width;
                double y = Math.Clamp(origin.Y, work.Top,
                    Math.Max(work.Top, work.Bottom - size.Height * dpi.DpiScaleY)) - origin.Y;
                return [new CustomPopupPlacement(new Point(x, y / dpi.DpiScaleY), PopupPrimaryAxis.Horizontal)];
            };
            popup.Placement = PlacementMode.Custom;
        };

        // Keep the active group open while moving toward a submenu that opens on the other side.
        item.MouseLeave += (_, _) =>
        {
            if (item.Parent is not ContextMenu menu || !menu.IsOpen || !item.IsSubmenuOpen) return;
            item.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                if (!menu.IsOpen) return;
                foreach (var sibling in menu.Items)
                    if (sibling is MenuItem other && !ReferenceEquals(other, item) && other.IsMouseOver) return;
                item.IsSubmenuOpen = true;
            }));
        };
    }
}
