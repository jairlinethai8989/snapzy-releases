namespace SnapCraft;

internal enum SelectionMode { Region, Window, Scroll }
internal sealed record CaptureSelection(Rectangle Region, IntPtr WindowHandle);

internal sealed class SelectionOverlay : Form
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    private readonly SelectionMode mode;
    private Point start;
    private Point current;
    private bool dragging;
    private ScrollTarget? hoveredTarget;
    private Point probedPoint;
    private bool probing;
    private readonly System.Windows.Forms.Timer hoverTimer = new() { Interval = 120 };
    private Bitmap? desktop;
    private ScrollTarget? cachedWindow;
    private ScrollTarget? cachedSheet;
    public CaptureSelection? Selection { get; private set; }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; parameters.ExStyle |= 0x08000000; return parameters; }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0021) { message.Result = new IntPtr(3); return; }
        base.WndProc(ref message);
    }

    private SelectionOverlay(SelectionMode mode)
    {
        this.mode = mode;
        FormBorderStyle = FormBorderStyle.None;
        Bounds = SystemInformation.VirtualScreen;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        // Setting WinForms TopMost before Show can activate a full-screen overlay.
        // Raise it with SWP_NOACTIVATE instead, preserving the sheet's current viewport.
        Shown += (_, _) => SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        BackColor = Color.Black;
        Opacity = 0.45;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        DoubleBuffered = true;
        if (mode is SelectionMode.Scroll or SelectionMode.Window)
        {
            try
            {
                desktop = new Bitmap(Width, Height);
                using var graphics = Graphics.FromImage(desktop);
                graphics.CopyFromScreen(Location, Point.Empty, Size);
                Opacity = 1;
            }
            catch { desktop?.Dispose(); desktop = null; }
            hoverTimer.Tick += async (_, _) => await ProbeHoverAsync();
            Shown += (_, _) => hoverTimer.Start();
        }
    }

    private async Task ProbeHoverAsync()
    {
        if (probing || dragging || IsDisposed) return;
        var point = Cursor.Position;
        if (point == probedPoint && hoveredTarget is not null) return;
        probing = true;
        var excluded = Handle;
        try
        {
            hoveredTarget = RefineTarget(ScrollTargetDetector.WindowAt(point, excluded), point);
            Invalidate();
            if (mode == SelectionMode.Window || hoveredTarget == cachedSheet && cachedSheet is not null) { probedPoint = point; return; }
            var target = await Task.Run(() => mode == SelectionMode.Scroll ? ScrollTargetDetector.FindWindow(point, excluded) : ScrollTargetDetector.WindowAt(point, excluded));
            if (IsDisposed || !Visible || dragging || Cursor.Position != point) return;
            probedPoint = point; hoveredTarget = RefineTarget(target, point); Invalidate();
        }
        finally { probing = false; }
    }

    private ScrollTarget? RefineTarget(ScrollTarget? target, Point point)
    {
        if (mode == SelectionMode.Window && target is not null)
            return new ScrollTarget(NativeInput.IsDesktopWindow(target.WindowHandle) ? Screen.FromPoint(point).Bounds : NativeInput.VisibleWindowBounds(target.WindowHandle), target.WindowHandle);
        if (mode != SelectionMode.Scroll || target is null || desktop is null) return target;
        if (target != cachedWindow)
        {
            cachedWindow = target;
            cachedSheet = null;
            var crop = target.Region;
            crop.Offset(-Location.X, -Location.Y);
            crop = Rectangle.Intersect(crop, new Rectangle(Point.Empty, desktop.Size));
            if (crop.Width >= 300 && crop.Height >= 250)
            {
                using var image = desktop.Clone(crop, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (SheetViewportDetector.Find(image) is Rectangle sheet)
                {
                    sheet.Offset(crop.Left + Location.X, crop.Top + Location.Y);
                    cachedSheet = new ScrollTarget(sheet, target.WindowHandle);
                }
            }
        }
        return cachedSheet?.Region.Contains(point) == true ? cachedSheet : target;
    }

    public static async Task<CaptureSelection?> ChooseAsync(SelectionMode mode)
    {
        using var overlay = new SelectionOverlay(mode);
        var completion = new TaskCompletionSource<CaptureSelection?>(TaskCreationOptions.RunContinuationsAsynchronously);
        overlay.FormClosed += (_, _) => completion.TrySetResult(overlay.Selection);
        using var escape = new EscapeHook(() => overlay.Close());
        overlay.Show();
        return await completion.Task;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (mode == SelectionMode.Window || mode == SelectionMode.Scroll && !ModifierKeys.HasFlag(Keys.Control))
        {
            var point = PointToScreen(e.Location);
            var target = mode != SelectionMode.Window && hoveredTarget?.Region.Contains(point) == true ? hoveredTarget : RefineTarget(ScrollTargetDetector.WindowAt(point, Handle), point);
            Hide();
            var handle = target?.WindowHandle ?? NativeInput.RootWindowAt(point);
            if (handle != IntPtr.Zero) Selection = new CaptureSelection(mode == SelectionMode.Scroll ? target?.Region ?? NativeInput.ClientBounds(handle) : NativeInput.IsDesktopWindow(handle) ? Screen.FromPoint(point).Bounds : NativeInput.VisibleWindowBounds(handle), handle);
            DialogResult = Selection is null ? DialogResult.Cancel : DialogResult.OK;
            Close();
            return;
        }
        start = e.Location;
        current = e.Location;
        dragging = true;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging)
        {
            if (mode == SelectionMode.Window || mode == SelectionMode.Scroll && (hoveredTarget is null || !hoveredTarget.Region.Contains(PointToScreen(e.Location))))
            {
                hoveredTarget = RefineTarget(ScrollTargetDetector.WindowAt(PointToScreen(e.Location), Handle), PointToScreen(e.Location));
                Invalidate();
            }
            return;
        }
        current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging || e.Button != MouseButtons.Left) return;
        dragging = false;
        Capture = false;
        current = e.Location;
        if (mode == SelectionMode.Scroll && Math.Abs(current.X - start.X) <= 10 && Math.Abs(current.Y - start.Y) <= 10)
        {
            var point = PointToScreen(current);
            if (hoveredTarget is null || !hoveredTarget.Region.Contains(point) || Math.Abs(point.X - probedPoint.X) > 12 || Math.Abs(point.Y - probedPoint.Y) > 12) { Invalidate(); return; }
            Selection = new CaptureSelection(hoveredTarget.Region, hoveredTarget.WindowHandle);
            DialogResult = DialogResult.OK; Close(); return;
        }
        var region = Rectangle.FromLTRB(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y), Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));
        if (region.Width < 24 || region.Height < 24) return;
        region.Offset(Location);
        Hide();
        Selection = new CaptureSelection(region, NativeInput.RootWindowAt(new Point(region.Left + region.Width / 2, region.Top + region.Height / 2)));
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var instruction = mode switch
        {
            SelectionMode.Window => "คลิกหน้าต่างที่ต้องการจับ  •  Esc ยกเลิก",
            SelectionMode.Scroll => "คลิกหน้าต่างเพื่อจับภาพยาว  •  Ctrl + ลาก เลือกพื้นที่  •  Esc ยกเลิก",
            _ => "เลือกพื้นที่: ลากบริเวณที่ต้องการจับ  •  Esc ยกเลิก"
        };
        var visible = dragging || hoveredTarget is not null;
        var rectangle = dragging ? Rectangle.FromLTRB(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y), Math.Max(start.X, current.X), Math.Max(start.Y, current.Y)) : hoveredTarget?.Region ?? Rectangle.Empty;
        if (!visible) rectangle = Rectangle.Empty;
        if (!dragging) rectangle.Offset(-Location.X, -Location.Y);
        if (desktop is not null)
        {
            e.Graphics.DrawImageUnscaled(desktop, 0, 0);
            using var shade = new SolidBrush(Color.FromArgb(95, 12, 20, 32));
            if (visible)
            {
                e.Graphics.FillRectangle(shade, 0, 0, Width, Math.Max(0, rectangle.Top));
                e.Graphics.FillRectangle(shade, 0, rectangle.Bottom, Width, Math.Max(0, Height - rectangle.Bottom));
                e.Graphics.FillRectangle(shade, 0, rectangle.Top, Math.Max(0, rectangle.Left), rectangle.Height);
                e.Graphics.FillRectangle(shade, rectangle.Right, rectangle.Top, Math.Max(0, Width - rectangle.Right), rectangle.Height);
            }
            else e.Graphics.FillRectangle(shade, ClientRectangle);
        }
        using var font = new Font("Segoe UI", 11, FontStyle.Bold);
        var hint = new Rectangle(20, Height - 55, Math.Min(650, Width - 40), 35);
        using var hintBrush = new SolidBrush(Color.FromArgb(21, 94, 239));
        e.Graphics.FillRectangle(hintBrush, hint);
        TextRenderer.DrawText(e.Graphics, instruction, font, hint, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (visible)
        {
            using var pen = new Pen(mode == SelectionMode.Scroll ? Color.FromArgb(239, 51, 64) : Color.FromArgb(21, 94, 239), 3);
            e.Graphics.DrawRectangle(pen, rectangle);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hoverTimer.Dispose(); desktop?.Dispose(); }
        base.Dispose(disposing);
    }
}
