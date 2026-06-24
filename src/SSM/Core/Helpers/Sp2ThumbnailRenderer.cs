using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;
using WpfColor = System.Windows.Media.Color;

namespace SSM.Core.Helpers;

public static class Sp2ThumbnailRenderer
{
    private static readonly Dictionary<string, WpfColor> KindColors = new()
    {
        ["IMG"]    = WpfColor.FromArgb(140, 70, 70, 70),
        ["LBL"]    = WpfColor.FromArgb(160, 40, 140, 60),
        ["SIMPLE"] = WpfColor.FromArgb(160, 0, 120, 200),
        ["GAUGE"]  = WpfColor.FromArgb(160, 200, 120, 0),
        ["GRAPH"]  = WpfColor.FromArgb(160, 100, 0, 180),
        ["BAR"]    = WpfColor.FromArgb(160, 0, 150, 150),
    };

    public static BitmapSource? TryRender(string sp2Path, int thumbW, int thumbH)
    {
        try
        {
            var panel = Sp2Parser.Parse(sp2Path);
            if (panel.Width <= 0 || panel.Height <= 0) return null;

            var canvas = new Canvas
            {
                Width      = panel.Width,
                Height     = panel.Height,
                Background = new SolidColorBrush(panel.Background),
            };

            foreach (var el in panel.Elements)
            {
                if (el.Width <= 0 || el.Height <= 0) continue;

                var color = KindColors.GetValueOrDefault(el.ElementKind,
                    WpfColor.FromArgb(120, 100, 100, 100));

                var b = new Border
                {
                    Width        = el.Width,
                    Height       = el.Height,
                    Background   = new SolidColorBrush(color),
                    CornerRadius = new CornerRadius(2),
                };
                Canvas.SetLeft(b, el.X);
                Canvas.SetTop(b, el.Y);
                canvas.Children.Add(b);
            }

            canvas.Measure(new Size(panel.Width, panel.Height));
            canvas.Arrange(new Rect(0, 0, panel.Width, panel.Height));
            canvas.UpdateLayout();

            var rtb = new RenderTargetBitmap(thumbW, thumbH, 96, 96, PixelFormats.Pbgra32);
            var dv  = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var vb = new VisualBrush(canvas) { Stretch = Stretch.Uniform };
                dc.DrawRectangle(vb, null, new Rect(0, 0, thumbW, thumbH));
            }
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }
        catch
        {
            return null;
        }
    }
}
