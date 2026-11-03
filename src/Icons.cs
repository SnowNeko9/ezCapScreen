using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Drawn as vectors and rendered per scale, so icons stay sharp on any monitor.
static class Icons
{
    static readonly Color Blue = Color.FromArgb(47, 127, 193);
    static readonly Color Pale = Color.FromArgb(232, 241, 252);
    static readonly Color DeepBlue = Color.FromArgb(20, 72, 150);
    static readonly Color Navy = Color.FromArgb(10, 40, 105);
    static readonly Color Green = Color.FromArgb(32, 140, 24);
    static readonly Color Red = Color.FromArgb(214, 62, 12);
    static readonly Color Wood = Color.FromArgb(245, 150, 20);
    static readonly Color Ink = Color.FromArgb(60, 40, 20);

    const int DesignSize = 26;
    static readonly Dictionary<KeyValuePair<Action<Graphics>, int>, Bitmap> Rendered = new Dictionary<KeyValuePair<Action<Graphics>, int>, Bitmap>();

    public static Bitmap At(Action<Graphics> draw, int size)
    {
        var key = new KeyValuePair<Action<Graphics>, int>(draw, size);
        Bitmap image;
        if (Rendered.TryGetValue(key, out image)) return image;
        image = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(image))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size / (float)DesignSize, size / (float)DesignSize);
            draw(g);
        }
        Rendered[key] = image;
        return image;
    }

    static Action<Graphics> Make(Action<Graphics> draw) { return draw; }

    // Half-pixel offset puts integer coordinates on pixel edges, so straight edges land on the grid and stay sharp.
    static Action<Graphics> Crisp(Action<Graphics> draw)
    {
        return g =>
        {
            g.PixelOffsetMode = PixelOffsetMode.Half;
            draw(g);
        };
    }

    static Pen Stroke(Color color, float width)
    {
        return new Pen(color, width) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter };
    }

    static Action<Graphics> Dot(int diameter)
    {
        return Crisp(g =>
        {
            using (var fill = new SolidBrush(Navy))
                g.FillEllipse(fill, 13 - diameter / 2, 13 - diameter / 2, diameter, diameter);
        });
    }

    public static readonly Action<Graphics> Rectangle = Make(g =>
    {
        using (var fill = new SolidBrush(Pale))
        using (var pen = new Pen(Blue, 1.5f))
        {
            g.FillRectangle(fill, 6, 6, 14, 14);
            g.DrawRectangle(pen, 6, 6, 14, 14);
        }
    });

    public static readonly Action<Graphics> Oval = Make(g =>
    {
        using (var fill = new SolidBrush(Pale))
        using (var pen = new Pen(Blue, 1.5f))
        {
            g.FillEllipse(fill, 5, 7, 16, 12);
            g.DrawEllipse(pen, 5, 7, 16, 12);
        }
    });

    public static readonly Action<Graphics> Arrow = Make(g =>
    {
        using (var cap = new AdjustableArrowCap(3.5f, 3.5f))
        using (var pen = new Pen(Blue, 2f) { CustomEndCap = cap })
            g.DrawLine(pen, 6, 20, 19, 7);
    });

    public static readonly Action<Graphics> Brush = Crisp(g =>
    {
        using (var handle = new LinearGradientBrush(new Point(24, 2), new Point(12, 14), Color.FromArgb(150, 82, 30), Color.FromArgb(232, 150, 52)))
        using (var outline = new Pen(Color.FromArgb(92, 50, 18), 0.8f))
        {
            var shaft = new[] { new PointF(22.5f, 1.5f), new PointF(24.5f, 3.5f), new PointF(15.5f, 14f), new PointF(12f, 10.5f) };
            g.FillPolygon(handle, shaft);
            g.DrawPolygon(outline, shaft);
        }
        using (var metal = new LinearGradientBrush(new Point(10, 10), new Point(16, 16), Color.FromArgb(240, 244, 248), Color.FromArgb(140, 150, 162)))
        using (var outline = new Pen(Color.FromArgb(80, 88, 98), 0.8f))
        {
            var ferrule = new[] { new PointF(12f, 9.5f), new PointF(16.5f, 14f), new PointF(14f, 16.5f), new PointF(9.5f, 12f) };
            g.FillPolygon(metal, ferrule);
            g.DrawPolygon(outline, ferrule);
        }
        using (var hairs = new GraphicsPath())
        using (var paint = new LinearGradientBrush(new Point(12, 12), new Point(3, 24), Color.FromArgb(60, 150, 235), Color.FromArgb(16, 76, 170)))
        using (var outline = new Pen(Color.FromArgb(12, 56, 130), 0.8f))
        using (var shine = new Pen(Color.FromArgb(170, 255, 255, 255), 1f))
        {
            hairs.AddBezier(9.5f, 12f, 6f, 14f, 6.5f, 19f, 2.5f, 23.5f);
            hairs.AddBezier(2.5f, 23.5f, 8f, 22.5f, 12.5f, 20f, 14f, 16.5f);
            hairs.CloseFigure();
            g.FillPath(paint, hairs);
            g.DrawPath(outline, hairs);
            g.DrawBezier(shine, 9.5f, 14.5f, 8f, 16f, 8f, 18f, 6.5f, 20f);
        }
    });

    public static readonly Action<Graphics> Text = Crisp(g =>
    {
        g.SmoothingMode = SmoothingMode.None;
        using (var ink = new SolidBrush(DeepBlue))
        {
            g.FillRectangle(ink, 6, 5, 14, 3);
            g.FillRectangle(ink, 6, 5, 2, 6);
            g.FillRectangle(ink, 18, 5, 2, 6);
            g.FillRectangle(ink, 11, 5, 4, 16);
            g.FillRectangle(ink, 8, 19, 10, 2);
        }
    });

    public static readonly Action<Graphics> Undo = Crisp(g =>
    {
        using (var pen = new Pen(Green, 3.5f))
            g.DrawArc(pen, 7, 8, 13, 12, 180, 240);
        using (var head = new SolidBrush(Green))
            g.FillPolygon(head, new[] { new Point(2, 12), new Point(13, 12), new Point(7, 20) });
    });

    public static readonly Action<Graphics> Save = Crisp(g =>
    {
        g.SmoothingMode = SmoothingMode.None;
        using (var body = new SolidBrush(DeepBlue))
        {
            g.FillRectangle(body, 4, 4, 18, 18);
            g.FillRectangle(Brushes.White, 8, 4, 10, 6);
            g.FillRectangle(body, 14, 5, 2, 4);
            g.FillRectangle(Brushes.White, 7, 13, 12, 9);
            g.FillRectangle(body, 9, 15, 8, 1);
            g.FillRectangle(body, 9, 18, 8, 1);
        }
    });

    public static readonly Action<Graphics> Cancel = Crisp(g =>
    {
        using (var pen = Stroke(Red, 4f))
        {
            g.DrawLine(pen, 7, 7, 19, 19);
            g.DrawLine(pen, 19, 7, 7, 19);
        }
    });

    public static readonly Action<Graphics> Done = Crisp(g =>
    {
        using (var pen = Stroke(Green, 4f))
            g.DrawLines(pen, new[] { new Point(5, 13), new Point(11, 19), new Point(21, 7) });
    });

    public static readonly Action<Graphics> DotSmall = Dot(4);
    public static readonly Action<Graphics> DotMedium = Dot(8);
    public static readonly Action<Graphics> DotLarge = Dot(12);
}
