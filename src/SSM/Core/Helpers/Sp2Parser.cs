using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace SSM.Core.Helpers;

public record Sp2Element(
    string RawId,
    string SensorId,
    string ElementKind,
    double X,
    double Y,
    double Width,
    double Height,
    string Label,
    string Unit,
    bool ShowLabel,
    bool ShowValue,
    bool ShowUnit,
    string FontName,
    int FontSize,
    WpfColor TextColor,
    WpfColor ValueColor,
    WpfColor LabelColor,
    double MinVal,
    double MaxVal,
    string ImageFile,
    List<string> GaugeFrames,
    WpfColor GraphColor,
    WpfColor BgColor,
    bool IsBackground
);

public class Sp2Panel
{
    public double Width { get; set; } = 1024;
    public double Height { get; set; } = 600;
    public WpfColor Background { get; set; } = WpfColor.FromRgb(0, 0, 0);
    public List<Sp2Element> Elements { get; set; } = [];
}

public static class Sp2Parser
{
    private static readonly Regex TagRx = new(@"<([A-Z0-9]+)>(.*?)</\1>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static Sp2Panel Parse(string filePath)
    {
        // .sp2 is UTF-16 LE with a BOM
        string[] lines = File.ReadAllLines(filePath, Encoding.Unicode);

        var panel = new Sp2Panel();

        foreach (var raw in lines)
        {
            var tags = ExtractTags(raw);
            if (tags.Count == 0) continue;

            // Header line
            if (tags.ContainsKey("SPWIDTH"))
            {
                panel.Width = ParseDouble(tags, "SPWIDTH", 1024);
                panel.Height = ParseDouble(tags, "SPHEIGHT", 600);
                panel.Background = ParseColor(tags, "SPBGCOLOR");
                continue;
            }

            // Version line
            if (tags.ContainsKey("SPVER") && !tags.ContainsKey("ID")) continue;

            if (!tags.TryGetValue("ID", out var rawId)) continue;

            var el = BuildElement(rawId, tags);
            if (el is not null)
                panel.Elements.Add(el);
        }

        // SPWIDTH=0/SPHEIGHT=0 means "full screen" in AIDA64 — infer from bg image
        if (panel.Width == 0 || panel.Height == 0)
        {
            var dir = Path.GetDirectoryName(filePath) ?? "";
            var bgEl = panel.Elements.FirstOrDefault(e => e.ElementKind == "IMG" && !string.IsNullOrEmpty(e.ImageFile));
            var bgPath = bgEl is not null ? Path.Combine(dir, bgEl.ImageFile) : null;
            if (bgPath is not null && File.Exists(bgPath))
            {
                try
                {
                    var decoder = BitmapDecoder.Create(new Uri(bgPath, UriKind.Absolute),
                        BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    var frame = decoder.Frames[0];
                    if (panel.Width  == 0) panel.Width  = frame.PixelWidth;
                    if (panel.Height == 0) panel.Height = frame.PixelHeight;
                }
                catch { }
            }
            if (panel.Width  == 0) panel.Width  = 1920;
            if (panel.Height == 0) panel.Height = 1080;
        }

        return panel;
    }

    private static Sp2Element? BuildElement(string rawId, Dictionary<string, string> t)
    {
        string kind;
        string sensorId;

        if (rawId == "IMG")
        {
            kind = "IMG";
            sensorId = "";
        }
        else if (rawId == "LBL")
        {
            kind = "LBL";
            sensorId = "";
        }
        else if (rawId.StartsWith("[SIMPLE]", StringComparison.Ordinal))
        {
            kind = "SIMPLE";
            sensorId = rawId[8..];
        }
        else if (rawId.StartsWith("[GAUGE]", StringComparison.Ordinal))
        {
            kind = "GAUGE";
            sensorId = rawId[7..];
        }
        else if (rawId.StartsWith("[GRAPH]", StringComparison.Ordinal))
        {
            kind = "GRAPH";
            sensorId = rawId[7..];
        }
        else
        {
            // plain sensor text (e.g. TMOBO, FCPU, SCPUCLK …)
            kind = "BAR";
            sensorId = rawId;
        }

        double x = ParseDouble(t, "ITMX", 0);
        double y = ParseDouble(t, "ITMY", 0);
        double w = ParseDouble(t, "RESIZW", ParseDouble(t, "WID", 0));
        double h = ParseDouble(t, "RESIZH", ParseDouble(t, "HEI", 0));

        string label = t.GetValueOrDefault("LBL", "");
        string unit  = t.GetValueOrDefault("UNT", "");
        bool showLbl = ParseInt(t, "SHWLBL", 0) == 1;
        bool showVal = ParseInt(t, "SHWVAL", 1) == 1;
        bool showUnt = ParseInt(t, "SHWUNT", 1) == 1;
        string font  = t.GetValueOrDefault("FNTNAM", "Segoe UI");
        int fontSize = ParseInt(t, "TXTSIZ", 12);

        WpfColor textCol  = ParseColor(t, "TXTCOL");
        WpfColor valCol   = ParseColor(t, "VALCOL");
        WpfColor lblCol   = ParseColor(t, "LBLCOL");
        double minVal  = ParseDouble(t, "MINVAL", 0);
        double maxVal  = ParseDouble(t, "MAXVAL", 100);

        string imgFile = t.GetValueOrDefault("IMGFIL", "");
        bool isBg      = ParseInt(t, "BGIMG", 0) == 1;

        // gauge / volume gauge frames
        var frames = new List<string>();
        if (t.TryGetValue("STAFLS", out var stafls))
            frames.AddRange(stafls.Split('|', StringSplitOptions.RemoveEmptyEntries));

        // graph line color
        WpfColor gphCol = ParseColor(t, "GPHCOL");
        WpfColor bgCol  = ParseColor(t, "BGCOL");

        return new Sp2Element(rawId, sensorId, kind, x, y, w, h,
            label, unit, showLbl, showVal, showUnt,
            font, fontSize,
            textCol, valCol, lblCol,
            minVal, maxVal,
            imgFile, frames,
            gphCol, bgCol, isBg);
    }

    private static Dictionary<string, string> ExtractTags(string line)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in TagRx.Matches(line))
            d[m.Groups[1].Value] = m.Groups[2].Value;
        return d;
    }

    private static double ParseDouble(Dictionary<string, string> t, string key, double fallback = 0)
        => t.TryGetValue(key, out var v) && double.TryParse(v, out var r) ? r : fallback;

    private static int ParseInt(Dictionary<string, string> t, string key, int fallback = 0)
        => t.TryGetValue(key, out var v) && int.TryParse(v, out var r) ? r : fallback;

    // AIDA64 colors are Windows COLORREF decimals: low byte = R, next = G, next = B
    private static WpfColor ParseColor(Dictionary<string, string> t, string key)
    {
        if (!t.TryGetValue(key, out var v) || !long.TryParse(v, out var n) || n < 0)
            return WpfColors.Transparent;
        return WpfColor.FromRgb((byte)(n & 0xFF), (byte)((n >> 8) & 0xFF), (byte)((n >> 16) & 0xFF));
    }
}
