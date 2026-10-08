using System.ComponentModel;

namespace SnapCraft;

internal sealed class ImageReviewCanvas : ScrollableControl
{
    internal Bitmap Image { get; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal Bitmap? Second { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal double Split { get; set; } = .5;
    internal List<Rectangle> Regions { get; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool SelectRegions { get; set; }
    internal event Action? RegionsChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal bool Fit { get; set; } = true;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] internal int MarkerY { get; set; } = -1;
    private Point? start;
    private Rectangle draft;
    private float ScaleFactor => Fit ? Math.Min(1f, Math.Max(.01f, (ClientSize.Width - 24f) / Image.Width)) : 1f;
    public ImageReviewCanvas(Bitmap image)
    {
        Image = image; Dock = DockStyle.Fill; AutoScroll = true; BackColor = Color.FromArgb(238, 242, 246); DoubleBuffered = true;
        Resize += (_, _) => RefreshView();
    }
    internal void RefreshView() { AutoScrollMinSize = new Size((int)Math.Ceiling(Image.Width * ScaleFactor), (int)Math.Ceiling(Image.Height * ScaleFactor)); Invalidate(); }
    internal void ScrollTo(int y) { AutoScrollPosition = new Point(0, Math.Max(0, (int)(y * ScaleFactor) - 100)); Invalidate(); }
    private Point ImagePoint(Point p) => new(Math.Clamp((int)((p.X - AutoScrollPosition.X) / ScaleFactor), 0, Image.Width), Math.Clamp((int)((p.Y - AutoScrollPosition.Y) / ScaleFactor), 0, Image.Height));
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (SelectRegions && e.Button == MouseButtons.Left) { start = ImagePoint(e.Location); Capture = true; } }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); if (start is not Point from) return; var to = ImagePoint(e.Location);
        draft = Rectangle.FromLTRB(Math.Min(from.X, to.X), Math.Min(from.Y, to.Y), Math.Max(from.X, to.X), Math.Max(from.Y, to.Y)); Invalidate();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); if (start is null) return; var completed = draft; start = null; Capture = false;
        if (completed.Width > 1 && completed.Height > 1) Regions.Add(completed); draft = Rectangle.Empty; RegionsChanged?.Invoke(); Invalidate();
    }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) { start = null; draft = Rectangle.Empty; Invalidate(); } }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var graphics = e.Graphics; graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y); graphics.ScaleTransform(ScaleFactor, ScaleFactor);
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor; graphics.DrawImage(Image, 0, 0, Image.Width, Image.Height);
        if (Second is not null)
        {
            var width = (int)Math.Round(Image.Width * Split); graphics.SetClip(new Rectangle(0, 0, width, Math.Max(Image.Height, Second.Height)));
            graphics.DrawImage(Second, 0, 0, Second.Width, Second.Height); graphics.ResetClip();
            using var splitPen = new Pen(Color.DeepSkyBlue, 2 / ScaleFactor); graphics.DrawLine(splitPen, width, 0, width, Math.Max(Image.Height, Second.Height));
        }
        foreach (var region in Regions) graphics.FillRectangle(Brushes.Black, region);
        using var pen = new Pen(Color.Crimson, 2 / ScaleFactor);
        if (!draft.IsEmpty) graphics.DrawRectangle(pen, draft);
        if (MarkerY >= 0) graphics.DrawLine(pen, 0, MarkerY, Image.Width, MarkerY);
    }
}
