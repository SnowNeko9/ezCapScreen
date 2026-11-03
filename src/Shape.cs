using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;

// Screen-space annotation. Rectangle, Oval and Arrow hold exactly two points, Brush holds the whole stroke,
// Text holds its top-left corner. For Text, width picks the font size.
sealed class Shape
{
    static readonly StringFormat TextLayout = MakeTextLayout();

    readonly Tool kind;
    readonly List<Point> points;
    readonly float scale;
    Color color;
    int width;

    public string Text = "";

    public Shape(Tool kind, Color color, int width, float scale, Point start)
    {
        this.kind = kind;
        this.color = color;
        this.width = width;
        this.scale = scale;
        points = new List<Point> { start, start };
    }

    public void Extend(Point p)
    {
        if (kind == Tool.Brush) points.Add(p);
        else points[1] = p;
    }

    public bool IsEmpty
    {
        get { return kind == Tool.Text ? string.IsNullOrWhiteSpace(Text) : points.All(p => p == points[0]); }
    }

    public void Restyle(Color newColor, int newWidth)
    {
        color = newColor;
        width = newWidth;
    }

    static StringFormat MakeTextLayout()
    {
        var layout = (StringFormat)StringFormat.GenericTypographic.Clone();
        layout.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoClip;
        return layout;
    }

    // Leelawadee UI carries both Thai and Latin, so no glyph depends on font fallback.
    Font TextFont()
    {
        return new Font("Leelawadee UI", (12 + width * 3) * scale, GraphicsUnit.Pixel);
    }

    public RectangleF TextBounds(Graphics g)
    {
        using (var font = TextFont())
        {
            var size = g.MeasureString(Text, font, PointF.Empty, TextLayout);
            return new RectangleF(points[0], new SizeF(size.Width, Math.Max(size.Height, font.GetHeight(g))));
        }
    }

    public RectangleF CaretBounds(Graphics g, int index)
    {
        using (var font = TextFont())
        {
            var before = Text.Substring(0, Math.Min(index, Text.Length));
            var line = before.Substring(before.LastIndexOf('\n') + 1);
            float height = font.GetHeight(g);
            float x = g.MeasureString(line, font, PointF.Empty, TextLayout).Width;
            return new RectangleF(points[0].X + x, points[0].Y + before.Count(c => c == '\n') * height, 2, height);
        }
    }

    public void Draw(Graphics g)
    {
        using (var pen = new Pen(color, width * scale))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = kind == Tool.Rectangle ? LineJoin.Miter : LineJoin.Round;
            var box = Overlay.Corners(points[0], points[1]);
            switch (kind)
            {
                case Tool.Rectangle: g.DrawRectangle(pen, box); break;
                case Tool.Oval: g.DrawEllipse(pen, box); break;
                case Tool.Brush: g.DrawLines(pen, points.ToArray()); break;
                case Tool.Text:
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    using (var font = TextFont())
                    using (var ink = new SolidBrush(color))
                        g.DrawString(Text, font, ink, points[0], TextLayout);
                    break;
                case Tool.Arrow:
                    using (var cap = new AdjustableArrowCap(4, 4))
                    {
                        pen.CustomEndCap = cap;
                        g.DrawLine(pen, points[0], points[1]);
                    }
                    break;
            }
        }
    }
}
