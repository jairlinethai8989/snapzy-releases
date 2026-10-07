using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace SnapCraft;

internal sealed record ScrollTarget(Rectangle Region, IntPtr WindowHandle);

internal static class ScrollTargetDetector
{
    public static ScrollTarget? WindowAt(Point point, IntPtr excluded)
    {
        var handle = NativeInput.WindowBelow(point, excluded);
        if (handle == IntPtr.Zero) return null;
        var region = Rectangle.Intersect(NativeInput.ClientBounds(handle), Screen.FromPoint(point).Bounds);
        return region.Width >= 80 && region.Height >= 100 ? new ScrollTarget(region, handle) : null;
    }

    public static ScrollTarget? FindWindow(Point point, IntPtr excluded)
    {
        var fallback = WindowAt(point, excluded);
        if (fallback is null) return null;
        try
        {
            var root = AutomationElement.FromHandle(fallback.WindowHandle);
            var clock = Stopwatch.StartNew();
            var remaining = 250;
            var content = FindInTree(root, point, clock, ref remaining, 0);
            if (content is null)
            {
                clock.Restart(); remaining = 250;
                content = FindDocument(root, point, clock, ref remaining, 0);
            }
            var region = Rectangle.Intersect(content ?? fallback.Region, Screen.FromPoint(point).Bounds);
            return new ScrollTarget(region, fallback.WindowHandle);
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException) { return fallback; }
    }

    private static Rectangle? FindDocument(AutomationElement element, Point point, Stopwatch clock, ref int remaining, int depth)
    {
        if (--remaining < 0 || depth > 20 || clock.ElapsedMilliseconds > 250) return null;
        var box = element.Current.BoundingRectangle;
        if (box.IsEmpty || !box.Contains(point.X, point.Y) || element.Current.IsOffscreen) return null;
        if (element.Current.ControlType == ControlType.Document && box.Width >= 200 && box.Height >= 200)
            return Rectangle.FromLTRB((int)Math.Ceiling(box.Left), (int)Math.Ceiling(box.Top), (int)Math.Floor(box.Right), (int)Math.Floor(box.Bottom));
        var walker = TreeWalker.RawViewWalker;
        for (var child = walker.GetFirstChild(element); child is not null && remaining > 0 && clock.ElapsedMilliseconds <= 250; child = walker.GetNextSibling(child))
        {
            var document = FindDocument(child, point, clock, ref remaining, depth + 1);
            if (document is not null) return document;
        }
        return null;
    }

    // Run on an MTA worker: querying another window's provider on the UI thread can deadlock.
    public static ScrollTarget? Find(Point point, IntPtr excluded)
    {
        try
        {
            var handle = NativeInput.WindowBelow(point, excluded);
            if (handle == IntPtr.Zero) return null;
            var root = AutomationElement.FromHandle(handle);
            var clock = Stopwatch.StartNew();
            var remaining = 250;
            var region = FindInTree(root, point, clock, ref remaining, 0);
            if (region is null) return null;
            var clipped = Rectangle.Intersect(region.Value, Screen.FromPoint(point).Bounds);
            return clipped.Width >= 80 && clipped.Height >= 100 ? new ScrollTarget(clipped, handle) : null;
        }
        catch (Exception error) when (error is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Rectangle? FindInTree(AutomationElement element, Point point, Stopwatch clock, ref int remaining, int depth)
    {
        if (--remaining < 0 || depth > 20 || clock.ElapsedMilliseconds > 250) return null;
        var box = element.Current.BoundingRectangle;
        if (box.IsEmpty || !box.Contains(point.X, point.Y) || element.Current.IsOffscreen) return null;
        Rectangle? best = null;
        if (box.Width >= 80 && box.Height >= 100 && element.TryGetCurrentPattern(ScrollPattern.Pattern, out var pattern)
            && ((ScrollPattern)pattern).Current.VerticallyScrollable)
        {
            best = Rectangle.FromLTRB((int)Math.Ceiling(box.Left), (int)Math.Ceiling(box.Top), (int)Math.Floor(box.Right), (int)Math.Floor(box.Bottom));
        }
        var walker = TreeWalker.RawViewWalker;
        for (var child = walker.GetFirstChild(element); child is not null && remaining > 0 && clock.ElapsedMilliseconds <= 250; child = walker.GetNextSibling(child))
        {
            var nested = FindInTree(child, point, clock, ref remaining, depth + 1);
            if (nested is not null && (best is null || (long)nested.Value.Width * nested.Value.Height < (long)best.Value.Width * best.Value.Height)) best = nested;
        }
        // A grid can expose scrolling only on its body, with frozen rows as siblings.
        var type = element.Current.ControlType;
        if (best is not null && (type == ControlType.DataGrid || type == ControlType.Table)
            && box.Width <= best.Value.Width * 1.2 && box.Height <= best.Value.Height * 3)
            return Rectangle.FromLTRB((int)Math.Ceiling(box.Left), (int)Math.Ceiling(box.Top), (int)Math.Floor(box.Right), (int)Math.Floor(box.Bottom));
        return best;
    }
}
